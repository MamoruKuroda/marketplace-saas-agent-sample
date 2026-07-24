# Develop locally

Everything you need to build, test, run, and configure the sample on your own machine — no Azure.
The [README](../README.md#run-locally) has the 30-second quickstart; this fills in the details.

> 🌐 日本語版: **[develop.ja.md](develop.ja.md)**

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- A state-store database. `dotnet run` defaults to **SQLite**, so nothing extra is needed
  to start. For the SQL Server path (the authoritative store), pick the row for your host:

  | Host | Database | How |
  | --- | --- | --- |
  | x86-64 (Linux / Intel Mac / Windows x64) | SQL Server | Docker, via the bundled `docker-compose.yml` |
  | arm64 (Apple Silicon, Windows-on-ARM) | SQLite | built-in provider, local dev only |
  | Windows x64, no Docker | SQL Server LocalDB | same connection-string switch |

- For the Docker-based end-to-end path, the [Fulfillment API Emulator](l2-demo.md).
  (The automated proof needs no Docker.)

<details>
<summary>Database provider switch & migrations</summary>

Select the provider with these keys (in `appsettings.Development.json` or env vars):

| `Database:Provider` | `Database:ConnectionString` example |
| --- | --- |
| `SqlServer` (default) | `Server=localhost,1433;Database=SaasAgentSample;User Id=sa;******;TrustServerCertificate=True;` |
| `Sqlite` | `Data Source=./saas-agent-sample.db` |
| `InMemory` | *(ignored — used only for tests)* |

Start a local SQL Server on x86-64 (image `mcr.microsoft.com/mssql/server:2022-latest`):

```bash
cp .env.example .env       # then set MSSQL_SA_PASSWORD to a strong value
docker compose up -d sqlserver
```

On startup the SQL Server path runs `DbContext.Database.Migrate()` (authoritative
migrations in `src/SaaSAgentSample.Data/Persistence/Migrations/`); the SQLite path runs
`EnsureCreated()`, so arm64 developers can iterate without a separate migration history.

</details>

## Build & test

```bash
dotnet build SaaSAgentSample.slnx
dotnet test SaaSAgentSample.slnx
```

The default test run covers the SQLite / InMemory paths only.

<details>
<summary>Also running the SQL Server integration tests</summary>

Start the compose service above, then export a connection string:

```bash
export SQL_SERVER_CONNECTION='Server=localhost,1433;Database=SaasAgentSample;User Id=sa;<your MSSQL_SA_PASSWORD>;TrustServerCertificate=True;'
dotnet test SaaSAgentSample.slnx
```

</details>

## Run the app

```bash
dotnet run --project src/SaaSAgentSample.Web
```

The `Development` environment uses the SQLite store, disables buyer sign-in
(`Landing:RequireAuthentication=false`), points the fulfillment client at the local
emulator, and accepts unsigned webhook tokens — so the whole flow works without Entra or a
real purchase.

| Path | What it is |
| --- | --- |
| `/?token=<purchase-token>` | Buyer SSO landing (Resolve → explicit-confirm Activate) |
| `/admin`, `/admin/{id}` | Publisher admin (inspect + explicit-confirm Activate) |
| `POST /api/webhook` | Connection webhook (server-side Entra JWT + Get Operation) |

<details>
<summary>Configuration reference</summary>

Bind from `appsettings*.json`, environment variables (`__` for nested keys), or App
Service settings. Secrets are **placeholders only** — never commit real values.

| Key | Purpose | Local default |
| --- | --- | --- |
| `Database:Provider` | `SqlServer` \| `Sqlite` \| `InMemory` | `Sqlite` |
| `Database:ConnectionString` | State store connection | SQLite file |
| `Landing:RequireAuthentication` | Require Entra sign-in for landing/admin | `false` (dev) |
| `AzureAd:*` | Buyer sign-in app (multitenant; authority `common`) | placeholder client id |
| `Fulfillment:BaseUrl` | Fulfillment API base (incl. `/api`) | emulator |
| `Fulfillment:ApiVersion` | API version | `2018-08-31` |
| `Fulfillment:Webhook:Audience` | Expected JWT audience = publisher app client id | placeholder |
| `Fulfillment:Webhook:ExpectedAppId` | Expected `appid`/`azp` claim | public Marketplace app id |
| `Fulfillment:Webhook:MetadataAddress` | Entra OpenID metadata for signing keys | — |
| `Fulfillment:Webhook:RequireSignedToken` | Enforce JWT signature (**true in prod**) | `false` (dev) |

</details>

## Prove it end to end (L2)

Run the whole fulfillment lifecycle — Resolve → Activate → webhook → state — with no real
purchase. The emulator stands in for Microsoft over real HTTP. An automated test does this
in CI with no Docker; a manual path runs the real emulator in Docker.

```bash
dotnet test --filter FullyQualifiedName~SyntheticL2LifecycleTests
```

Full details, including the manual emulator path: [l2-demo.md](l2-demo.md).

## See also

- [README](../README.md) — overview and quickstart.
- [Deploy a cloud demo / to Azure](deploy.md) — one-command `azd up`, and the production-shaped manual walkthrough.
- [L2 walkthrough](l2-demo.md) — the full lifecycle proof (automated and manual).
