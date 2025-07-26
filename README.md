# Azure SRE Observability PoC

Este repositório contém uma **Prova de Conceito (PoC)** para SRE e Observabilidade no Azure, utilizando Docker Compose para orquestrar uma stack completa de aplicações .NET 8 com monitoramento e observabilidade.

## 🏗️ Arquitetura

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Admin Panel   │    │    Collector    │    │Controller Manager│
│ (ASP.NET MVC 8) │    │ (ASP.NET MVC 8) │    │   (.NET 8)       │
│   Port: 5001    │    │   Port: 5002    │    │                  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
         ┌───────────────────────┼───────────────────────┐
         │                       │                       │
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Events Pusher  │    │  Shots Pusher   │    │ Azure Service   │
│   (.NET 8)      │    │   (.NET 8)      │    │ Bus Emulator    │
│                 │    │                 │    │  Port: 5671     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
    ┌────────────────────────────┼────────────────────────────┐
    │              OBSERVABILITY STACK                        │
    │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐        │
    │  │ Prometheus  │ │   Grafana   │ │   Jaeger    │        │
    │  │ Port: 9090  │ │ Port: 3000  │ │ Port: 16686 │        │
    │  └─────────────┘ └─────────────┘ └─────────────┘        │
    │  ┌─────────────┐ ┌─────────────┐                        │
    │  │    Loki     │ │  Promtail   │                        │
    │  │ Port: 3100  │ │             │                        │
    │  └─────────────┘ └─────────────┘                        │
    └────────────────────────────────────────────────────────┘
```

## 🚀 Setup Rápido

### 1. Clone o Repositório

```bash
git clone https://github.com/seu-usuario/azure-sre-observability-poc.git
cd azure-sre-observability-poc
```

### 2. Configure os Serviços Azure

⚠️ **IMPORTANTE**: Esta solução utiliza serviços Azure reais (Service Bus e Key Vault).

Siga o guia completo em [AZURE_SETUP.md](AZURE_SETUP.md) para configurar:
- Azure Service Bus com topics e subscriptions
- Azure Key Vault
- Filtros de mensagem por `idChampionship`, `idMatch` e `idSkill`

### 3. Configure as Variáveis de Ambiente

Copie o arquivo de exemplo e configure suas credenciais:

```bash
# Copiar arquivo de exemplo
cp .env.example .env

# Editar com suas credenciais reais
nano .env
```

Configure as variáveis no arquivo `.env`:

```bash
# Azure Service Bus
AZURE_SERVICE_BUS_CONNECTION_STRING="Endpoint=sb://seu-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SUA_CHAVE"

# Azure Key Vault
AZURE_KEY_VAULT_URL="https://seu-keyvault.vault.azure.net/"
```

#### 🔧 Configuração via appsettings.json

Alternativamente, você pode configurar as conexões diretamente nos arquivos `appsettings.json` de cada serviço:

```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://seu-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SUA_CHAVE",
    "TopicName": "championship-events"
  },
  "AzureKeyVault": {
    "VaultUrl": "https://seu-keyvault.vault.azure.net/"
  }
}
```

**Prioridade de Configuração:**
1. `appsettings.json` → `AzureServiceBus:ConnectionString`
2. Variável de ambiente → `AZURE_SERVICE_BUS__CONNECTION_STRING`
3. Variável de ambiente → `AZURE_SERVICE_BUS_CONNECTION_STRING`

#### 🔧 Configuração de Observabilidade

Para desabilitar o Jaeger em desenvolvimento (útil quando Jaeger não está disponível):

```json
{
  "OpenTelemetry": {
    "Jaeger": {
      "Enabled": false
    }
  }
}
```

#### 🛠️ Modo de Desenvolvimento Local

Para desenvolvimento local sem Azure Services, use:

```bash
# Copiar configuração local
cp src/collector/appsettings.Local.json src/collector/appsettings.json

