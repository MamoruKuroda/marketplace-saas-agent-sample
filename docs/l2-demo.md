# L2 walkthrough: synthetic fulfillment lifecycle

This sample proves the SaaS fulfillment plumbing end to end **without a real purchase**, using the
Microsoft Commercial Marketplace SaaS Fulfillment APIs as the contract and a token-free emulator as
Microsoft's stand-in. **"L2"** here means an integration-level proof: the app talks to a running
fulfillment API over real HTTP (not a unit-level mock) and reacts to connection webhooks, exercising
the full subscription lifecycle — Resolve → Activate → webhook → state. **"Synthetic"** means the
emulator (or an in-repo HTTP stub) replaces a real marketplace purchase; no real buyer account or
Marketplace subscription is needed.

> 🌐 日本語版: **[l2-demo.ja.md](l2-demo.ja.md)**

There are two ways to run it:

- **A. Automated (runs in CI, no Docker):** an in-repo HTTP stub of the emulator drives the full
  lifecycle over real HTTP. This is the durable proof and needs nothing installed beyond the .NET SDK.
- **B. Manual, against the real emulator:** run the actual
  [Commercial Marketplace SaaS API Emulator](https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator)
  in Docker and drive it from its UI.

---

## A. Automated synthetic L2 (recommended)

The test hosts the real app, points its Fulfillment client at an in-repo stub that implements the
emulator's `/api/saas/subscriptions/...` routes on a real socket, and drives the lifecycle over HTTP:

```bash
dotnet test --filter FullyQualifiedName~SyntheticL2LifecycleTests
```

What it asserts, step by step (authoritative state is checked after each step):

1. **Resolve** — the buyer opens the landing page with a purchase token; the app calls the emulator's
   resolve API and records the subscription as `PendingFulfillmentStart`.
2. **Activate** — with explicit confirmation, the app calls the emulator's activate API; state → `Subscribed`.
3. **ChangePlan webhook** — the app authorizes the notification via **Get Operation**, changes the plan,
   and acknowledges via **Patch Operation** (all over HTTP). Plan → `gold`, state stays `Subscribed`.
4. **Suspend** webhook → `Suspended`.
5. **Reinstate** webhook → `Subscribed`.
6. **Unsubscribe** webhook → `Unsubscribed`.

A second test posts a webhook whose operation the emulator does not know about and asserts the app
**rejects it (403) and does not change state** — the server-side Get Operation check fails closed.

This runs as part of `dotnet test` in both CI lanes.

---

## B. Manual walkthrough against the real emulator

The emulator is a Node app; it runs natively on arm64 (Apple Silicon, Windows-on-ARM). You need Docker.

### 1. Start the emulator

```bash
docker compose up -d --build emulator
```

This builds the emulator from source (pinned commit) and exposes it on `http://localhost:8080`
(the container listens on port 80). It is preconfigured to call this app's webhook at
`http://host.docker.internal:5134/api/webhook` (see `docker-compose.yml`; adjust the port to match
your app URL).

### 2. Run the app pointed at the emulator

The default dev config points the Fulfillment client at `http://localhost:3978/api`; override it to
the emulator's `/api` base and keep dev auth/signature relaxations on:

```bash
# from the repo root
$env:Fulfillment__BaseUrl        = "http://localhost:8080/api"   # PowerShell
$env:Landing__RequireAuthentication = "false"
dotnet run --project src/SaaSAgentSample.Web
```

```bash
# bash equivalent
export Fulfillment__BaseUrl="http://localhost:8080/api"
export Landing__RequireAuthentication="false"
dotnet run --project src/SaaSAgentSample.Web
```

The app listens on `http://localhost:5134` by default. `appsettings.Development.json` already sets
`Fulfillment:Webhook:RequireSignedToken=false` so the emulator's unsigned notifications are accepted.

### 3. Resolve and activate

1. Open the emulator UI at `http://localhost:8080` and **Generate Token**.
2. Copy the generated purchase token, then open this app's landing page with it:
   `http://localhost:5134/?token=<purchase-token>`.
   The page calls **Resolve** and shows the plan.
3. Click **Activate**. The app calls the emulator's **Activate** API and moves the record to `Subscribed`.
4. Confirm at `http://localhost:5134/admin` (or `GET http://localhost:5134/api/subscriptions`).

### 4. Drive webhooks

In the emulator UI, change the plan / quantity, or **Suspend**, **Reinstate**, or **Unsubscribe** the
subscription. The emulator POSTs a connection webhook to `/api/webhook`. The app validates it
server-side (Entra JWT, then **Get Operation** authorization) and updates the authoritative state.
Refresh `/admin` to see the new state.

> The emulator adds realistic delays (`OPERATION_TIMEOUT`, `WEBHOOK_CALL_DELAY`,
> `SUBSCRIPTION_UPDATE_DELAY`); a webhook may take a few seconds to arrive.

### 5. Tear down

```bash
docker compose down
```

---

## Configuration reference

| Setting | Where | Value for L2 |
| --- | --- | --- |
| `Fulfillment:BaseUrl` | app | `http://localhost:8080/api` (emulator), incl. `/api` |
| `Fulfillment:Webhook:RequireSignedToken` | app | `false` (emulator sends unsigned tokens) |
| `Landing:RequireAuthentication` | app | `false` (skip Entra sign-in locally) |
| `WEBHOOK_URL` | emulator | `http://host.docker.internal:5134/api/webhook` |
| `PUBLISHER_ID` | emulator | any value (default `FourthCoffee`) |
| `REQUIRE_AUTH` | emulator | unset/false (token-free) |

## Sources (fetched HTTP 200)

- Emulator repo & docs: <https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator>
  (README, `docs/config.md`, `rest_calls/subscription-apis.http`, `docker/Dockerfile`)
- Implementing a webhook: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook>
- SaaS fulfillment life cycle: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-life-cycle>
