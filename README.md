# Kelvinvale Wealth Management API

A production-grade .NET 10 Web API built to manage UK investment wrappers (ISA, GIA, SIPP), dual-audience authorization, statutory compliance validation, and auditable financial workflows.

---

## Overview & Quickstart

### Prerequisites
- .NET 10 SDK
- Postman (for standalone endpoint testing)
- SQL Server (LocalDB or containerised instance for relational execution)

## 1. Authorisation Model, Ownership & Decisions

### Core Architecture & Technical Implementation
We implemented a dynamic, two-tier security pipeline combining database-backed **Claims Enrichment** and **Role-Based Access Control (RBAC)** at the perimeter with **Relationship-Based Access Control (ReBAC / ABAC)** at the data repository boundary:

* **Zero-Trust Identity Ingestion (`X-Caller-Id` Only):** Callers supply solely an `X-Caller-Id` (GUID) header. The API intentionally does not accept caller-declared role headers.
* **Database Claims Enrichment:** An internal authentication handler resolves the incoming `X-Caller-Id` against the database (`SELECT Role, IsActive FROM Users WHERE Id = @callerId`). If the caller exists and is active, their verified role is injected in-memory into the `ClaimsPrincipal` as `ClaimTypes.Role` and `ClaimTypes.NameIdentifier`. Decommissioned users (`IsActive = 0`) are rejected immediately.
* **Perimeter Role Gate (RBAC):** Standard endpoint attributes (`[Authorize(Roles = "...")]`) evaluate the enriched claims in memory, terminating invalid actions early (e.g., customers cannot hit onboarding routes; advisers cannot place trading instructions).
* **Data Tenancy Gate (ReBAC / ABAC):** Because claims cannot capture dynamic database relationships, repository queries enforce strict isolation (`WHERE CustomerId = @callerId AND IsActive = 1`). Advisers can access only clients explicitly mapped in `dbo.CustomerAdvisors`. Cross-tenant reads return `403 Forbidden`.
* **Soft-Delete Lifecycle:** Physical deletion is strictly prohibited by financial compliance rules. Deactivating an entity sets `IsActive = 0`, updates `ModifiedOn` / `ModifiedById`, and triggers cascading deactivation of dependent child records.
  
### Architectural Rationale & Accepted Trade-Offs
* **Why not pure RBAC?** Pure RBAC only asks, *"Are you an Adviser?"* Under RBAC alone, Adviser A could inspect and modify portfolios belonging to Adviser B's clients, and Customer A could drain Customer B's accounts. The brief explicitly mandates that no customer read or move another's money, and advisers only reach their assigned book. ReBAC eliminates this cross-tenant vulnerability.
* **Why not store customer relationships inside JWT claims?** If an adviser manages 500 accounts, embedding those IDs exceeds HTTP header size limits (causing HTTP 431 errors). Furthermore, if client assignments change, cached token claims remain stale until expiration. Dynamic repository queries against `dbo.CustomerAdvisors` reflect reassignments immediately.
* **Accepted Trade-offs:** For this evaluation, caller identities are stubbed via custom headers rather than federating a live OAuth2/OIDC identity server, keeping execution contained within the 4-hour build limit.

### Production Roadmap & Enterprise Identity (IdP) Cost Analysis
Because Kelvinvale operates in wealth management under FCA regulatory compliance, production perimeter security cannot rely on stubbed headers or public multitenant auth:
1. **Custom Dedicated Identity Provider (IdP):** Deploy an isolated, self-hosted IdP service built with **Duende IdentityServer** on .NET 10. It will handle the OAuth 2.0 Authorisation Code Flow + PKCE, user authentication, and hardware-backed MFA (FIDO2/WebAuthn) off the core API request path.
2. **Cryptographic JWT Validation:** The core API will replace header parsing with `Microsoft.AspNetCore.Authentication.JwtBearer`, validating token signatures against the IdP's `/.well-known/openid-configuration` and JWKS endpoints using HSM-managed RS256/ES256 asymmetric keys.
3. **Dedicated IdP Cost & Run-Rate Analysis:**
   * **Duende IdentityServer Enterprise License:** ~$12,500 – $24,900 / year (financial services tier with multi-client and protocol support).
   * **Azure Key Vault Managed HSM / Premium:** ~$28,000 – $33,000 / year (£2,700/mo for dedicated FIPS 140-2 Level 3 hardware key signing).
   * **High-Availability Compute (Azure Container Apps / App Service):** ~$2,500 – $4,500 / year across paired multi-region instances.
   * **Annual CREST Penetration Testing (FCA Audit Standard):** ~$10,000 – $15,000 / year.
   * **Total Run-Rate:** Hosting a dedicated, bank-grade self-hosted IdP incurs an estimated **£45,000 – £65,000 / year**. *(Alternatively, commercial SaaS providers like Microsoft Entra ID P2 or Auth0 Enterprise charge per active user/month, making a self-hosted IdP more cost-effective once active user volumes scale beyond 10,000 investors).*
---

## 2. Domain Decisions & Statutory Compliance Boundaries

* **ISA Allowance Breach:** Evaluated upfront at submission. If an incoming subscription causes active contributions to exceed £20,000 in the current tax year, it is rejected immediately with RFC 7807 `400 Bad Request` (`ProblemDetails`) rather than failing downstream during settlement.
* **Tax-Year Boundary Crossing:** Instructions lock immutably to the tax year of their recorded submission timestamp (`CreatedOn`). An in-flight transaction submitted on April 4th that settles after April 5th belongs strictly to the prior tax year.
* **SIPP Restrictions:** Validated via the `IProductOpeningRule` strategy (minimum opening age: 18). Withdrawals before the statutory minimum pension age (55) are rejected upfront with `400 Bad Request`.
* **Integer Pence Precision:** All financial values are modelled and calculated as `long` pence (e.g., £5,000.00 = `500000`) to eliminate floating-point rounding errors.
* **Stored Procedures & Audit Trail:** Critical read models and high-throughput transactional flows utilise compiled SQL Stored Procedures (`sp_GetCustomerPortfolioOverview`, `sp_DeactivateCustomerCascade`, `sp_AuditLogLedger`). All mutations stamp `CreatedById`, `CreatedOn`, `ModifiedById`, and `ModifiedOn`.

---

## 3. Azure Architecture & CI/CD Pipeline

### Azure Cloud Target
* **Compute:** Azure App Service (Linux, .NET 10 LTS runtime) or Azure Container Apps.
* **Database:** Azure SQL Database running with `SqlServerRetryingExecutionStrategy`.
* **Secrets & Config:** Azure Key Vault accessed via System-Assigned Managed Identity.
* **Observability:** Application Insights with structured Serilog telemetry capturing correlation IDs across every request, alongside `/health` readiness and liveness endpoints.

### Pull Request Pipeline (GitHub Actions / Azure Pipelines)
Every pull request targeting `main` executes:
```bash
dotnet restore --locked-mode
dotnet build --no-restore -c Release /warnaserror
dotnet test --no-build -c Release --collect: "XPlat Code Coverage"
