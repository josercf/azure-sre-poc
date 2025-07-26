# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Build and Run
```bash
# Start the complete stack with observability
docker-compose up --build

# Start in detached mode
docker-compose up --build -d

# Stop all services
docker-compose down

# Clean up with volumes
docker-compose down --volumes --remove-orphans
```

### Individual Service Development
```bash
# Run specific .NET service locally
dotnet run --project src/admin-panel
dotnet run --project src/collector
dotnet run --project src/controller-manager

# Build specific Docker image
docker build -t admin-panel ./src/admin-panel
docker build -t collector ./src/collector
```

### Testing and Health Checks
```bash
# Health check endpoints
curl http://localhost:5001/health  # Admin Panel
curl http://localhost:5002/health  # Collector

# Metrics endpoints
curl http://localhost:5001/metrics  # Admin Panel
curl http://localhost:5002/metrics  # Collector

# Test API endpoints
curl -X POST http://localhost:5002/api/collect \
  -H "Content-Type: application/json" \
  -d '{"idChampionship": 1, "idMatch": 101, "idSkill": 1, "timestamp": "2024-01-15T10:30:00Z"}'
```

## Architecture Overview

This is an Azure SRE observability PoC with 5 microservices and a complete observability stack:

### Applications (.NET 8)
- **admin-panel** (ASP.NET MVC, port 5001): Customer onboarding service
- **collector** (ASP.NET MVC, port 5002): Championship data collection service  
- **controller-manager** (.NET Console): Background controller service
- **events-pusher** (.NET Console): Processes events (skill IDs 1-3)
- **shots-pusher** (.NET Console): Processes shots (skill ID 4)

### Observability Stack
- **Prometheus** (port 9090): Metrics collection
- **Grafana** (port 3001): Metrics visualization (admin/admin)
- **Jaeger** (port 16686): Distributed tracing
- **Loki** (port 3100): Log aggregation
- **Promtail**: Log collection agent

### Message Flow
1. Collector receives championship data via REST API
2. Data is published to Azure Service Bus topic "championship-events"
3. Events-pusher subscribes to filtered messages (skill IDs 1-3)
4. Shots-pusher subscribes to filtered messages (skill ID 4)
5. All operations are traced end-to-end with OpenTelemetry

## Configuration

### Environment Variables (`.env` file)
```bash
AZURE_SERVICE_BUS_CONNECTION_STRING=Endpoint=sb://...
AZURE_KEY_VAULT_URL=https://your-keyvault.vault.azure.net/
```

### Development Modes
- **Production**: Uses real Azure Service Bus and Key Vault
- **Local/Mock**: Set `ASPNETCORE_ENVIRONMENT=Local` to use mock services
- **Jaeger Disabled**: Set `OpenTelemetry__Jaeger__Enabled=false` in appsettings

## Key Files Structure
```
src/
├── admin-panel/           # Customer onboarding web app
├── collector/             # Championship data collector
├── controller-manager/    # Background controller
└── pushers/
    ├── events/           # Events processor (skills 1-3)
    └── shots/            # Shots processor (skill 4)

observability/
├── docker-compose.yml    # Observability services
├── prometheus.yml        # Prometheus configuration
├── grafana/provisioning/ # Grafana datasources/dashboards
└── *.yml                # Loki/Promtail configs
```

## Azure Services Required
- **Azure Service Bus**: Topic "championship-events" with filtered subscriptions
- **Azure Key Vault**: For secrets management
- Follow `AZURE_SETUP.md` for complete Azure configuration

## Troubleshooting
- Check container logs: `docker-compose logs -f [service-name]`
- Verify Azure connectivity: Test Service Bus connection string
- For local development: Use `appsettings.Local.json` with mock services
- Monitor health: All web services expose `/health` and `/metrics` endpoints