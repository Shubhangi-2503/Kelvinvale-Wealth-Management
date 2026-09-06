# Kelvinvale Wealth Management API

A production-grade .NET 10 Web API built to manage UK investment wrappers (ISA, GIA, SIPP), dual-audience authorisation, statutory compliance validation, and auditable financial workflows.

---

## Overview & Quickstart

### Prerequisites
* Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for your platform if not installed yet.
* Install SQL Server (LocalDB, standalone SQL Server, or Docker container).
* Install Postman or cURL for testing endpoints.

### Run Locally with .NET CLI

Clone or download this repository to your local machine:
   ```bash
   git clone [https://github.com/your-org/kelvinvale-api.git](https://github.com/your-org/kelvinvale-api.git)
   cd kelvinvale-api
```
### Run with .NET CLI

1. **Configure Local Environment**  
   Verify or update `src/Kelvinvale.Api/appsettings.Development.json` (see sample below).

2. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

3. **Execute Test Suite**  
   Validate domain invariants, statutory wrapper rules, and authorisation boundaries:
   ```bash
   dotnet test -c Release --verbosity normal
   ```

4. **Apply Database Migrations**
   ```bash
   dotnet ef database update --project src/Kelvinvale.Infrastructure --startup-project src/Kelvinvale.Api
   ```

5. **Start API Server**
   ```bash
   dotnet run --project src/Kelvinvale.Api/Kelvinvale.Api.csproj
   ```

---

### Run with Visual Studio / VS Code