# Ou executar com perfil específico
ASPNETCORE_ENVIRONMENT=Local dotnet run
```

**Recursos em Modo Local:**
- ✅ MockServiceBusService (simula Service Bus)
- ✅ Jaeger desabilitado (evita erros de conexão)
- ✅ Logs detalhados de desenvolvimento

### 4. Inicie a Stack Completa

```bash
docker-compose up --build
```

Este comando irá:
- Buildar todas as imagens Docker das aplicações .NET
- Conectar aos serviços Azure Service Bus e Key Vault online
- Configurar toda a stack de observabilidade (Prometheus, Grafana, Jaeger, Loki)
- Configurar a rede customizada `azure-sre-poc`

### 3. Aguarde a Inicialização

Aguarde alguns minutos para que todos os serviços sejam inicializados completamente.

## 📊 URLs de Acesso

### Aplicações
- **Admin Panel**: http://localhost:5001
  - Swagger UI: http://localhost:5001/swagger
  - Health Check: http://localhost:5001/health
  - Metrics: http://localhost:5001/metrics

- **Collector**: http://localhost:5002
  - Swagger UI: http://localhost:5002/swagger
  - Health Check: http://localhost:5002/health
  - Metrics: http://localhost:5002/metrics

### Observabilidade
- **Grafana**: http://localhost:3001
  - Usuário: `admin`
  - Senha: `admin`

- **Prometheus**: http://localhost:9090
  - Targets: http://localhost:9090/targets
  - Graph: http://localhost:9090/graph

- **Jaeger UI**: http://localhost:16686
  - Tracing distribuído de todas as aplicações

- **Loki**: http://localhost:3100
  - Endpoint para consulta de logs

## 🔍 Testando os Endpoints

### Admin Panel - Onboarding de Clientes

```bash
# Criar novo cliente
curl -X POST http://localhost:5001/api/customers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "João Silva",
    "email": "joao.silva@empresa.com",
    "company": "Empresa XYZ"
  }'

# Verificar métricas
curl http://localhost:5001/metrics
```

### Collector - Coleta de Dados de Campeonato

```bash
# Enviar dados de campeonato para coleta
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 101, 
    "idSkill": 1,
    "timestamp": "2024-01-15T10:30:00Z"
  }'

# Exemplo com evento de chute (skill id diferente)
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{
    "idChampionship": 1,
    "idMatch": 102,
    "idSkill": 2, 
    "timestamp": "2024-01-15T10:35:00Z"
  }'

# Verificar métricas
curl http://localhost:5002/metrics
```

### Exemplos de Métricas

```bash
# Verificar todas as métricas do Admin Panel
curl http://localhost:5001/metrics | grep -E "(http_requests_total|http_request_duration)"

# Verificar todas as métricas do Collector
curl http://localhost:5002/metrics | grep -E "(http_requests_total|http_request_duration)"

# Verificar saúde dos serviços
curl http://localhost:5001/health
curl http://localhost:5002/health
```

## 📈 Configuração do Grafana

### 1. Acesse o Grafana
- URL: http://localhost:3001
- Login: admin / admin

### 2. Datasources Pré-configurados

Os seguintes datasources já estão configurados automaticamente:

#### Prometheus
- **URL**: http://prometheus:9090
- **Type**: Prometheus
- **Default**: Yes

#### Loki
- **URL**: http://loki:3100
- **Type**: Loki

#### Jaeger
- **URL**: http://jaeger:16686
- **Type**: Jaeger

### 3. Dashboards Sugeridos

Importe os seguintes dashboards da comunidade Grafana:

1. **ASP.NET Core Dashboard**
   - ID: 10915
   - Para métricas das aplicações web

2. **Docker Container Dashboard**
   - ID: 193
   - Para métricas dos containers

3. **Loki Dashboard**
   - ID: 13639
   - Para visualização de logs

## 🏭 Configuração de Produção

### Variáveis de Ambiente

Todas as aplicações utilizam as seguintes variáveis de ambiente:

```yaml
# Azure Service Bus (Produção)
AZURE_SERVICE_BUS_CONNECTION_STRING: "Endpoint=sb://seu-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SUA_CHAVE"

# Azure Key Vault (Produção)
AZURE_KEY_VAULT_URL: "https://seu-keyvault.vault.azure.net/"

