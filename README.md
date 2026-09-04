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

## 2. What to Tackle Next Given Another Day

### 1. Full Soft-Delete & Decommissioning Engine (`IsActive = 0`)
While the `IsActive` flag exists on domain models, the complete deactivation workflow will be implemented:
* **Administrative Decommissioning:** Build secure endpoints allowing authenticated Admins to soft-delete Advisers or Customers.
* **Cascading Soft-Deactivation:** Implement logic to cascade deactivations across related child entities. Deactivating a customer sets `IsActive = 0` across their active wrappers (ISA, GIA, SIPP), associated holdings, and adviser mappings (`dbo.CustomerAdvisors`), ensuring deactivated clients cannot be queried or targeted by financial instructions.
* **EF Core Global Query Filters:** Enforce `modelBuilder.Entity<T>().HasQueryFilter(e => e.IsActive)` on `KelvinvaleDbContext` so inactive records are excluded from all read queries across the system by default.

### 2. Database Optimisation: Stored Procedures vs. EF Core Navigation Graphs
* **High-Throughput Stored Procedures (SPs):** Replace high-volume or complex multi-table operational writes and aggregate read queries (`sp_GetCustomerPortfolioOverview`, `sp_DeactivateCustomerCascade`, `sp_AuditLogLedger`) with compiled SQL Stored Procedures to eliminate ORM query compilation overhead and streamline cross-table mutations into single network round-trips.
* **EF Core Entity Graph Fixup:** For standard REST mutations across multi-table relationships (e.g., creating a Customer while simultaneously provisioning an Account and initial Holdings), fully leverage EF Core navigation properties and change-tracker foreign key fixup to execute transactional parent-child graph persistence within `SaveChangesAsync()` without manual SQL scripts.

### 3. Dedicated Self-Hosted Identity Provider (IdP)
Migrate the perimeter from header-based simulation (`X-Caller-Id`) to an isolated, self-hosted Identity Provider using **Duende IdentityServer** on .NET 10. The IdP will run in a dedicated virtual network, managing user credentials, FIDO2/WebAuthn MFA, and cryptographically signed RS256/ES256 JWT tokens validated via OpenID Connect discovery (`/.well-known/openid-configuration`) and JWKS endpoints.

### 4. Asynchronous Notifications via Azure Logic Apps
Decouple customer notifications (e.g., account opening confirmations, ISA allowance warning alerts, trade completion summaries) from the core synchronous API execution path:
* Expose an internal event trigger using an HTTP-triggered Azure Logic App workflow or Azure Service Bus topic.
* The API will emit JSON event payloads (e.g., `InstructionExecutedEvent`) to the Logic App, which coordinates email dispatch, SMS notifications, and compliance archiving via Office connectors without adding latency to the main transaction.

### 5. Payment Gateway & Open Banking Integration
Implement a secure payment gateway integration for real-time customer account funding:
* Integrate an FCA-authorized Open Banking provider (e.g., TrueLayer or Yapily) or Payment Gateway (Stripe/Modulr) using Payment Initiation Services (PIS).
* Secure webhook endpoints using HMAC-SHA256 signature verification to process instant settlement callbacks, transitioning subscriptions from `Pending` to `Completed` with ledger consistency.

### 6. Power BI Predictive Investment Dashboard
Add predictive portfolio intelligence and forecasting:
* Expose an aggregate, read-only analytics model to Microsoft Power BI via DirectQuery or Azure Synapse.
* Implement financial projection algorithms (compound interest growth, statutory tax relief on SIPP contributions, and ISA tax-free compounding projections based on historical annualised interest rates and variable client risk profiles).
---

## 3. Azure Infrastructure, Database Isolation & Azure DevOps CI/CD Pipeline

### Environment & Database Isolation Strategy
Every tier runs on a separate, dedicated infrastructure stack in Azure to ensure strict tenant and compliance boundaries:

| Environment | Purpose | Target Branch | Approval Required? | Database Hosting & Secret Management |
| :--- | :--- | :--- | :--- | :--- |
| **DEV** | Feature & scratch testing | Personal feature branches (`users/*`, `feature/*`) | **No** (Direct manual deploy) | Dedicated hosted DB (`kelvinvale-db-dev`). Connection string stored in `kv-kelvinvale-dev`. |
| **QA** | Integration & system testing | Sprint release candidate (`develop`, `sprint/*`) | **No** (Direct manual deploy) | Dedicated hosted DB (`kelvinvale-db-qa`). Connection string stored in `kv-kelvinvale-qa`. |
| **UAT** | User acceptance & staging testing | Pre-release / hotfix branches (`release/*`, `hotfix/*`) | **No** (Direct manual deploy) | Dedicated hosted DB (`kelvinvale-db-uat`). Connection string stored in `kv-kelvinvale-uat`. |
| **PROD** | Live FCA-regulated execution | Main / Master (`main`) | **Yes** (Strict human sign-off gate) | Dedicated hosted DB (`kelvinvale-db-prod`). Private Endpoints only; connection string stored in HSM-backed `kv-kelvinvale-prod`. |

