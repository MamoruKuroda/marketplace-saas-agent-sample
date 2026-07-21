# Azure へのデプロイ（App Service + Azure SQL）

> **人間の承認がある場合のみ。** エージェントはプロビジョニングもデプロイも**行いません**。これは
> リファレンス手順です。レビューのうえ、ご自身で実行してください。以下の識別子はすべて
> **プレースホルダ**です — 実テナント/サブスクリプション/パブリッシャー/アプリの ID やシークレットを
> コミットしないでください。

> 🌐 English: **[deploy.md](deploy.md)**

v0（初期バージョン、最小構成）の対象トポロジ：

- **Azure App Service**（Linux, .NET 10）が `SaaSAgentSample.Web` をホスト。
- **Azure SQL Database** が権威ある状態ストア。
- **マネージド ID** で App Service → Azure SQL を**パスワードレス**接続（接続文字列にシークレットなし）。
- リージョン：**West US 3**（本サンプルの統合テストで使用した実績から選定）。

```mermaid
flowchart LR
    MP["Microsoft Commercial Marketplace"]
    subgraph AZ["Azure (West US 3)"]
        APP["App Service (.NET 10)<br/>SaaSAgentSample.Web"]
        SQL[("Azure SQL Database<br/>state = source of truth")]
    end
    MP -->|marketplace token| APP
    APP -->|Resolve / Activate / Get Operation| MP
    MP -->|connection webhook + Entra JWT| APP
    APP -->|managed identity, passwordless| SQL
```

## 前提条件

- Azure サブスクリプションと [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)。
- 購入者サインインとマーケットプレースのセキュリティトークン用に登録済みの **Microsoft Entra アプリケーション**
  （[Register a SaaS application](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-registration) 参照）。
- Partner Center 上の取引可能な SaaS オファー（Partner Center 前のテスト用にはエミュレーターでも可）。

## 1. プロビジョニング（例示）

```bash
# Placeholders — replace <...>. Do not paste real IDs/secrets into source control.
LOCATION=westus3
RG=<resource-group>
PLAN=<app-service-plan>
APP=<app-name>                 # becomes https://<app-name>.azurewebsites.net
SQLSERVER=<sql-server-name>
SQLDB=SaasAgentSample

az group create -n "$RG" -l "$LOCATION"

az appservice plan create -g "$RG" -n "$PLAN" --is-linux --sku B1
az webapp create -g "$RG" -p "$PLAN" -n "$APP" --runtime "DOTNETCORE:10.0"

az sql server create -g "$RG" -n "$SQLSERVER" -l "$LOCATION" --enable-ad-only-auth \
  --external-admin-principal-type User \
  --external-admin-name "<aad-admin-upn>" --external-admin-sid "<aad-admin-object-id>"
az sql db create -g "$RG" -s "$SQLSERVER" -n "$SQLDB" --service-objective S0
```

