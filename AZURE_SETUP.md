# Azure Services Setup Guide

Este guia explica como configurar os serviços Azure necessários para o funcionamento da aplicação.

## 📋 Pré-requisitos

- Conta Azure ativa
- Azure CLI instalado
- Permissões para criar recursos no Azure

## 🚌 Azure Service Bus

### 1. Criar Service Bus Namespace

```bash
# Criar resource group
az group create --name rg-sre-observability --location brazilsouth

# Criar Service Bus namespace
az servicebus namespace create \
  --resource-group rg-sre-observability \
  --name sb-sre-observability-poc \
  --location brazilsouth \
  --sku Standard
```

### 2. Criar Topic e Subscriptions

```bash
# Criar topic para eventos de campeonato
az servicebus topic create \
  --resource-group rg-sre-observability \
  --namespace-name sb-sre-observability-poc \
  --name championship-events

# Criar subscription para events-pusher
az servicebus topic subscription create \
  --resource-group rg-sre-observability \
  --namespace-name sb-sre-observability-poc \
  --topic-name championship-events \
  --name events-pusher-subscription

# Criar subscription para shots-pusher
az servicebus topic subscription create \
  --resource-group rg-sre-observability \
  --namespace-name sb-sre-observability-poc \
  --topic-name championship-events \
  --name shots-pusher-subscription
```

### 3. Configurar Filtros de Subscription

```bash
# Filtro para events-pusher (apenas eventos de skill específicos - ex: 1=GOAL, 2=ASSIST, 3=CARD)
az servicebus topic subscription rule create \
  --resource-group rg-sre-observability \
  --namespace-name sb-sre-observability-poc \
  --topic-name championship-events \
  --subscription-name events-pusher-subscription \
  --name EventsFilter \
  --filter-sql-expression "idSkill IN (1, 2, 3)"

# Filtro para shots-pusher (apenas eventos de chute - ex: 4=SHOT)
az servicebus topic subscription rule create \
  --resource-group rg-sre-observability \
  --namespace-name sb-sre-observability-poc \
  --topic-name championship-events \
  --subscription-name shots-pusher-subscription \
  --name ShotsFilter \
  --filter-sql-expression "idSkill = 4"
```

### 4. Obter Connection String

```bash
# Obter connection string
az servicebus namespace authorization-rule keys list \
  --resource-group rg-sre-observability \
  --namespace-name sb-sre-observability-poc \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  --output tsv
```

## 🔐 Azure Key Vault

### 1. Criar Key Vault

```bash
# Criar Key Vault
az keyvault create \
  --resource-group rg-sre-observability \
  --name kv-sre-observability-poc \
  --location brazilsouth \
  --sku standard
```

### 2. Configurar Secrets

```bash
# Adicionar secrets exemplo
az keyvault secret set \
  --vault-name kv-sre-observability-poc \
  --name "DatabaseConnectionString" \
  --value "your-database-connection-string"

az keyvault secret set \
  --vault-name kv-sre-observability-poc \
  --name "ApiKey" \
  --value "your-api-key"
```

### 3. Configurar Access Policy

```bash
# Obter Object ID do usuário atual
USER_OBJECT_ID=$(az ad signed-in-user show --query id --output tsv)

# Configurar permissões
az keyvault set-policy \
  --name kv-sre-observability-poc \
  --object-id $USER_OBJECT_ID \
  --secret-permissions get list set delete
```

## 🔧 Configuração da Aplicação

### 1. Variáveis de Ambiente

Crie um arquivo `.env` na raiz do projeto com as seguintes variáveis:

```bash
# Azure Service Bus
AZURE_SERVICE_BUS_CONNECTION_STRING="Endpoint=sb://sb-sre-observability-poc.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_ACCESS_KEY"

# Azure Key Vault
AZURE_KEY_VAULT_URL="https://kv-sre-observability-poc.vault.azure.net/"

# Opcional: Para autenticação via Service Principal
# AZURE_CLIENT_ID="your-client-id"
# AZURE_CLIENT_SECRET="your-client-secret"
# AZURE_TENANT_ID="your-tenant-id"
```

### 2. Configuração no docker-compose.yml

O docker-compose.yml já está configurado para usar essas variáveis de ambiente:

```yaml
environment:
  - AZURE_SERVICE_BUS__CONNECTION_STRING=${AZURE_SERVICE_BUS_CONNECTION_STRING}
  - AZURE_KEY_VAULT__VAULT_URL=${AZURE_KEY_VAULT_URL}
```

## 📊 Service Bus Message Properties

As mensagens enviadas pelo collector incluem as seguintes propriedades para filtros:

- `idChampionship`: ID do campeonato
- `idMatch`: ID da partida
- `idSkill`: ID da habilidade/evento
- `timestamp`: Timestamp do evento
- `eventType`: Tipo do evento (sempre "ChampionshipData")
- `source`: Origem da mensagem (sempre "collector-service")
- `version`: Versão do schema da mensagem

## 🧪 Testando a Configuração

### 1. Testar Service Bus

```bash
# Enviar dados de teste para o collector
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 101,
    "idSkill": 1,
    "timestamp": "2024-01-15T10:30:00Z"
  }'
```

### 2. Verificar Mensagens no Portal Azure

1. Acesse o portal Azure
2. Navegue até o Service Bus namespace
3. Verifique as métricas do topic `championship-events`
4. Verifique as subscriptions para ver se as mensagens estão sendo filtradas corretamente

## 🔒 Segurança

### Managed Identity (Recomendado para Produção)

Para produção, configure Managed Identity em vez de connection strings:

```bash
# Habilitar Managed Identity
az webapp identity assign --resource-group rg-sre-observability --name your-app-name

# Configurar permissões no Service Bus
az role assignment create \
  --role "Azure Service Bus Data Sender" \
  --assignee-object-id YOUR_MANAGED_IDENTITY_OBJECT_ID \
  --scope /subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/rg-sre-observability/providers/Microsoft.ServiceBus/namespaces/sb-sre-observability-poc
```

## 📝 Monitoramento

Configure alertas para:

- Mensagens mortas (Dead Letter Queue)
- Latência de processamento
- Falhas de autenticação
- Throttling

## 🔄 Limpeza de Recursos

```bash
# Remover todos os recursos
az group delete --name rg-sre-observability --yes --no-wait
``` 