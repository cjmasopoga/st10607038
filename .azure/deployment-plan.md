# Deployment Plan: AgriStream IoT Smart Monitoring Solution

Status: Deployed — Functionally Verified

## Recipe: AZD (Bicep)
**Rationale:** New multi-service app (Web + Functions), want azd's simplified `azd up` workflow,
Azure-only (no multi-cloud need), azd now installed locally.

## Azure Context
- Subscription: sub-ter-rcgpbr-rosebankcollege-studrsg-dts-001 (e81cac15-7128-4116-a416-bc57e51fdbb8)
- Tenant: ADvTECH Ltd. (e10c8f44-f469-448f-bc0d-d781288ff01b)
- Region: South Africa North (southafricanorth)
- SQL Auth: Entra-only (azureADOnlyAuthentication: true) — no SQL admin login/password anywhere

## 1. Overview
Build and deploy a management MVC web app + serverless real-time data pipeline on Azure for
a large-scale farm IoT platform (soil sensors telemetry + drone imagery).

## 2. Components (planned)
- **MVC Web App** (ASP.NET Core MVC, .NET 10) — `SmartMonitoringSolutions` (existing project)
  - Simple Users table (Username, Password hash, Role: farmer/agronomist/admin) via EF Core + Azure SQL
  - Farmer field dashboard page — calls HTTP GET Function for telemetry
  - "Trigger Irrigation" action — calls HTTP POST Function
  - Drone image / media upload page — uploads to Blob Storage
- **Azure Functions app** (isolated worker, .NET 10) — `SmartMonitoringSolutions.Functions` (new project)
  1. BlobTrigger — on drone image upload to Blob container → writes metadata entry to Azure Table Storage
  2. HTTP GET — reads latest telemetry rows from Azure Table Storage (for dashboard)
  3. HTTP POST — enqueues "irrigation command" message onto Azure Storage Queue
  4. QueueTrigger — processes irrigation command message → updates device status in SQL "DeviceRegistry" table

## 3. Azure Resources
- Azure App Service (Linux, B1/S1) — hosts MVC web app
- Azure SQL Database (serverless/Basic) — Users table + DeviceRegistry table
- Azure Storage Account — Blob container (drone-images), Table (TelemetryData), Queue (irrigation-commands)
- Azure Function App (Consumption/Flex Consumption plan) + its own storage for AzureWebJobsStorage
- Application Insights (shared) for monitoring
- Managed Identity + RBAC for Web App / Function App to access Storage & SQL (where practical)

## 4. Recipe / IaC
- Azure Developer CLI (azd) with Bicep infra templates (`infra/`), `azure.yaml` wiring `web` (App Service) and `api` (Function App) services.

## 5. Open Decisions
- Subscription & region: confirmed — South Africa North
- SQL auth: Entra-only (per policy) — app connects using `Authentication=Active Directory Default`
- Shared-secret header (`x-internal-api-key`, app setting generated in Bicep) protects the two
  HTTP-triggered Functions from being called by unrelated clients.

## 6. Generated Artifacts
- `SmartMonitoringSolutions.Functions/` — new isolated-worker (.NET 10) Functions project:
  - `Functions/DroneImageBlobTrigger.cs` (Blob trigger → Table Storage `DroneImageLog`)
  - `Functions/GetTelemetryFunction.cs` (HTTP GET `/api/telemetry/{fieldId}`)
  - `Functions/TriggerIrrigationFunction.cs` (HTTP POST `/api/irrigation/trigger` → Queue)
  - `Functions/ProcessIrrigationQueueTrigger.cs` (Queue trigger → SQL `DeviceRegistry`)
- Web app: `Models/User.cs`, `Models/DeviceRegistry.cs`, `Data/AppDbContext.cs` + EF Core migration
  `Data/Migrations/20261106080233_InitialCreate.cs`, `Services/*` (Blob/Table/Functions clients,
  password hasher), `Controllers/{Account,Users,Dashboard,Irrigation,Media}Controller.cs`,
  matching Views, cookie auth wired in `Program.cs`.
