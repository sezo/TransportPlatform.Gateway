using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TransportPlatform.Gateway.Public.Auth;

/// <summary>
/// Dynamically generates ASP.NET authorization policies from permission strings.
///
/// Usage in controllers:
///   [Authorize(Policy = "permission:ticket:read")]
///   [Authorize(Policy = "permission:fleet:manage|fleet:write")]  ← OR logic
///
/// No manual policy registration needed.
/// Add claim in Keycloak → use in attribute. Zero code changes.
///
/// Pipe separator = OR logic (user needs ANY one of the listed permissions)
/// Multiple [Authorize] attributes = AND logic (user needs ALL)
/// </summary>
public class PermissionPolicyProvider(
    IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private const string PermissionPrefix = "permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPrefix))
            return await _fallback.GetPolicyAsync(policyName);

        var permissionPart = policyName[PermissionPrefix.Length..];
        var permissions = permissionPart.Split('|', StringSplitOptions.RemoveEmptyEntries);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser();

        if (permissions.Length == 1)
        {
            // Single permission — simple claim check
            policy.RequireClaim("permission", permissions[0].Trim());
        }
        else
        {
            // Multiple permissions — OR logic
            // User needs at least one of the listed permissions
            var permissionList = permissions.Select(p => p.Trim()).ToArray();
            policy.RequireAssertion(ctx =>
                permissionList.Any(p =>
                    ctx.User.HasClaim("permission", p)));
        }

        return policy.Build();
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