#### How Databases & Connected Services Are Configured
1. **Zero Hardcoded Secrets:** Application configurations (`appsettings.json`) contain only placeholders. 
2. **Key Vault Dynamic Resolution:** Each App Service has a single App Setting configured:
   * `KeyVaultUri`: e.g. `https://kv-kelvinvale-prod.vault.azure.net/`
3. **Passwordless Managed Identity:** The App Service in each environment uses its Azure System-Assigned Managed Identity to pull secrets at runtime. It requests `ConnectionStrings--KelvinvaleDb`, which overwrites the in-memory connection string without any plaintext credentials residing in the repository.
4. **External Services (Logic Apps & Power BI):**
   * **Azure Logic Apps:** Connected via managed webhook triggers. The environment-specific trigger endpoint URL is stored in Key Vault as `ExternalServices--LogicAppNotificationUrl`.
   * **Power BI:** Connects directly to read replicas of the respective Azure SQL database using Azure Active Directory (Entra ID) service principals, keeping analytical reporting workloads off the primary transactional write database.


### Pull Request (PR) Quality Gate (Continuous Integration)
Every pull request targeting `main` or `develop` triggers an automated validation pipeline in Azure DevOps before code can be reviewed or merged:
* **Dependency Restore:** `dotnet restore --locked-mode` to ensure reproducible packages.
* **Deterministic Build:** `dotnet build --no-restore -c Release /warnaserror` (any compiler warning fails the build).
* **Test Suite Execution:** Executes all Unit and Integration tests with code coverage collection.
* **85% Coverage Gate:** Enforces an automated quality gate requiring a minimum of 85% branch coverage across financial rule engines and authorisation paths.
* **Static Security Analysis:** Scans code for hardcoded secrets, SQL injection vectors, and dependency vulnerabilities via SARIF/SonarCloud.


### Azure DevOps Deployment Pipeline Architecture

The deployment workflow uses a single, multi-stage Azure DevOps YAML pipeline (`azure-pipelines.yml`) supporting both manual exploratory deployments and regulated production releases:

---

## 4. AI Usage & Engineering Governance

In line with professional delivery standards, AI tools were leveraged as an engineering force-multiplier for architectural research, test scaffolding, and documentation, while all domain logic, financial modelling, and security boundaries were strictly authored and verified manually.

### Tools & Scope of Use
* **NotebookLM:** Used as an interactive research assistant to analyse complex documentation, specifically Microsoft's RESTful Web API Design patterns, RFC 7807 problem details specifications, and claims transformation mechanics.
* **Google Gemini:** Leveraged for rapid boilerplate generation, integration test fixture scaffolding, generating edge-case permutations, and drafting comprehensive architectural documentation.

### Accelerated Workflows (What AI Generated)
* **Integration Test Scaffolding:** Accelerated the creation of parameterised NUnit fixtures testing authorisation matrices across Admin, Adviser, and Customer roles.
* **DTO & Model Boilerplate:** Rapidly generated repetitive request/response contract definitions and FluentValidation rule skeletons.
* **Documentation Structuring:** Assisted in organising operational runbooks and architectural decision records (ADRs) into scannable formats.

### Critical Engineering Overrides (What Was Rejected or Corrected)
* **Floating-Point vs. Integer Precision:** AI models initially suggested using standard `decimal` or `double` types for account balances. This was rejected in favour of discrete `long` integer pence (e.g., £500.00 represented as `50000`) to eliminate floating-point drift and adhere to UK banking accounting standards.
* **Client-Supplied Role Claims:** Initial AI scaffolding suggested accepting `X-Caller-Role` directly from incoming headers. This was overridden with a secure **Claims Enrichment** pattern: the API accepts only `X-Caller-Id` and resolves the verified role directly against the database to prevent client-side privilege escalation.
* **In-Memory Repository Mocks:** AI suggested heavy mocking libraries (`Moq`) for repository layers. This was replaced with Entity Framework Core in-memory providers and container-ready execution patterns to ensure genuine relational querying behaviour during tests.
* **SQL Constraint Handling:** AI relied on catching SQL Server unique constraint codes (`2601/2627`) after the fact for duplicate emails. This was corrected by adding explicit application-level pre-validation checks to ensure consistent 400 Bad Request responses regardless of database provider.

### Verification & Quality Assurance
* All AI-assisted code was subjected to strict static analysis (`/warnaserror`), verified against 85%+ branch coverage gates, and manually audited to ensure full compliance with FCA statutory wrapper rules (e.g., ISA £20k annual contribution thresholds and SIPP minimum age limits).
