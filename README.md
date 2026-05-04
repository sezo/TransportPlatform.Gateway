# TransportPlatform.Gateway

Dual YARP reverse proxy gateways for the TransportPlatform.

## Prerequisites
- .NET 9 SDK
- Docker Desktop
- Infra stack running (`_transport-platform-meta/infra`)

## Quick start

```bash
# Ensure infra is running
cd ../_transport-platform-meta/infra && docker compose up -d

# Start both gateways
docker compose up -d
```

## Ports
| Gateway | Port | Purpose |
|---|---|---|
| Public | 8080 | Internet-facing — Mobile, Web portals |
| Internal | 8081 | VPN only — Onboard CPU, Inspector, Smart card |

## Running without Docker
```bash
# Public gateway
cd src/TransportPlatform.Gateway.Public
dotnet run

# Internal gateway (separate terminal)
cd src/TransportPlatform.Gateway.Internal
dotnet run --urls "http://localhost:8081"
```

## Testing routes via Postman

### Get a token from Keycloak first
```
POST http://localhost:9090/realms/transport/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password
client_id=b2c-web
username=test@test.com
password=test
```

### Call through public gateway
```
GET http://localhost:8080/api/tickets
Authorization: Bearer {token}
```

### Call through internal gateway (simulating VPN)
```
PUT http://localhost:8081/api/vehicles/{id}/position
Authorization: Bearer {device-token}
```

## Architecture decisions
- ADR 003 — YARP as gateway
- ADR 006 — Keycloak identity

## Running tests
```bash
dotnet test tests/TransportPlatform.Gateway.Tests
```

## M2M token note
Current implementation uses a stub M2M token for demo purposes.
See `src/TransportPlatform.Gateway.Public/Auth/M2MTokenProvider.cs`.
Production implementation requires Keycloak client_credentials flow + Redis cache.