> Azure SQL は **Entra 専用**（`--enable-ad-only-auth`）でプロビジョニングするため、管理すべき SQL
> パスワードがありません。[What is Azure SQL Database](https://learn.microsoft.com/en-us/azure/azure-sql/database/sql-database-paas-overview?view=azuresql) 参照。

## 2. パスワードレス接続（マネージド ID）

アプリにマネージド ID を付与し、contained user としてデータベースへのアクセスを許可します。
[Connect .NET apps to Azure SQL with managed identity](https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database) に従います。

```bash
az webapp identity assign -g "$RG" -n "$APP"
```

次に、Entra 管理者としてデータベースに接続し、アプリの ID 用のユーザーを作成して最小権限ロールを
付与します：

```sql
CREATE USER [<app-name>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<app-name>];
ALTER ROLE db_datawriter ADD MEMBER [<app-name>];
ALTER ROLE db_ddladmin  ADD MEMBER [<app-name>];   -- needed for EF Core Migrate() on startup
```

接続文字列に**シークレットは含まれません** — 認証はマネージド ID です：

```
Server=tcp:<sql-server-name>.database.windows.net,1433;Database=SaasAgentSample;Authentication=Active Directory Default;Encrypt=True;
```

## 3. アプリ設定

App Service に構成を設定します（App settings のネストキーは `__`）。ID はすべてプレースホルダです。

```bash
az webapp config appsettings set -g "$RG" -n "$APP" --settings \
  Database__Provider=SqlServer \
  "Database__ConnectionString=Server=tcp:$SQLSERVER.database.windows.net,1433;Database=$SQLDB;Authentication=Active Directory Default;Encrypt=True;" \
  Landing__RequireAuthentication=true \
  AzureAd__Instance=https://login.microsoftonline.com/ \
  AzureAd__TenantId=common \
  AzureAd__ClientId=<landing-app-client-id> \
  Fulfillment__BaseUrl=https://marketplaceapi.microsoft.com/api \
  Fulfillment__ApiVersion=2018-08-31 \
  Fulfillment__Webhook__Audience=<publisher-app-client-id> \
  Fulfillment__Webhook__ExpectedAppId=20e940b3-4c07-4bc1-a733-45f7c7a3d0e3 \
  Fulfillment__Webhook__MetadataAddress=https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration \
  Fulfillment__Webhook__RequireSignedToken=true
```

補足：

- `Fulfillment:Webhook:RequireSignedToken` は本番では **`true` 必須**（ローカルの `false` は
  トークン不要のエミュレーター専用）。
- `ExpectedAppId` は既定で**公開**の Microsoft Marketplace アプリ ID `20e940b3-…`（文書化された定数で
  あり、シークレットではありません）。
- 機微とみなす値には [Key Vault references](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
  を推奨。マネージド ID により**データベースのシークレットは保存不要**です。

## 4. アプリのデプロイ

```bash
dotnet publish src/SaaSAgentSample.Web -c Release -o ./publish
cd publish && zip -r ../app.zip . && cd ..
az webapp deploy -g "$RG" -n "$APP" --src-path app.zip --type zip
```

初回起動時、SQL Server 経路では権威ある EF Core マイグレーション（`Database.Migrate()`）が実行され、
スキーマが作成されます。[Deploy an ASP.NET web app](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore) 参照。

## 5. マーケットプレースオファーの配線（Partner Center）

SaaS オファーの **Technical configuration** で：

| 項目 | 値 |
| --- | --- |
| Landing page URL | `https://<app-name>.azurewebsites.net/` |
| Connection webhook | `https://<app-name>.azurewebsites.net/api/webhook` |
| Microsoft Entra tenant ID | `<your-tenant-id>` |
| Microsoft Entra application ID | `<publisher-app-client-id>` |

テナント/アプリ ID は、フルフィルメント API 認証に使うアプリ登録のものです
（[Register a SaaS application](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-registration)、
[Implementing a webhook](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook) 参照）。

## 6. 確認

- `https://<app-name>.azurewebsites.net/admin` を開く（本番ではサインイン必須）。
- Partner Center の preview またはエミュレーターから購入を駆動し、購読が表示され正しく遷移することを
  確認する。

## 7. 破棄（teardown）

```bash
az group delete -n "$RG" --yes --no-wait
```

## Azure 上のガードレール

- **状態 DB が唯一の正本**であることは、ここでも変わりません。
- **可能な限りソースや app settings にシークレットを置かない** — SQL はマネージド ID、その他は
  Key Vault references。本ドキュメントの ID はプレースホルダです。
- Webhook の Authorization 検証は**サーバー側**（Entra JWT + Get Operation）のままです。

## 出典（2026-07-18 に HTTP 200 で取得確認）

- Deploy an ASP.NET web app to App Service: <https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore>
- Connect .NET apps to Azure SQL with managed identity: <https://learn.microsoft.com/en-us/azure/app-service/tutorial-connect-msi-sql-database>
- What is Azure SQL Database: <https://learn.microsoft.com/en-us/azure/azure-sql/database/sql-database-paas-overview?view=azuresql>
- Register a SaaS application: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-registration>
- Implementing a webhook: <https://learn.microsoft.com/en-us/partner-center/marketplace-offers/pc-saas-fulfillment-webhook>
