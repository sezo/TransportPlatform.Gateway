using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using TransportPlatform.Gateway.Public.Auth;
using TransportPlatform.Gateway.Public.Transforms;

var builder = WebApplication.CreateBuilder(args);

// ── YARP ──────────────────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<UserContextTransform>();

// ── JWT validation ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

// ── Authorization — permission-based, dynamically generated ──────────────────
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddAuthorization();

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("per-user", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.SegmentsPerWindow = 6;
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddSlidingWindowLimiter("ticket-purchase", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.SegmentsPerWindow = 6;
        opt.QueueLimit = 2;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── M2M token provider ────────────────────────────────────────────────────────
// NOTE: Demo stub — replace with KeycloakM2MTokenProvider for production
// See Auth/M2MTokenProvider.cs for full implementation notes
builder.Services.AddSingleton<IM2MTokenProvider, StubM2MTokenProvider>();

// ── Redis cache (used by production M2M token provider) ───────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);

// ── HTTP client for Keycloak token endpoint ───────────────────────────────────
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

await app.RunAsync();
