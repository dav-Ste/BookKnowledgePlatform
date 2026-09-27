using BookKnowledge.ServiceDefaults;
using BookKnowledge.Identity.Infrastructure.Data;
using BookKnowledge.Identity.Infrastructure.Entities;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Configure cookie policy and secure cookie defaults (HTTPS only, HttpOnly, SameSite handling)
builder.Services.Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.OnAppendCookie = cookieContext =>
    {
        cookieContext.CookieOptions.Secure = true;
        cookieContext.CookieOptions.HttpOnly = true;
        var name = cookieContext.CookieName ?? string.Empty;
        if (name.Contains("correlation", StringComparison.OrdinalIgnoreCase))
            cookieContext.CookieOptions.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
        else if (name.Equals("PAX", StringComparison.OrdinalIgnoreCase) || name.Equals("JAX", StringComparison.OrdinalIgnoreCase))
            cookieContext.CookieOptions.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
        else
            cookieContext.CookieOptions.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    };
    options.OnDeleteCookie = cookieContext =>
    {
        cookieContext.CookieOptions.Secure = true;
        cookieContext.CookieOptions.HttpOnly = true;
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Configure database for Identity and OpenIddict
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["ConnectionStrings:DefaultConnection"];
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Register the OpenIddict entity sets in the EF Core model
    options.UseOpenIddict();
});

// Configure application cookie defaults for Identity
builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
    Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme,
    options =>
    {
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    });

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// Configure OpenIddict server
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<IdentityDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");
        options.SetAuthorizationEndpointUris("/connect/authorize");

        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange(); // PKCE
        options.AllowRefreshTokenFlow();

        // Register scopes
        options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.OfflineAccess);

        // During development use ephemeral keys; in production use persisted keys (e.g. Vault or Key Vault)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        // Configure validation to use local OpenIddict server
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var app = builder.Build();
// Run migrations and seed initial data
using (var scope = app.Services.CreateScope())
{
    await BookKnowledge.Identity.Infrastructure.Data.IdentityDataSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// Test-only endpoint to set cookies for integration tests (only available in Development)
if (app.Environment.IsDevelopment())
{
    app.MapGet("/test/setcookies", (HttpContext http) =>
    {
        // Append a correlation cookie (should be SameSite=None per cookie policy)
        http.Response.Cookies.Append("correlation_test", "v1", new Microsoft.AspNetCore.Http.CookieOptions
        {
            Path = "/",
        });

        // Append PAX and JAX cookies (sensitive)
        http.Response.Cookies.Append("PAX", "pax", new Microsoft.AspNetCore.Http.CookieOptions
        {
            Path = "/",
        });

        http.Response.Cookies.Append("JAX", "jax", new Microsoft.AspNetCore.Http.CookieOptions
        {
            Path = "/",
        });

        return Results.Ok(new { ok = true });
    });
}

app.MapGet("/healthz", () => Results.Ok(new { service = "Identity", status = "ok" }))
   .RequireRateLimiting("api");

app.MapGet("/api/identity/ping", () => Results.Ok(new { service = "identity" })).RequireRateLimiting("api");

app.Run();
public partial class Program { }
