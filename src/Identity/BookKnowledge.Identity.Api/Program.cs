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

app.MapDefaultEndpoints();

app.MapGet("/healthz", () => Results.Ok(new { service = "Identity", status = "ok" }))
   .RequireRateLimiting("api");

app.MapGet("/api/identity/ping", () => Results.Ok(new { service = "identity" })).RequireRateLimiting("api");

app.Run();
public partial class Program { }