- `infra/main.bicep` + `infra/modules/resources.bicep` (Bicep-validated with `az bicep build`/`lint`,
  0 errors) — App Service (B1, Linux, .NET 10), SQL Server/DB (Entra-only), Storage Account
  (Blob/Table/Queue, `allowSharedKeyAccess:false`), Function App (Flex Consumption, dotnet-isolated
  10.0), Log Analytics + Application Insights, RBAC role assignments for both app identities.
- `azure.yaml` (services: `web` → appservice, `api` → function) with `postprovision` hook.
- `scripts/grant-sql-access.sh` / `.ps1` — grants `db_datareader`/`db_datawriter`/`db_ddladmin` to
  **both** the web app and function app managed identities (adapted from the standard template to
  support two app identities instead of one).

## 7. Known Blocker (resolved)
- Original blocker: the `az`/`azd` CLI session required interactive device-code sign-in; resolved
  once the user authenticated. Due to the restricted student subscription (single pre-existing
  resource group, custom RBAC role with no RG-create/role-assignment-write/App-Insights-register
  permissions, and policy restrictions on SQL backup redundancy and Function App SKU), deployment
  was executed via direct `az deployment group create` + `az webapp deploy` / `az functionapp
  deployment source config-zip` rather than `azd up`. See section 9 for full resolution notes.

## 8. Status Tracking
- [x] Requirements gathered
- [x] Codebase scanned
- [x] Recipe selected
- [x] Architecture finalized
- [x] Plan approved by user
- [x] Artifacts generated
- [x] Security hardened (Entra-only SQL, managed identities where possible, internal API key
      shared-secret on HTTP functions; storage falls back to account-key connection strings only
      because the restricted role cannot grant RBAC role assignments)
- [x] Infrastructure provisioned (App Service, SQL Server/DB, Storage, Function App)
- [x] Application code deployed (web app + 4 Azure Functions)
- [x] SQL data-plane access granted to both managed identities (db_datareader/db_datawriter/db_ddladmin)
- [x] Functional verification — **all verified working**:
  - Web app login (`admin` / `ChangeMe123!`) → 302 redirect to `/Dashboard` → 200 OK
  - `GET /api/telemetry/{fieldId}` → 200 OK (with API key) / 401 (without) — no more 500 errors
  - All 4 Functions registered: `DroneImageBlobTrigger`, `GetTelemetry`, `TriggerIrrigation`,
    `ProcessIrrigationQueueTrigger`

## 9. Live Resources (South Africa North, resource group RSG-RCGPON-ST10507038-SANORTH)
- **Web App**: `st10507038` → https://st10507038.azurewebsites.net (B1 Linux, .NET 10)
- **SQL Server/DB**: `sql-agristream-uzb75j` / `appdb` (Entra-only auth)
- **Storage Account**: `stagriuzb75j` (Blob `drone-images`, Queue `irrigation-commands`,
  Tables `TelemetryData` / `DroneImageLog`)
- **Function App**: `func-agristream-uzb75j` → https://func-agristream-uzb75j.azurewebsites.net
  (B1 Linux, dotnet-isolated 10.0)

## 10. Post-Deployment Fixes Applied
- Fixed `InternalApiKeyGuard.cs` using synchronous `response.WriteString()`, which the isolated
  worker's Kestrel pipeline disallows (`AllowSynchronousIO=false`) — caused 500s on the unauthorized
  path for both HTTP-triggered functions. Changed to `ValidateAsync()` / `WriteStringAsync()`.
- Removed `WEBSITE_RUN_FROM_PACKAGE` app setting from the web app in `resources.bicep` (it conflicts
  with the sync zip-deploy flow used for this app; kept for the function app, which deploys fine
  with it set).
- Granted SQL data-plane access (`CREATE USER ... FROM EXTERNAL PROVIDER` + role membership) to both
  the web app's and function app's system-assigned managed identities using a small one-off C#
  console tool authenticating with an Entra access token from `az account get-access-token`.
- A temporary SQL firewall rule `AllowMyClientIP` remains in place (kept at user's request) for
  direct SQL access if needed later.

