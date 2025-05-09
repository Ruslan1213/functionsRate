# 🚀 Azure Durable Functions — Deployment & Infrastructure Setup Guide

## 📌 Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed  
- Azure account with permissions to create resources  
- GitHub repository for CI/CD  
- Visual Studio Code with Azure extensions  
- Node.js or .NET SDK installed (depending on your stack)  

---

## 🔧 1. Set Up Azure Infrastructure

### 🔐 Login to Azure CLI

```bash
az login
```

### 📂 1.1 Create a Resource Group

```bash
az group create --name MyResourceGroup --location westeurope
```

### ☁️ 1.2 Create a Storage Account

```bash
az storage account create   --name mystorageaccountfunc   --location westeurope   --resource-group MyResourceGroup   --sku Standard_LRS
```

### ⚙️ 1.3 Create a Function App

```bash
az functionapp create   --resource-group MyResourceGroup   --consumption-plan-location westeurope   --runtime dotnet   --functions-version 4   --name my-durable-func-app   --storage-account mystorageaccountfunc
```

### ✅ 1.4 Ensure the Function App is Running

```bash
az functionapp start --name my-durable-func-app --resource-group MyResourceGroup
```

---

## 🌌 2. Set Up Azure Cosmos DB

### 2.1 Create Cosmos DB Account

```bash
az cosmosdb create   --name mycosmosdbaccount   --resource-group MyResourceGroup   --kind GlobalDocumentDB   --locations regionName=westeurope
```

### 2.2 Create Database and Container

```bash
az cosmosdb sql database create   --account-name mycosmosdbaccount   --name ExchangeRatesDB   --resource-group MyResourceGroup

az cosmosdb sql container create   --account-name mycosmosdbaccount   --database-name ExchangeRatesDB   --name Rates   --partition-key-path "/from"   --resource-group MyResourceGroup
```

### 2.3 Retrieve Cosmos DB Connection String

```bash
az cosmosdb keys list   --name mycosmosdbaccount   --resource-group MyResourceGroup   --type connection-strings
```

---

## 🔐 3. Set Up Azure Key Vault

### 3.1 Create Key Vault

```bash
az keyvault create --name myKeyVault --resource-group MyResourceGroup --location westeurope
```

### 3.2 Add Secrets

```bash
az keyvault secret set   --vault-name myKeyVault   --name ExchangeRateApiKey   --value "<your-API-key>"

az keyvault secret set   --vault-name myKeyVault   --name CosmosDbConnectionString   --value "<your-connection-string>"
```

### 3.3 Grant Access to Function App (Managed Identity)

Assign identity to the Function App:

```bash
az functionapp identity assign   --name my-durable-func-app   --resource-group MyResourceGroup
```

Get the principal ID:

```bash
az functionapp show   --name my-durable-func-app   --resource-group MyResourceGroup   --query identity.principalId --output tsv
```

Use the output principalId to set the Key Vault policy:

```bash
az keyvault set-policy   --name myKeyVault   --object-id <principalId>   --secret-permissions get list
```

---

## 🚀 4. GitHub Actions CI/CD Setup

### 4.1 Update `deploy.yml`

Replace:

```yaml
app-name: 'YourFunctionAppName'
```

With your Function App name:

```yaml
app-name: 'my-durable-func-app'
```

### 4.2 Generate `AZURE_CREDENTIALS` Secret

```bash
az ad sp create-for-rbac --name "GitHubActions" --sdk-auth
```

Copy the full JSON output.

### 4.3 Add GitHub Secret

1. Go to your GitHub repo → **Settings** → **Secrets and variables** → **Actions**  
2. Click **New repository secret**  
3. Name: `AZURE_CREDENTIALS`  
4. Value: paste the JSON from the previous command  

---

## 🧪 5. Local Development & Debugging

### 5.1 Install Required Tools

- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Visual Studio Code](https://code.visualstudio.com/)
- VS Code Extensions:
  - Azure Functions
  - Azure Account
  - C# or Node.js (depending on your runtime)

### 5.2 Run Locally

```bash
func start
```

---

## ✅ You’re All Set!

You now have a full setup for Azure Durable Functions: infrastructure, secrets, CI/CD, and local debugging.
