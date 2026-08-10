# AvenChart Azure Deployment Operations

These Bicep files are the infrastructure source of truth used by the protected
Azure Deployment Operations page.

The deployment is intentionally limited to synthetic, development, and test
environments. It creates a small Azure Container Apps Consumption environment,
private Azure Database for PostgreSQL Flexible Server, Basic Azure Container
Registry, Key Vault, Log Analytics workspace, virtual network, cost budget,
one-shot schema-migration job, and a multi-container AvenChart application.

Deployment phases are separate so that the registry exists before images are
built and the database is migrated before the application revision receives
traffic:

1. `main.bicep` creates the resource group and platform.
2. The operations service builds the API and UI images in Azure Container Registry.
3. `migration.bicep` creates and runs the one-shot schema migration job.
4. `application.bicep` creates or updates the application revision.

No credential is committed to this directory. The deployment service generates
the initial PostgreSQL administrator credential or reuses the existing Key Vault
secret and supplies it through a short-lived secure parameter file.

## Conservative default profile

The page starts with a profile for 20 named users and 10 concurrent users. It
uses one warm Container Apps replica, permits two replicas at peak, and gives
the API 0.5 vCPU/1 GiB and the UI 0.25 vCPU/0.5 GiB per replica. PostgreSQL uses
the Burstable `Standard_B1ms` SKU with 32 GiB storage and seven days of backup.
The API pool is 15 connections per replica (30 possible pooled connections at
the default two-replica ceiling), leaving operating headroom on that database
class. The validator recalculates this envelope whenever sizing changes and
rejects unsafe totals. Treat this as a starting point and load-test the actual
workflow before increasing the user commitment.

Private networking, Key Vault references, managed identity, HTTPS ingress,
health probes, Log Analytics, a cost budget, and multiple Container Apps
revisions are enabled. PostgreSQL high availability, geo-redundant backup, and
extra replicas are opt-in because they materially increase cost. The budget is
an alerting guardrail, not a spending cap.

## Operator-host prerequisites

The deployment coordinator runs Azure CLI commands from the API process. Run
that process on a trusted operator host with:

- Azure CLI installed and signed in to the selected tenant and subscription;
- this repository available on disk (or set `AzureOperations__RepositoryRoot`);
- Docker contexts buildable by Azure Container Registry;
- permission to create subscription deployments, resource groups, role
  assignments, provider registrations, and the resources in these templates.

Owner is sufficient for an initial sandbox. A tighter custom role can replace
it, but it must include the preceding actions; User Access Administrator plus
resource-specific Contributor roles is a common separation. The page's access
check reports identity, subscription, provider registration, and coarse role
assignment evidence before a plan or deployment.

The API container used by the normal local Docker quickstart intentionally does
not carry operator Azure credentials or mount the source tree. For deployment,
run the API directly on the trusted operator host, or build a separately secured
operator image with Azure CLI, workload identity, and a read-only source mount.

## Safe enablement

### Operations access-code gate

The administration role is necessary but is not sufficient to read this page or
call its protected API endpoints. Every protected request must also carry a
short-lived Operations grant bound to the current AvenChart login session. A new
database is initialized with the bootstrap code `AvenChartAdmin`. The first
successful unlock permits only changing or locking that code; the API will not
return Azure configuration, history, health, or deployment data until a private
replacement has been set.

The replacement must be 12 to 128 characters. It is stored only as a salted
PBKDF2-HMAC-SHA256 hash. Changing it revokes every active Operations grant.
Grants expire after 15 minutes, are kept only in browser memory, and are lost on
refresh or tab close. Five failed attempts within 15 minutes trigger a 15-minute
session lockout, and unlocks, failures, grant rejection, locking, and code
changes are audited. Use **Lock Operations** before leaving an unattended
session.

Because the bootstrap code is public documentation, change it immediately after
the initial deployment and store the replacement in the organization's password
manager. Do not reuse the shared demo account password. Recovery from a lost
Operations code is an explicit database-administrator procedure: rotate the
stored hash through a reviewed migration or maintenance command, then revoke all
existing grants. The application intentionally has no email or shared-admin
reset path.

Profiles, validation, history, and ARM what-if planning are available by default.
Resource-changing execution is an explicit host setting and remains off in
source control:

```powershell
$env:AzureOperations__AllowDeploymentExecution = 'true'
$env:AzureOperations__RepositoryRoot = (Resolve-Path .).Path
dotnet run --project .\avenchart\backend\src\AvenChart.Api\AvenChart.Api.csproj -- --migrate-only
dotnet run --project .\avenchart\backend\src\AvenChart.Api\AvenChart.Api.csproj
```

Keep the setting off on ordinary application hosts. The endpoints are also
inside the existing administration authorization boundary, require the
`admin:acl` write capability, and enforce the independent Operations access-code
gate described above. A deploy requires the operator to type the exact profile
name; rollback has a separate typed confirmation. Only one execution can
be active for a profile, cancellation is best-effort, and every state transition
is appended to the execution event log.

## Information stored by the page

The database stores subscription and tenant identifiers, naming, region,
sizing, scaling, pool limits, network ranges, retention, alert recipients,
image tags, custom-domain intent, validation results, immutable profile
revisions, execution history, and resource outputs. It does not store Azure
access tokens or the generated PostgreSQL password. The password and full
connection string live in Key Vault; Container Apps consumes the latter through
a managed-identity Key Vault reference. Temporary ARM parameter files are
created with restrictive permissions and deleted after each command.

Custom-domain and allowed-IP information is collected and validated as operator
intent. DNS ownership, certificate issuance, and the final ingress restriction
must be completed and verified for the organization's network before relying on
them as controls.

## Deployment and recovery flow

1. Create a draft and enter the Azure scope, globally unique names, workload,
   database, network, governance, and image information.
2. Save, validate, and run the Azure access check. Resolve all blocking errors.
3. Run **What-if plan** and inspect the complete Azure change list.
4. Type the profile name and deploy. The coordinator registers providers,
   provisions the platform, builds images in ACR, optionally seeds synthetic
   data once, runs the migration job, deploys the application revision, and
   verifies health.
5. Monitor structured phase events and use **Verify now** for later checks.
6. If an application revision is unhealthy, select a known revision and use
   rollback to shift Container Apps traffic. Database migrations are forward-
   only; investigate migration compatibility before rolling application code
   back. Archiving a profile removes it from normal page use but never deletes
   Azure resources.

Before changing or deleting infrastructure, export the profile and execution
history, confirm PostgreSQL restore coverage, and use Azure resource locks and
organizational policy appropriate for the environment.
