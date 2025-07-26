# Changelog - Azure SRE Observability PoC

## [v2.1.1] - 2024-01-26

### 🐛 **Correções e Melhorias de Robustez**

#### Tratamento de Erros OpenTelemetry/Jaeger
- Configuração do Jaeger agora é opcional e não falha na inicialização
- Adicionada flag `OpenTelemetry:Jaeger:Enabled` para controlar o Jaeger
- Try/catch para evitar falhas quando Jaeger não está disponível
- Parsing mais robusto de portas de configuração

#### Fallback para Mock Services
- `MockServiceBusService` implementado para desenvolvimento local
- Registro de serviços com fallback automático quando Azure Service Bus não está disponível
- Logs informativos quando usando serviços mock

#### Configuração Local
- `appsettings.Local.json` para desenvolvimento sem Azure Services
- Jaeger desabilitado por padrão no modo local
- MockServiceBusService ativado automaticamente

#### Troubleshooting Aprimorado
- Seção expandida de troubleshooting no README
- Soluções específicas para erros do Jaeger e Service Bus
- Comandos para verificar health checks e conectividade
- Configuração de logs detalhados para debugging

---

## [v2.1.0] - 2024-01-26

### ✅ **Configuração via appsettings.json**

#### Arquivos de Configuração Adicionados
- `appsettings.json` e `appsettings.Development.json` para todos os serviços
- Configuração estruturada para Azure Service Bus, Key Vault e OpenTelemetry
- Suporte a variáveis de ambiente nos arquivos de desenvolvimento

#### Estrutura de Configuração
```json
{
  "AzureServiceBus": {
    "ConnectionString": "",
    "TopicName": "championship-events"
  },
  "AzureKeyVault": {
    "VaultUrl": ""
  },
  "OpenTelemetry": {
    "Jaeger": {
      "AgentHost": "localhost",
      "AgentPort": 14250
    },
    "ServiceName": "service-name",
    "ServiceVersion": "1.0.0"
  }
}
```

#### Múltiplas Fontes de Configuração
- **Prioridade 1**: `appsettings.json` → `AzureServiceBus:ConnectionString`
- **Prioridade 2**: Variável de ambiente → `AZURE_SERVICE_BUS__CONNECTION_STRING` 
- **Prioridade 3**: Variável de ambiente → `AZURE_SERVICE_BUS_CONNECTION_STRING`

#### ServiceBusService Atualizado
- Suporte a múltiplas fontes de configuração
- Expansão automática de variáveis de ambiente
- Mensagens de erro mais detalhadas

---

## [v2.0.0] - 2024-01-26

### 🔄 **BREAKING CHANGES**

#### Tipos de Dados Alterados
- **IdChampionship**: `string` → `int`
- **IdMatch**: `string` → `int` 
- **IdSkill**: `string` → `int`

#### Validação Atualizada
- Valores devem ser inteiros positivos (> 0)
- Mensagens de erro mais específicas para validação

### ✅ **Novas Funcionalidades**

#### 📊 Métricas Customizadas
- `servicebus_messages_published_total`: Contador de mensagens publicadas
- `servicebus_publish_errors_total`: Contador de erros de publicação
- `servicebus_publish_duration_seconds`: Histograma de duração das operações

#### 🔍 Tracing Distribuído Completo
- **Trace ID Propagação**: Cada request gera um trace ID único
- **Spans Customizados**: Instrumentação com OpenTelemetry/Jaeger
- **Tags Contextuais**: `championship.id`, `match.id`, `skill.id`, `operation.name`
- **Correlação End-to-End**: Trace IDs incluídos nas mensagens do Service Bus
- **Error Tracking**: Status de erro automático nos spans

#### 🚌 Propriedades de Mensagem Service Bus
- `traceId`: Para correlação distribuída
- `parentSpanId`: Para hierarquia de spans
- Todas as propriedades com tipos corretos (int)

### 🏗️ **Melhorias de Arquitetura**

#### ServiceBusService Aprimorado
- Instrumentação completa com métricas e tracing
- Tratamento robusto de erros com logging estruturado
- Medição de performance com `Stopwatch`
- Tags de status nos activities (Ok/Error)

#### Logging Estruturado
- Trace IDs em todos os logs
- Request IDs únicos para debugging
- Duração de operações nos logs
- Informações contextuais (championship, match, skill)

#### Validação Robusta
- Validação de inteiros positivos
- Responses com trace/request IDs para debugging
- Status codes HTTP apropriados

### 🔧 **Configuração**

#### Remoção de Emuladores
- ❌ Removido Azure Service Bus Emulator
- ❌ Removido SQL Edge dependency
- ✅ Configuração para serviços Azure reais

#### OpenTelemetry Configurado
- Source customizado: `Collector.ServiceBus`
- Meter customizado: `Collector.ServiceBus` 
- Instrumentação HTTP enriquecida
- Exportadores Jaeger e Prometheus

### 📚 **Documentação Atualizada**

#### README.md
- Exemplos com tipos `int`
- Seção de métricas e observabilidade
- Exemplo de response com tracing
- Filtros atualizados do Service Bus

#### AZURE_SETUP.md
- Filtros com valores `int`: `idSkill IN (1, 2, 3)`
- Exemplos de teste atualizados
- Comandos de setup para Azure

### 🧪 **Exemplos de Uso**

#### Request Payload
```json
{
  "idChampionship": 1,
  "idMatch": 101,
  "idSkill": 1,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

#### Response com Tracing
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

#### Service Bus Message Properties
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

### 🚀 **Performance & Observabilidade**

- ⚡ Medição de latência com `Stopwatch`
- 📈 Métricas Prometheus para dashboards
- 🔍 Spans Jaeger para debugging
- 📝 Logs estruturados com contexto
- 🔗 Correlação completa request → Service Bus → consumers

### 🔒 **Migração**

#### Breaking Changes
1. **Payload**: Altere todos os IDs de `string` para `int`
2. **Filtros**: Atualize filtros do Service Bus para usar valores numéricos
3. **Consumers**: Atualize aplicações que consomem as mensagens

#### Compatibilidade
- Docker Compose v2 (plugin) suportado
- .NET 8 mantido
- Azure Service Bus SDK atualizado com `Azure.Identity`

---

**Nota**: Esta versão requer configuração de serviços Azure reais. Consulte `AZURE_SETUP.md` para instruções detalhadas. 