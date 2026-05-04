using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;
using System.Security.Claims;
using TransportPlatform.Gateway.Public.Auth;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace TransportPlatform.Gateway.Public.Transforms;

/// <summary>
/// Runs on every proxied request.
///
/// Responsibilities:
/// 1. Strip incoming X-User-* headers (prevent header spoofing from clients)
/// 2. Extract user claims from validated JWT
/// 3. Replace user JWT with M2M token (gateway service account)
/// 4. Forward user context as trusted internal headers
/// 5. Inject correlation ID for distributed tracing
/// </summary>
public class UserContextTransform(IM2MTokenProvider m2mTokenProvider)
    : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }
    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        // Strip any incoming user context headers
        // Prevents clients from injecting fake user identity
        context.AddRequestHeaderRemove("X-User-Id");
        context.AddRequestHeaderRemove("X-User-Email");
        context.AddRequestHeaderRemove("X-User-Roles");
        context.AddRequestHeaderRemove("X-User-Permissions");
        context.AddRequestHeaderRemove("X-Correlation-Id");

        context.AddRequestTransform(async transformContext =>
        {
            var httpContext = transformContext.HttpContext;
            var user = httpContext.User;

            // ── Correlation ID ────────────────────────────────────────────
            var correlationId = httpContext.Request.Headers["X-Correlation-Id"]
                .FirstOrDefault() ?? Guid.NewGuid().ToString();

            transformContext.ProxyRequest.Headers
                .TryAddWithoutValidation("X-Correlation-Id", correlationId);

            httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

            if (!user.Identity?.IsAuthenticated ?? true)
                return;

            // ── Extract user claims from JWT ──────────────────────────────
            var userId = user.FindFirst("sub")?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Keycloak puts roles in realm_access.roles
            var roles = user.FindAll("roles")
                .Concat(user.FindAll(ClaimTypes.Role))
                .Select(c => c.Value)
                .Distinct()
                .ToArray();

            var permissions = user.FindAll("permission")
                .Select(c => c.Value)
                .ToArray();

            // ── Forward user context as trusted headers ────────────────────
            if (userId is not null)
            {
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Id", userId);
            }

            if (roles.Length > 0)
            {
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Roles",
                        string.Join(",", roles));
            }

            if (permissions.Length > 0)
            {
                transformContext.ProxyRequest.Headers
                    .TryAddWithoutValidation("X-User-Permissions",
                        string.Join(",", permissions));
            }

            // ── Replace user JWT with M2M token ───────────────────────────
            // Services validate this token to confirm caller is trusted gateway
            // NOTE: Demo uses stub — see M2MTokenProvider.cs
            var m2mToken = await m2mTokenProvider.GetTokenAsync();
            transformContext.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", m2mToken);
        });
    }
}