# Jaeger Tracing
JAEGER_AGENT_HOST: "jaeger"
JAEGER_AGENT_PORT: "14250"
```

### Filtros do Service Bus

As mensagens são filtradas automaticamente usando as propriedades:

- **Events Pusher**: Recebe mensagens com `idSkill IN (1, 2, 3)` (ex: 1=GOAL, 2=ASSIST, 3=CARD)
- **Shots Pusher**: Recebe mensagens com `idSkill = 4` (ex: 4=SHOT)

### 📊 Métricas e Observabilidade

O collector agora inclui métricas detalhadas e tracing distribuído:

#### Métricas Customizadas

- `servicebus_messages_published_total`: Total de mensagens publicadas no Service Bus
- `servicebus_publish_errors_total`: Total de erros ao publicar no Service Bus  
- `servicebus_publish_duration_seconds`: Duração das operações de publicação

#### Tracing Distribuído

- **Trace ID**: Cada request recebe um trace ID único que é propagado através de todo o fluxo
- **Spans**: Operações são instrumentadas com spans do Jaeger para rastreabilidade
- **Tags**: Informações contextuais como `championship.id`, `match.id`, `skill.id`
- **Correlação**: Trace IDs são incluídos nas mensagens do Service Bus para correlação end-to-end

#### Exemplo de Response com Tracing

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Championship data collected and published to Service Bus successfully",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "data": {
    "championship": 1,
    "match": 101,
    "skill": 1,
    "timestamp": "2024-01-15T10:30:00Z",
    "published": "2024-01-15T10:30:01.234Z"
  }
}
```

### Propriedades da Mensagem

Cada mensagem enviada para o Service Bus inclui:

```json
{
  "applicationProperties": {
    "idChampionship": 1,
    "idMatch": 101, 
    "idSkill": 1,
    "timestamp": "2024-01-15T10:30:00.000Z",
    "eventType": "ChampionshipData",
    "source": "collector-service",
    "version": "1.0",
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "parentSpanId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
  }
}
```

### Volumes Persistentes

Os seguintes volumes são configurados para persistência:

- `prometheus_data`: Dados históricos do Prometheus
- `grafana_data`: Dashboards e configurações do Grafana
- `loki_data`: Logs históricos do Loki

## 🧪 Executando Testes

### Testes de Integração

```bash
# Iniciar todos os serviços
docker-compose up --build -d

# Aguardar inicialização
sleep 60

# Testar endpoints de health check
curl -f http://localhost:5001/health
curl -f http://localhost:5002/health

# Verificar logs específicos
docker-compose logs admin-panel
docker-compose logs collector

# Limpar ambiente
docker-compose down --volumes --remove-orphans
```

### Health Checks

```bash
# Verificar saúde de todos os serviços
curl http://localhost:5001/health
curl http://localhost:5002/health
curl http://localhost:9090/-/healthy
curl http://localhost:3001/api/health
```

## 📊 SLIs (Service Level Indicators)

### Aplicações Web (Admin Panel & Collector)

- **Latência**: P95 < 500ms para requests HTTP
- **Throughput**: Requests por segundo
- **Taxa de Erro**: < 1% de requests com status 4xx/5xx
- **Disponibilidade**: > 99.9% uptime

### Aplicações Background (Controller Manager & Pushers)

- **Processamento**: Tempo médio de processamento por item
- **Taxa de Sucesso**: > 99% de processamentos bem-sucedidos
- **Lag de Processamento**: < 30 segundos entre recebimento e processamento

### Métricas Personalizadas

Todas as aplicações expõem métricas customizadas:

- `app_requests_total`: Total de requests processados
- `app_processing_duration_seconds`: Duração do processamento
- `app_errors_total`: Total de erros
- `app_service_bus_messages_total`: Mensagens do Service Bus

## 🔧 Desenvolvimento

### Estrutura do Projeto

```
azure-sre-observability-poc/
├── .github/workflows/
│   └── ci.yml                  # Pipeline CI/CD
├── src/
│   ├── admin-panel/           # ASP.NET MVC Core 8
│   ├── collector/             # ASP.NET MVC Core 8
│   ├── controller-manager/    # .NET 8 Console
│   └── pushers/
│       ├── events/            # .NET 8 Console
│       └── shots/             # .NET 8 Console
├── observability/
│   ├── docker-compose.yml     # Stack de observabilidade
│   ├── prometheus.yml         # Configuração Prometheus
│   ├── loki-config.yml        # Configuração Loki
│   ├── promtail-config.yml    # Configuração Promtail
│   └── grafana/
│       └── provisioning/      # Configurações Grafana
├── docker-compose.yml         # Orquestração principal
└── README.md
```