1. Install **Visual Studio 2026** (or VS Code with the **C# Dev Kit** extension).
2. Open `Kelvinvale.sln`.
3. Confirm that `src/Kelvinvale.Api/appsettings.Development.json` exists with your local DB connection string.
4. Set `Kelvinvale.Api` as the **Startup Project**.
5. Press <kbd>F5</kbd> (or select **Debug → Start Debugging**).


### Local Configuration Reference

`src/Kelvinvale.Api/appsettings.Development.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "KeyVaultUri": "",
  "ConnectionStrings": {
    "KelvinvaleDb": "Server=(localdb)\\mssqllocaldb;Database=KelvinvaleDev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
}
```

### Verify Service Health & Test Authentication

* **Check Service Liveness**
  ```bash
  curl -k https://localhost:5001/health
  ```
  *Expected Output:*
  ```json
  { 
    "status": "Healthy", 
    "environment": "Development" 
  }
  ```

* **Test Authenticated Routes**  
  Pass the identity header using an existing GUID from the local seed database:
  ```http
  X-Caller-Id: 3fa85f64-5717-4562-b3fc-2c963f66afa6
  ```
  > The API evaluates the incoming `X-Caller-Id` against SQL Server, enriches the caller's verified role into the `ClaimsPrincipal`, and dynamically enforces relationship-based data ownership (ReBAC).

---

## 1. Authorisation Model, Ownership & Decisions

### Core Architecture & Technical Implementation
We implemented a dynamic, two-tier security pipeline combining database-backed **Claims Enrichment** and **Role-Based Access Control (RBAC)** at the perimeter with **Relationship-Based Access Control (ReBAC / ABAC)** at the data repository boundary:

### Key Decisions, Rationale & Production Roadmap

* **Header Isolation**: Accepted only `X-Caller-Id` because client-asserted roles risk immediate privilege escalation.  
  * *Roadmap:* Transition entirely to cryptographically signed JWTs in production.
* **Per-Request Role Resolution**: Resolved roles directly from the database per request because enforcing immediate deactivation for revoked accounts outweighed query latency.  
  * *Roadmap:* Pair short-lived JWTs with an event-driven, low-latency Redis cache.
* **Two-Tier Enforcement**: Combined perimeter `[Authorize]` RBAC with repository-level ReBAC queries because route attributes cannot prevent horizontal data leaks between customers or advisers.  
  * *Roadmap:* Decouple relationship checks into a dedicated fine-grained authorization engine (e.g., OpenFGA).
* **Dynamic Adviser Book Scoping**: Evaluated `dbo.CustomerAdvisors` dynamically on every query because caching or claim-embedding client IDs causes stale access when books transfer.  
  * *Roadmap:* Retain dynamic database joins while optimising query performance using composite indexes.
*  **Production Identity Transition**: Used a lightweight custom header handler to keep evaluation self-contained without external dependencies.  
  * *Roadmap:* Migrate to a dedicated Duende IdentityServer supporting OAuth 2.0 with PKCE, RS256 JWTs, and OIDC discovery.

### Production Roadmap & Enterprise Identity (IdP) 
Because Kelvinvale operates in wealth management under FCA regulatory compliance, production perimeter security cannot rely on stubbed headers or public multitenant auth:
1. **Custom Dedicated Identity Provider (IdP):** Deploy an isolated, self-hosted IdP service built with **Duende IdentityServer** on .NET 10. It will handle the OAuth 2.0 Authorisation Code Flow + PKCE, user authentication, and hardware-backed MFA (FIDO2/WebAuthn) off the core API request path.
2. **Cryptographic JWT Validation:** The core API will replace header parsing with `Microsoft.AspNetCore.Authentication.JwtBearer`, validating token signatures against the IdP's `/.well-known/openid-configuration` and JWKS endpoints using HSM-managed RS256/ES256 asymmetric keys.

---

## 2(a). What to Tackle Next Given Another Day

* **Global Exception Handler**: Implement a centralized custom exception middleware to intercept unhandled domain and validation errors, ensuring uniform RFC 7807 ProblemDetails responses without leaking internal stack traces.
* **FluentValidation Pipeline**: Integrate a FluentValidation pipeline behavior to execute input validation before controller execution, keeping endpoints lean and guaranteeing only valid payloads reach business logic.
*  **Pagination on Customer Retrieval**: Introduce cursor- or offset-based pagination on `GET /customers` to restrict unbounded query result sets and protect memory usage as the user base expands.
*  **EF Core Global Query Filters**: Configure global query filters on all soft-deletable entities (`WHERE IsActive = 1`) to eliminate repetitive manual filters and prevent accidental exposure of deactivated records.
*  **Application Insights Telemetry**: Add Azure Application Insights with structured telemetry to correlate distributed traces, monitor SQL execution times, and quickly diagnose production failures.

---

## 2(b). Extended Roadmap (Given a Month)
* **High-Throughput Stored Procedures**: Transition critical high-volume writes and complex reporting aggregates into compiled SQL Stored Procedures to bypass ORM mapping overhead, achieving maximum execution performance for heavy data operations while preserving a clean, unified data access layer in C#.
* **Dedicated Self-Hosted Identity Provider**: Replace custom header-based identity simulation with an isolated Duende IdentityServer on .NET, implementing OAuth 2.0 PKCE, WebAuthn MFA, and RS256-signed JWTs.
* **Asynchronous Event-Driven Notifications**: Decouple client communications (confirmations, allowance alerts) from the synchronous HTTP request path using Azure Service Bus and Logic Apps to reduce user-facing latency.
* **Payment Gateway & Open Banking Integration**: Implement Open Banking Payment Initiation Services (PIS) with HMAC-SHA256 verified webhooks to enable real-time account funding and instant settlement transitions.
* **Power BI Investment Intelligence**: Expose an aggregate read-only data model to Microsoft Power BI via DirectQuery to provide clients and advisers with predictive compounding projections and tax-relief forecasting.
---

## 3. Azure Infrastructure, Database Isolation & Azure DevOps CI/CD Pipeline

### Azure Cloud Infrastructure

*  **Azure App Service**: Hosts the Web API with deployment slots to enable zero-downtime blue/green deployments.
*  **Azure SQL Database**: Provides zone-redundant transactional storage to survive physical data center disruptions.
*  **Azure Key Vault**: Stores connection strings and credentials securely, avoiding plaintext secrets in source control.
*  **Azure Application Insights**: Collects distributed traces, SQL execution metrics, and structured telemetry.
*  **Azure Front Door / APIM**: Serves as the perimeter reverse proxy, providing Web Application Firewall (WAF) filtering and rate limiting.

---

###  Environment & Database Isolation

* **Dedicated Environments (DEV, QA, UAT, PROD)**: Deploys separate infrastructure and database instances per tier to prevent test data from contaminating production records.
* **Passwordless Managed Identity**: Uses Azure System-Assigned Managed Identity to pull configuration at runtime without storing passwords in `appsettings.json`.
* **Read-Replica Analytics**: Connects reporting tools like Power BI directly to read replicas, protecting primary write performance.

---

###  CI/CD Pipeline & Quality Gates

1. **Deterministic Dependency Restore**: Runs `dotnet restore --locked-mode` against `packages.lock.json` to prevent dependency drift.
2. **Zero-Tolerance Compilation**: Builds with `/warnaserror` so any compiler warning fails the build immediately.
3. **Automated Test Execution**: Executes unit and integration test suites using `dotnet test`.
4. **85% Branch Coverage Gate**: Enforces an automated quality gate with Coverlet to ensure financial and authorization logic is verified.
5. **Static Security Analysis (SAST)**: Scans for vulnerabilities, tainted data paths, and secret leaks before packaging.
6. **Staging Deployment & Health Probe**: Deploys to an App Service staging slot and verifies service readiness via `GET /health`.
7. **Manual Production Approval & Swap**: Pauses for human sign-off in Azure DevOps before executing a seamless slot swap to live traffic.

---

## 4. AI Usage & Engineering Oversight

In line with professional delivery standards, AI tools were leveraged as force multipliers for research, repetitive scaffolding, and documentation, while all domain rules, financial calculations, and security boundaries were authored, verified, and owned manually.

### Tools & Scope of Use
* **Google Gemini**: Primary accelerator for DTO contracts, EF Core mapping boilerplate, integration test scaffolding, and initial documentation drafting.
* **NotebookLM**: Architectural research assistant for analyzing RFC 7807 specifications, claims transformation patterns, and REST best practices.
* **Claude (Free Tier)**: Second-opinion codebase auditor to identify gaps, architectural risks, and missing patterns (e.g., highlighting the need for a centralized global exception handler). *(Note: Utilized within free-tier limits despite a strong preference for Claude's analytical precision).*

###  Accelerated Workflows
* **Instruction-Driven Implementation**: Rapidly scaffolded and implemented baseline code structures strictly following detailed prompt specifications and architectural constraints.
* **Boilerplate & Contracts**: Generated repetitive request/response DTOs, entity configurations, and baseline FluentValidation skeletons.
* **Test Matrix Scaffolding**: Accelerated boilerplate setup for parameterized integration test fixtures covering Admin, Adviser, and Customer role permutations.
* **Documentation Layout**: Structured architectural sections and operational roadmaps into scannable formats.

### Engineering Overrides, Corrections & Friction Points
* **Security Enforcement**: Rejected AI-proposed client headers (`X-Caller-Role`); replaced with server-side database role resolution from `X-Caller-Id` to prevent privilege escalation.
* **Financial Precision**: Overrode suggested decimal/floating-point types in favor of discrete integer pence (`long AmountPence`) to eliminate rounding drift per UK accounting practices.
* **Test Architecture**: Discarded shallow `Moq` unit test setups in favor of EF Core in-memory providers to test genuine LINQ execution and ReBAC filters.
* **Context Drops & Omissions**: Corrected instances where AI lost conversational context, omitted specified edge-case scenarios, or lost track of pre-existing code.
* **Code Hygiene**: Audited and removed generated dead code, redundant variables, and unsolicited comments.
* **Markdown Citation Artifacts**: Cleaned up repetitive AI hallucination artifacts (e.g., ``) where models failed to follow negative prompting constraints.

### Verification & Ownership
Every line of code and test assertion was manually reviewed, compiled under strict zero-warning policy (`/warnaserror`), and validated against an 85%+ branch coverage gate to ensure domain correctness and FCA compliance.
