# L2 ウォークスルー：合成フルフィルメント・ライフサイクル

本サンプルは、フルフィルメントの配管を実購入なしでエンドツーエンドに実証します。Microsoft 商用
マーケットプレースの SaaS Fulfillment API を契約として用い、トークン不要のエミュレーターを Microsoft の
代役に使います。

タイトルの2つの用語：

- **L2** — 統合レベルの実証。アプリが実 HTTP 上でフルフィルメント API（ユニットモックではない）と通信し、
  接続 Webhook に反応して、購読ライフサイクル全体（Resolve → Activate → Webhook → 状態）を駆動します。
- **合成（Synthetic）** — エミュレーター（または in-repo の HTTP スタブ）が実購入の代わりを務めます。
  実購入者アカウントや実マーケットプレース購読は不要です。

> 🌐 English: **[l2-demo.md](l2-demo.md)**

実行方法は2通りあります：

- **A. 自動（CI 上で実行・Docker 不要）：** エミュレーターの in-repo HTTP スタブが実 HTTP 上でライフ
  サイクル全体を駆動します。これが恒久的な実証で、.NET SDK 以外に何もインストール不要です。
- **B. 手動・実エミュレーター相手：** 実物の
  [Commercial Marketplace SaaS API Emulator](https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator)
  を Docker で起動し、その UI から駆動します。

> **ローカルに何もインストールしたくない場合は？** [クラウドデモ](../README.ja.md#クラウドにデモをデプロイazd)
> （`azd up`）が、このエミュレーターをアプリと一緒に Azure へデプロイするので、ブラウザでライフサイクル全体を
> クリック体験できます。以下は**ローカル Docker** の手順です。

---

## A. 自動の合成 L2（推奨）

このテストは実アプリをホストし、その Fulfillment クライアントを、エミュレーターの
`/api/saas/subscriptions/...` ルートを実ソケット上で実装した in-repo スタブに向け、HTTP 上でライフ
サイクルを駆動します：

```bash
dotnet test --filter FullyQualifiedName~SyntheticL2LifecycleTests
```

各ステップで検証する内容（各ステップ後に権威ある状態を確認）：

1. **Resolve** — 購入者が購入トークンでランディングページを開くと、アプリはエミュレーターの resolve API
   を呼び、購読を `PendingFulfillmentStart` として記録。
2. **Activate** — 明示確認のうえ、アプリはエミュレーターの activate API を呼ぶ。状態 → `Subscribed`。
3. **ChangePlan Webhook** — アプリは **Get Operation** で通知を認可し、プランを変更、**Patch Operation**
   で ack（すべて HTTP 上）。プラン → `gold`、状態は `Subscribed` のまま。
4. **Suspend** Webhook → `Suspended`。
5. **Reinstate** Webhook → `Subscribed`。
6. **Unsubscribe** Webhook → `Unsubscribed`。

2つ目のテストは、エミュレーターが知らない operation を含む Webhook を POST し、アプリが**それを拒否
（403）し状態を変えない**ことを検証します — サーバー側の Get Operation チェックが fail-closed である証拠です。

これは両 CI レーンの `dotnet test` の一部として実行されます。

---

## B. 実エミュレーター相手の手動ウォークスルー

エミュレーターは Node アプリで、arm64（Apple Silicon・Windows-on-ARM）でネイティブに動作します。Docker が必要です。

### 1. エミュレーターを起動

```bash
docker compose up -d --build emulator
```

これはエミュレーターをソースから（pin したコミットで）ビルドし、`http://localhost:8080` で公開します
（コンテナはポート 80 を待ち受け）。このアプリの Webhook（`http://host.docker.internal:5134/api/webhook`）
を呼ぶよう事前設定済みです（`docker-compose.yml` 参照。ポートはアプリの URL に合わせて調整）。

### 2. エミュレーターに向けてアプリを起動

既定の dev 設定は Fulfillment クライアントを `http://localhost:3978/api` に向けています。これを
エミュレーターの `/api` ベースに上書きし、dev の認証/署名緩和は有効のままにします：

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

アプリは既定で `http://localhost:5134` を待ち受けます。`appsettings.Development.json` は
`Fulfillment:Webhook:RequireSignedToken=false` を設定済みなので、エミュレーターの未署名通知を受理します。

### 3. Resolve と Activate

1. エミュレーター UI（`http://localhost:8080`）を開き **Generate Token**。
2. 生成された購入トークンをコピーし、このアプリのランディングページを開く：
   `http://localhost:5134/?token=<purchase-token>`。ページは **Resolve** を呼びプランを表示します。
3. **Activate** をクリック。アプリはエミュレーターの **Activate** API を呼び、レコードを `Subscribed` に。
4. `http://localhost:5134/admin` で購読を確認。

### 4. Webhook を駆動

エミュレーター UI で、プラン/数量を変更、または **Suspend**・**Reinstate**・**Unsubscribe** します。
エミュレーターは `/api/webhook` へ接続 Webhook を POST します。アプリはそれをサーバー側で検証し
（Entra JWT、次に **Get Operation** 認可）、権威ある状態を更新します。`/admin` を再読み込みして新しい状態を確認。

> エミュレーターは現実的な遅延（`OPERATION_TIMEOUT`・`WEBHOOK_CALL_DELAY`・`SUBSCRIPTION_UPDATE_DELAY`）を
> 加えるため、Webhook の到着に数秒かかることがあります。

### 5. 破棄（teardown）

```bash
docker compose down
```

---

## 設定リファレンス

| 設定 | 場所 | L2 での値 |
| --- | --- | --- |
| `Fulfillment:BaseUrl` | app | `http://localhost:8080/api`（エミュレーター）。`/api` を含む |
| `Fulfillment:Webhook:RequireSignedToken` | app | `false`（エミュレーターは未署名トークンを送る） |
| `Landing:RequireAuthentication` | app | `false`（ローカルでは Entra サインインをスキップ） |
| `WEBHOOK_URL` | emulator | `http://host.docker.internal:5134/api/webhook` |
| `PUBLISHER_ID` | emulator | 任意の値（既定 `FourthCoffee`） |
| `REQUIRE_AUTH` | emulator | 未設定/false（トークン不要） |

## 出典（HTTP 200 で取得確認）

- Emulator repo & docs: <https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator>
  （README, `docs/config.md`, `rest_calls/subscription-apis.http`, `docker/Dockerfile`）
- Implementing a webhook: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook>
- SaaS subscription life cycle: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-life-cycle>