### Adicionando Novos Serviços

1. Crie a estrutura do projeto em `src/novo-servico/`
2. Adicione o Dockerfile
3. Configure no `docker-compose.yml`
4. Adicione scraping no `observability/prometheus.yml`
5. Configure telemetria (OpenTelemetry + Jaeger)

## 🚀 CI/CD Pipeline

O pipeline GitHub Actions inclui:

1. **Build**: Construção de todas as imagens Docker
2. **Test**: Testes de integração com Docker Compose
3. **Security**: Scan de vulnerabilidades com Trivy
4. **Push**: Deploy para GitHub Container Registry

### Executar Localmente

```bash
# Simular o pipeline de CI
.github/workflows/ci.yml
```

## 📝 Logs e Monitoramento

### Visualizar Logs

```bash
# Logs em tempo real
docker-compose logs -f

# Logs de um serviço específico
docker-compose logs -f admin-panel

# Logs por timestamp
docker-compose logs --since="2024-01-15T10:00:00"
```

### Queries Úteis no Prometheus

```promql
# Taxa de requests por segundo
rate(http_requests_total[5m])

# Latência P95
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Taxa de erro
rate(http_requests_total{status=~"4.."}[5m]) / rate(http_requests_total[5m])
```

### Queries Úteis no Loki

```logql
# Logs de erro por serviço
{container_name="admin-panel"} |= "ERROR"

# Logs por nível
{job="azure-sre-poc"} | json | level="ERROR"

# Rate de logs de erro
rate({container_name="admin-panel"} |= "ERROR"[5m])
```

## 🛠️ Troubleshooting

### Problemas Comuns

#### 1. **Erro: "Failed to configure Jaeger exporter"**

**Solução 1 - Desabilitar Jaeger:**
```json
{
  "OpenTelemetry": {
    "Jaeger": {
      "Enabled": false
    }
  }
}
```

**Solução 2 - Verificar se Jaeger está rodando:**
```bash
# Verificar se container Jaeger está ativo
docker ps | grep jaeger

# Testar conectividade
curl http://localhost:16686
```

#### 2. **Erro: "Failed to connect to Azure Service Bus"**

**Solução 1 - Verificar connection string:**
```bash
# Verificar variáveis de ambiente
echo $AZURE_SERVICE_BUS_CONNECTION_STRING

# Testar conectividade manualmente
az servicebus namespace show --name seu-namespace --resource-group seu-rg
```

**Solução 2 - Usar modo mock para desenvolvimento:**
```bash
# Usar configuração local (mock services)
ASPNETCORE_ENVIRONMENT=Local dotnet run --project src/collector
```

#### 3. **Containers não sobem**:
```bash
docker-compose down --volumes --remove-orphans
docker-compose up --build
```

#### 4. **Prometheus não coleta métricas**:
- Verifique se o endpoint `/metrics` está disponível
- Confirme a configuração de rede entre containers

#### 5. **Grafana não acessa datasources**:
- Verifique se os URLs dos datasources estão corretos
- Confirme que os serviços estão na mesma rede Docker

### Verificar Health Checks

```bash
# Collector
curl http://localhost:5002/health

# Admin Panel  
curl http://localhost:5001/health

# Métricas
curl http://localhost:5002/metrics
```

### Logs de Desenvolvimento

Para logs mais detalhados durante troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Collector.Services": "Trace",
      "OpenTelemetry": "Information"
    }
  }
}
```

### Limpeza Completa

```bash
# Parar todos os containers e remover volumes
docker-compose down --volumes --remove-orphans

# Remover imagens não utilizadas
docker system prune -a --volumes
```

## 🤝 Contribuindo

1. Faça fork do projeto
2. Crie sua feature branch (`git checkout -b feature/nova-funcionalidade`)
3. Commit suas mudanças (`git commit -am 'Adiciona nova funcionalidade'`)
4. Push para a branch (`git push origin feature/nova-funcionalidade`)
5. Abra um Pull Request

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

---

**Desenvolvido com ❤️ para demonstrar SRE e Observabilidade no Azure**