## 1. Authorisation Model & Ownership Enforcement

Identity and roles reach the domain via `X-Caller-Id` (GUID) and `X-Caller-Role` (string) request headers, translated into an authenticated `ClaimsPrincipal` via an authentication handler. The platform enforces a two-tier model: **Role Capabilities** followed by **Resource Ownership Isolation**.

### Role Matrix

| Role | Capabilities | Isolation Boundaries & Constraints |
| :--- | :--- | :--- |
| **Admin** | Platform provisioning, create/remove advisers, register admin profiles. | Administrative configuration only; strictly barred from reading customer portfolio balances or moving money. |
| **Adviser** | Onboard customers, open accounts (ISA, GIA, SIPP), view assigned customer books. | Reaches **only assigned customers** (`dbo.CustomerAdvisors`). Cannot place trade instructions[cite: 1]. |
| **Customer** | View owned accounts/holdings, place instructions (Subscription, Withdrawal, Switch), update profile. | Reaches **only self-owned records**[cite: 1]. Cannot provision products or access another customer's money[cite: 1]. |

### Tenancy & Ownership Rules

* **Ownership Verification:** Query boundaries strictly enforce isolation (`WHERE CustomerId = @callerId AND IsActive = 1`)[cite: 1]. Advisers attempting to access non-assigned customers or customers querying another client's assets receive `403 Forbidden`[cite: 1].
* **Soft-Delete Lifecycle (`IsActive = 0`):** Financial compliance strictly forbids physical database deletes[cite: 1]. Deleting a Customer, Adviser, Product, or Instruction updates `IsActive = 0`, populates audit columns (`ModifiedOn`, `ModifiedById`), and triggers cascading deactivation across subordinate links (e.g., deactivating a customer deactivates their active holdings and adviser assignments).
* **Trade-Offs Accepted:** Request headers simulate caller identity instead of a full OIDC/OAuth2 identity server to keep scope contained within the assessment window[cite: 1].

---

## 2. Domain Decisions & Statutory Compliance Boundaries

* **ISA Allowance Breach:** Evaluated upfront at submission[cite: 1]. If an incoming subscription causes active contributions to exceed £20,000 in the current tax year, it is rejected immediately with RFC 7807 `400 Bad Request` (`ProblemDetails`) rather than failing downstream during settlement[cite: 1].
* **Tax-Year Boundary Crossing:** Instructions lock immutably to the tax year of their recorded submission timestamp (`CreatedOn`)[cite: 1]. An in-flight transaction submitted on April 4th that settles after April 5th belongs strictly to the prior tax year[cite: 1].
* **SIPP Restrictions:** Validated via the `IProductOpeningRule` strategy (minimum opening age: 18)[cite: 1]. Withdrawals before the statutory minimum pension age (55) are rejected upfront with `400 Bad Request`[cite: 1].
* **Integer Pence Precision:** All financial values are modeled and calculated as `long` pence (e.g., £5,000.00 = `500000`) to eliminate floating-point rounding errors.
* **Stored Procedures & Audit Trail:** Critical read models and high-throughput transactional flows utilise compiled SQL Stored Procedures (`sp_GetCustomerPortfolioOverview`, `sp_DeactivateCustomerCascade`, `sp_AuditLogLedger`). All mutations stamp `CreatedById`, `CreatedOn`, `ModifiedById`, and `ModifiedOn`.

---

## 3. Azure Architecture & CI/CD Pipeline

### Azure Cloud Target
* **Compute:** Azure App Service (Linux, .NET 10 LTS runtime) or Azure Container Apps[cite: 1].
* **Database:** Azure SQL Database running with `SqlServerRetryingExecutionStrategy`.
* **Secrets & Config:** Azure Key Vault accessed via System-Assigned Managed Identity.
* **Observability:** Application Insights with structured Serilog telemetry capturing correlation IDs across every request, alongside `/health` readiness and liveness endpoints[cite: 1].

### Pull Request Pipeline (GitHub Actions / Azure Pipelines)
Every pull request targeting `main` executes[cite: 1]:
```bash
dotnet restore --locked-mode
dotnet build --no-restore -c Release /warnaserror
dotnet test --no-build -c Release --collect: "XPlat Code Coverage"
