using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// -- YARP ----------------------------------------------------------------------
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        ctx.AddRequestTransform(transform =>
        {
            var user = transform.HttpContext.User;
            if (user.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? user.FindFirstValue("sub");
                var roles = user.Claims
                    .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToArray();

                if (userId is not null)
                    transform.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", userId);

                if (roles.Length > 0)
                    transform.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Roles", string.Join(",", roles));
            }
            return ValueTask.CompletedTask;
        });
    });

// -- JWT validation ------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.TokenValidationParameters.ValidateAudience = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        // Accept tokens issued via any of the Keycloak URLs (internal Docker name or external localhost)
        options.TokenValidationParameters.ValidIssuers =
        [
            builder.Configuration["Keycloak:Authority"]!,
            "http://keycloak:8080/realms/transport",
            "http://localhost:9090/realms/transport",
            "http://host.docker.internal:9090/realms/transport"
        ];
    });

builder.Services.AddAuthorization();

// -- Rate limiting -------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("per-user", opt =>
    {
        opt.Window                = TimeSpan.FromMinutes(1);
        opt.PermitLimit           = 100;
        opt.SegmentsPerWindow     = 6;
        opt.QueueLimit            = 10;
        opt.QueueProcessingOrder  = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

app.UseWebSockets();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

await app.RunAsync();