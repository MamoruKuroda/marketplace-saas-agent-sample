# marketplace-saas-agent-sample

> **Experimental teaching sample — work in progress.** An agent-ready reference for
> publishing and operating a **Microsoft Commercial Marketplace SaaS Offer** at
> **Tier-1 (flat-rate)** on **.NET 10**. Not for production use.

Agent-assisted SaaS Offer fulfillment: a buyer **SSO landing page**
(Resolve → explicit-confirm Activate), a **connection webhook**, an **authoritative
subscription-state store**, and a **minimal publisher admin** — behind a language-agnostic
**tool boundary** so an LLM/agent layer can be added later without rewriting the
fulfillment plane. The official
[SaaS Accelerator](https://github.com/Azure/Commercial-Marketplace-SaaS-Accelerator) (MIT)
is used as a reference implementation (not a fork), and the
[Fulfillment API Emulator](https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator) (MIT)
drives Resolve/Activate/webhook with no real purchase.

Implementation is tracked in issue #1; the experience-flow walkthrough in issue #2.

## Architecture (v0 — runs entirely locally)

```mermaid
flowchart LR
    subgraph PC["Local machine (v0 L2 walkthrough)"]
        EMU["Fulfillment API Emulator<br/>(stands in for Microsoft, token-free)"]
        subgraph WEB["SaaSAgentSample.Web"]
            LP["Buyer SSO Landing<br/>Resolve to explicit-confirm Activate"]
            WH["Connection Webhook<br/>/api/webhook"]
            ADM["Publisher Admin<br/>inspect + explicit-confirm Activate"]
            TB["Tool boundary<br/>OpenAPI + tool descriptors"]
        end
        DB[("State DB = source of truth<br/>SQL Server via EF Core")]
    end
    EMU -->|marketplace token| LP
    LP -->|Resolve / Activate| EMU
    EMU -->|POST notify + Authorization JWT| WH
    WH -->|Get Operation to authorize| EMU
    LP --- DB
    WH --- DB
    ADM --- DB
    TB -. LLM binds here in v0.1 .-> ADM
```

## Solution layout

| Project | Purpose |
| --- | --- |
| `src/SaaSAgentSample.Core` | Domain model (subscription, state, plan); infrastructure-agnostic |
| `src/SaaSAgentSample.Data` | EF Core state store (single source of truth); SQL Server / Azure SQL |
| `src/SaaSAgentSample.Fulfillment` | Fulfillment/Operations API v2 client + webhook validation (server-side) |
| `src/SaaSAgentSample.Web` | Buyer SSO landing, connection webhook, publisher admin, tool boundary |
| `tests/SaaSAgentSample.Tests` | Unit + integration (synthetic L2) tests |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A local SQL Server for the state store (wired up in a later PR):
  - **Docker** (cross-platform, x86-64): `mcr.microsoft.com/mssql/server`, or
  - **Windows**: SQL Server **LocalDB** (no Docker), or
  - **Apple Silicon (macOS)**: SQLite fallback (documented later).
- The Fulfillment API Emulator for the synthetic L2 walkthrough (wired up later).

## Build & test

```bash
dotnet build SaaSAgentSample.slnx
dotnet test SaaSAgentSample.slnx
```

## Status (incremental — one logical change per PR)

- [x] **PR1** — solution scaffold (.NET 10), CI, README skeleton
- [ ] **PR2** — authoritative state store (EF Core + SQL Server)
- [ ] **PR3** — Fulfillment/Operations v2 client + webhook validation
- [ ] **PR4** — buyer SSO landing (Resolve → explicit-confirm Activate)
- [ ] **PR5** — connection webhook endpoint
- [ ] **PR6** — minimal publisher admin
- [ ] **PR7** — tool boundary (OpenAPI + tool descriptors)
- [ ] **PR8** — synthetic L2 proof via the Emulator
- [ ] **PR9** — README + deploy docs
- [ ] **PR10** — buyer & publisher (SDC/ISV) experience-flow walkthrough (issue #2)

## Guardrails (non-negotiable)

- The **state DB is the single source of truth**; the model never invents entitlement/state.
- **State-changing actions require explicit confirmation.**
- **No purchase/bearer tokens, secrets, or unnecessary PII** in the model context or logs.
- **Webhook Authorization validation is server-side** (Entra JWT + Get Operation), never delegated to the model.

## Deploy

Target: Azure **App Service** + **Azure SQL** (region **West US 3**). Provisioning is
**human-authorized only** and is not performed by the agent. Details land in a later PR.

## Sources (fetched, HTTP 200 on 2026-07-16)

- SaaS fulfillment APIs: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-apis>
- SaaS subscription life cycle: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-life-cycle>
- Implementing a webhook (JWT validation + Get Operation): <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook>
- .NET lifecycle (.NET 10 supported to 2028-11-14): <https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core>

## License

[MIT](LICENSE). Planning provenance: `MamoruKuroda/marketplace-skills` #29 / #30.
