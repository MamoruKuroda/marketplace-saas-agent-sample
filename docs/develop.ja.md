# ローカルで開発する

サンプルを自分のマシンでビルド・テスト・実行・設定するために必要なことすべて（Azure 不要）。
[README](../README.ja.md#ローカルで動かす) に 30 秒のクイックスタートがあり、ここではその詳細を補足します。

> 🌐 English: **[develop.md](develop.md)**

## 前提条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。
- 状態ストア用のデータベース。`dotnet run` は既定で **SQLite** を使うため、開始に追加の準備は
  不要です。SQL Server 経路（権威あるストア）を使う場合は、ホストに応じて選択します：

  | ホスト | データベース | 方法 |
  | --- | --- | --- |
  | x86-64（Linux / Intel Mac / Windows x64） | SQL Server | 同梱の `docker-compose.yml` で Docker |
  | arm64（Apple Silicon・Windows-on-ARM） | SQLite | 組み込みプロバイダ、ローカル開発専用 |
  | Windows x64（Docker なし） | SQL Server LocalDB | 同じ接続文字列の切り替え |

- Docker ベースのエンドツーエンド経路には [Fulfillment API Emulator](l2-demo.ja.md)。
  （自動実証は Docker 不要です。）

<details>
<summary>データベースプロバイダの切り替えとマイグレーション</summary>

以下のキー（`appsettings.Development.json` または環境変数）でプロバイダを選択します：

| `Database:Provider` | `Database:ConnectionString` の例 |
| --- | --- |
| `SqlServer`（既定） | `Server=localhost,1433;Database=SaasAgentSample;User Id=sa;<password>;TrustServerCertificate=True;` |
| `Sqlite` | `Data Source=./saas-agent-sample.db` |
| `InMemory` | *(無視される — テスト専用)* |

x86-64 でローカル SQL Server を起動（イメージ `mcr.microsoft.com/mssql/server:2022-latest`）：

```bash
cp .env.example .env       # then edit MSSQL_SA_PASSWORD to a strong value
docker compose up -d sqlserver
```

起動時、SQL Server 経路は `DbContext.Database.Migrate()`（権威あるマイグレーションは
`src/SaaSAgentSample.Data/Persistence/Migrations/`）を実行します。SQLite 経路は
`EnsureCreated()` を実行するため、arm64 開発者は別途マイグレーション履歴を維持せずに反復開発できます。

</details>

## ビルドとテスト

```bash
dotnet build SaaSAgentSample.slnx
dotnet test SaaSAgentSample.slnx
```

既定のテスト実行は SQLite / InMemory 経路のみを対象にします。

<details>
<summary>SQL Server 統合テストもあわせて実行する</summary>

上記の compose サービスを起動し、接続文字列をエクスポートします：

```bash
export SQL_SERVER_CONNECTION='Server=localhost,1433;Database=SaasAgentSample;User Id=sa;<your MSSQL_SA_PASSWORD>;TrustServerCertificate=True;'
dotnet test SaaSAgentSample.slnx
```

</details>

## アプリの起動

```bash
dotnet run --project src/SaaSAgentSample.Web
```

`Development` 環境では、SQLite ストアを使い、購入者サインインを無効化
（`Landing:RequireAuthentication=false`）、Fulfillment クライアントをローカルエミュレーター向けに設定し、
未署名の Webhook トークンを受理します — つまり Entra も実購入もなしで一連のフローがローカルで動きます。

| パス | 内容 |
| --- | --- |
| `/?token=<purchase-token>` | 購入者 SSO ランディング（Resolve → 明示確認 Activate） |
| `/admin`, `/admin/{id}` | パブリッシャー管理（閲覧＋明示確認 Activate） |
| `POST /api/webhook` | 接続 Webhook（サーバー側で Entra JWT ＋ Get Operation 検証） |

<details>
<summary>設定リファレンス</summary>

`appsettings*.json`・環境変数（ネストキーは `__`）・App Service 設定からバインドします。
シークレットは**プレースホルダのみ**。実値をコミットしないでください。

| キー | 目的 | ローカル既定 |
| --- | --- | --- |
| `Database:Provider` | `SqlServer` \| `Sqlite` \| `InMemory` | `Sqlite` |
| `Database:ConnectionString` | 状態ストアの接続 | SQLite ファイル |
| `Landing:RequireAuthentication` | ランディング/管理で Entra サインインを必須にする | `false`（dev） |
| `AzureAd:*` | 購入者サインイン用アプリ（マルチテナント・authority `common`） | プレースホルダの client id |
| `Fulfillment:BaseUrl` | Fulfillment API のベース（`/api` を含む） | エミュレーター |
| `Fulfillment:ApiVersion` | API バージョン | `2018-08-31` |
| `Fulfillment:Webhook:Audience` | 期待する JWT audience = パブリッシャーアプリの client id | プレースホルダ |
| `Fulfillment:Webhook:ExpectedAppId` | 期待する `appid`/`azp` クレーム | 公開 Marketplace アプリ ID |
| `Fulfillment:Webhook:MetadataAddress` | 署名鍵取得用の Entra OpenID メタデータ | — |
| `Fulfillment:Webhook:RequireSignedToken` | JWT 署名を必須にする（**本番では true**） | `false`（dev） |

</details>

## エンドツーエンドで実証する（L2）

フルフィルメントの一連（Resolve → Activate → Webhook → 状態）を実購入なしで通しで実行します。
エミュレーターが実 HTTP 上で Microsoft の代役を務めます。自動テストは Docker なしで CI 上でも
実行され、手動手順では実エミュレーターを Docker で起動します。

```bash
dotnet test --filter FullyQualifiedName~SyntheticL2LifecycleTests
```

手動のエミュレーター手順を含む詳細は **[l2-demo.ja.md](l2-demo.ja.md)**。

## 関連ドキュメント

- [README](../README.ja.md) — 概要とクイックスタート。
- [クラウドデモ / Azure へのデプロイ](deploy.ja.md) — ワンコマンドの `azd up` と、本番寄りの手動手順。
- [L2 ウォークスルー](l2-demo.ja.md) — ライフサイクル全体の実証（自動・手動）。
