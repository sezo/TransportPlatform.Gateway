# TransportPlatform.Gateway — Claude context

## Purpose
Two YARP reverse proxy gateways — public and internal.
No business logic lives here. Gateway concerns only:
- Routing requests to correct downstream service
- JWT validation (user tokens from Keycloak)
- User context header injection (X-User-Id, X-User-Roles, X-User-Permissions)
- M2M token swap (user JWT → gateway service account token)
- Rate limiting (public gateway only)
- Permission-based authorization policies

## Two gateways — why separate projects
Public and internal gateways have different:
- Authorization policies (user-facing vs device/inspector)
- Rate limiting (public only)
- Exposed endpoints (public: tickets, reports, invoices — internal: GPS, validate, manifest)
- Network access (public: internet — internal: VPN only)

If public gateway is compromised, internal endpoints are unreachable.
One process per gateway = process-level isolation, not just config isolation.

## Auth pattern
1. Client sends user JWT
2. Gateway validates JWT against Keycloak
3. Gateway extracts claims (sub, email, roles, permissions)
4. Gateway replaces user JWT with M2M token (gateway service account)
5. Gateway forwards X-User-Id, X-User-Roles, X-User-Permissions headers
6. Downstream services read headers — never validate user JWT directly

## M2M token (demo vs production)
Current: stub returns hardcoded dev token
Production: Keycloak client_credentials flow, cached in Redis
See: src/TransportPlatform.Gateway.Public/Auth/M2MTokenProvider.cs

## Permission policy provider
Dynamically generates ASP.NET authorization policies from permission strings.
"permission:fleet:read" → RequireClaim("permission", "fleet:read")
"permission:fleet:manage|fleet:write" → OR logic, user needs either claim
No manual policy registration needed — add claim in Keycloak, use in attribute.

## Routing
All routes defined in appsettings.json ReverseProxy section.
No routing logic in C# code — ops team can read and understand routes.
Transform logic (header injection, token swap) in C# — needs code.

## What NOT to do
- Never add business logic to gateway
- Never call a database from gateway
- Never share auth logic between public and internal (they evolve independently)
- Never expose internal gateway endpoints to public internet
- Never forward user JWT to downstream services
