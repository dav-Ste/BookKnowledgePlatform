using BookKnowledge.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using System.Net.Http.Headers;
using System.Diagnostics.Metrics;
using System.Diagnostics;


var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// If AZURE_KEY_VAULT_ENDPOINT is set, configure Azure Key Vault as a configuration source
var keyVaultEndpoint = builder.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
if (!string.IsNullOrEmpty(keyVaultEndpoint))
{
    try
    {
        var credential = new Azure.Identity.DefaultAzureCredential();
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultEndpoint), credential);
    }
    catch (Exception ex)
    {
        // Fail fast in development is okay; in production you may prefer to log and continue
        Console.WriteLine($"Warning: failed to configure Azure Key Vault: {ex.Message}");
    }
}

// Tune default logging filters to reduce noise from framework logs and keep app logs at Information
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("BookKnowledge", LogLevel.Information);

// Instrumentation note: this gateway uses ActivitySource and System.Diagnostics.Metrics (Meter)
// for lightweight in-process telemetry. Configure OpenTelemetry exporters (OTLP/Prometheus)
// in the host environment or by adding the OpenTelemetry provider packages and wiring
// them in Startup if you need centralized collection.

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Identity authority
var identityAuthority = builder.Configuration["Identity:Authority"] ?? "https://localhost:52194";

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GatewayPolicy", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("books.read", policy => policy.RequireClaim("scope", "books.read"));
    options.AddPolicy("content.read", policy => policy.RequireClaim("scope", "content.read"));
});

// Configure YARP
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configure gateway as an OIDC client (server-side) so it can sign-in users and obtain access tokens
var gatewayClientSecret = builder.Configuration["Authentication:Gateway:ClientSecret"] ?? "gateway-secret";

// Register a secret provider so the gateway can refresh the client secret at runtime
builder.Services.AddSingleton<BookKnowledge.Gateway.Services.ISecretProvider>(sp =>
{
    var configSecret = builder.Configuration["Authentication:Gateway:ClientSecret"] ?? builder.Configuration["GATEWAY_CLIENT_SECRET"];
    return new BookKnowledge.Gateway.Services.InMemorySecretProvider(configSecret ?? "gateway-secret");
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = identityAuthority;
        options.ClientId = "gateway.client";
        // The client secret will be provided at runtime via DI. Set a placeholder here.
        options.ClientSecret = "<deferred-by-secret-provider>";
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");
        options.RequireHttpsMetadata = true;

        // Provide events to ensure the handler uses the latest secret when exchanging tokens
        options.Events ??= new OpenIdConnectEvents();
        options.Events.OnRedirectToIdentityProviderForSignOut = context => Task.CompletedTask;
    });

// Also allow bearer token validation for API-to-gateway calls
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = identityAuthority;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters { ValidateAudience = false };
    });

// Add an HttpClient for token refresh calls
builder.Services.AddHttpClient("tokenClient");

var app = builder.Build();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseHttpsRedirection();

// Metrics instruments used by the token refresh middleware
// Local instrumentation sources (ActivitySource + Meter) for token refresh metrics
var _activitySource = new ActivitySource("BookKnowledge.Gateway.TokenRefresh");
var _refreshMeter = new Meter("BookKnowledge.Gateway.TokenRefresh", "1.0");
var _refreshAttempts = _refreshMeter.CreateCounter<long>("gateway.token_refresh.attempts");
var _refreshSuccesses = _refreshMeter.CreateCounter<long>("gateway.token_refresh.successes");
var _refreshFailures = _refreshMeter.CreateCounter<long>("gateway.token_refresh.failures");
var _refreshLatency = _refreshMeter.CreateHistogram<long>("gateway.token_refresh.latency_ms");

app.MapDefaultEndpoints();
app.MapGet("/", () => Results.Ok(new { service = "gateway", status = "ok" }));

// Middleware to add X-Forwarded-User header from authenticated principal (so downstreams can use it)
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var sub = context.User.FindFirst("sub")?.Value ?? context.User.Identity.Name;
        if (!string.IsNullOrEmpty(sub))
        {
            context.Request.Headers["X-Forwarded-User"] = sub;
        }
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Token propagation middleware: attach access token from cookie store to outgoing proxied requests, refresh if needed
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Gateway.TokenMiddleware");
    var secretProvider = context.RequestServices.GetService<BookKnowledge.Gateway.Services.ISecretProvider>();

    // If there is already an Authorization header, leave it
    if (!context.Request.Headers.ContainsKey("Authorization") && context.User?.Identity?.IsAuthenticated == true)
    {
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (result?.Succeeded == true && result.Properties != null && result.Principal != null)
        {
            var props = result.Properties;
            string? accessToken = props.GetTokenValue("access_token");
            string? refreshToken = props.GetTokenValue("refresh_token");
            string? expiresAt = props.GetTokenValue("expires_at");

            DateTimeOffset expires;
            if (!string.IsNullOrEmpty(expiresAt) && DateTimeOffset.TryParse(expiresAt, out expires))
            {
                // If token is expiring in next minute, attempt refresh
                if (DateTimeOffset.UtcNow >= expires.Subtract(TimeSpan.FromSeconds(60)) && !string.IsNullOrEmpty(refreshToken))
                {
                    // Record an attempt
                    try
                    {
                        _refreshAttempts.Add(1);
                    }
                    catch { }

                    var sw = Stopwatch.StartNew();
                    using var activity = _activitySource.StartActivity("token_refresh_attempt", ActivityKind.Internal);
                    try
                    {
                        activity?.SetTag("user", result.Principal?.Identity?.Name ?? "unknown");
                        var clientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
                        var http = clientFactory.CreateClient("tokenClient");
                        var tokenEndpoint = identityAuthority.TrimEnd('/') + "/connect/token";
                        string? currentSecret = null;
                        if (secretProvider != null)
                        {
                            currentSecret = await secretProvider.GetSecretAsync();
                        }
                        var secretForRequest = !string.IsNullOrEmpty(currentSecret) ? currentSecret : gatewayClientSecret;

                        var body = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
                        {
                            new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "refresh_token"),
                            new System.Collections.Generic.KeyValuePair<string, string>("refresh_token", refreshToken),
                            new System.Collections.Generic.KeyValuePair<string, string>("client_id", "gateway.client"),
                            new System.Collections.Generic.KeyValuePair<string, string>("client_secret", secretForRequest ?? gatewayClientSecret)
                        };
                        var resp = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(body));
                        sw.Stop();
                        try { _refreshLatency.Record(sw.ElapsedMilliseconds); } catch { }

                        if (resp.IsSuccessStatusCode)
                        {
                            try { _refreshSuccesses.Add(1); } catch { }

                            var json = await resp.Content.ReadAsStringAsync();
                            var jobj = System.Text.Json.JsonDocument.Parse(json).RootElement;
                            var newAccess = jobj.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                            var newRefresh = jobj.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken;
                            var expiresIn = jobj.TryGetProperty("expires_in", out var ei) && ei.TryGetInt64(out var seconds) ? seconds : (long?)null;

                            if (!string.IsNullOrEmpty(newAccess))
                            {
                                var tokens = new System.Collections.Generic.List<AuthenticationToken>
                                {
                                    new AuthenticationToken { Name = "access_token", Value = newAccess! },
                                    new AuthenticationToken { Name = "refresh_token", Value = newRefresh ?? string.Empty }
                                };

                                if (expiresIn.HasValue)
                                {
                                    var newExpires = DateTimeOffset.UtcNow.AddSeconds(expiresIn.Value).ToString("o");
                                    tokens.Add(new AuthenticationToken { Name = "expires_at", Value = newExpires });
                                }

                                props.StoreTokens(tokens);
                                // Persist updated cookie with refreshed tokens
                                // result.Principal is non-null due to the guard above; assign to local var for nullability
                                var principal = result.Principal!;
                                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
                                accessToken = newAccess;
                                logger?.LogInformation("Refreshed access token for user {User}", principal.Identity?.Name);
                                activity?.SetTag("token.refresh", "success");
                            }
                            else
                            {
                                logger?.LogWarning("Refresh response did not include an access_token");
                                activity?.SetTag("token.refresh", "no_access_token");
                            }
                        }
                        else
                        {
                            try { _refreshFailures.Add(1); } catch { }
                            var bodyText = await resp.Content.ReadAsStringAsync();
                            logger?.LogWarning("Token refresh failed: {Status} {Body}", resp.StatusCode, bodyText);
                            activity?.SetTag("token.refresh", "failed");
                            activity?.SetTag("token.refresh.status_code", (int)resp.StatusCode);
                            // If refresh failed, sign the user out locally and challenge to re-authenticate
                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                            return; // short-circuit the pipeline
                        }
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        try { _refreshFailures.Add(1); } catch { }
                        logger?.LogError(ex, "Exception while refreshing access token");
                        activity?.SetTag("token.refresh", "error");
                        activity?.SetTag("token.refresh.error", ex.Message);
                        // On unexpected error, sign the user out to force re-authentication
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Request.Headers["Authorization"] = $"Bearer {accessToken}";
            }
        }
    }

    await next();
});

// Logout propagation endpoint: signs out locally and triggers OIDC sign-out at the identity provider
app.MapGet("/account/logout", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Gateway.Logout");
    logger?.LogInformation("Initiating sign-out for {user}", context.User?.Identity?.Name ?? "anonymous");

    // Sign out of the local cookie and trigger the OpenID Connect sign-out flow which will redirect to the identity provider.
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
    {
        RedirectUri = "/"
    });
});

// Background task: periodically refresh secret from Key Vault (if configured)
var secretProviderInstance = app.Services.GetService<BookKnowledge.Gateway.Services.ISecretProvider>();
var keyVaultEndpointEnv = app.Configuration["AZURE_KEY_VAULT_ENDPOINT"];
if (!string.IsNullOrEmpty(keyVaultEndpointEnv) && secretProviderInstance != null)
{
    // Run a periodic refresh in the background to pull the latest secret (best-effort)
    _ = Task.Run(async () =>
    {
        var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger("Gateway.SecretRefresh");
        var httpClientFactory = app.Services.GetService<IHttpClientFactory>();
        var credential = new Azure.Identity.DefaultAzureCredential();
        var client = new Azure.Security.KeyVault.Secrets.SecretClient(new Uri(keyVaultEndpointEnv), credential);
        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                // Try to fetch a well-known secret name; this assumes the secret name is "gateway-client-secret"
                var secretName = app.Configuration["Authentication:Gateway:SecretName"] ?? "gateway-client-secret";
                var secret = await client.GetSecretAsync(secretName);
                if (secret?.Value?.Value != null)
                {
                    secretProviderInstance.SetSecret(secret.Value.Value);
                    logger?.LogInformation("Updated gateway client secret from Key Vault (version: {ver})", secret.Value.Properties.Version);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to refresh gateway secret from Key Vault");
            }

            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    });
}

// Require authentication for proxied routes by default
app.MapReverseProxy().RequireAuthorization("GatewayPolicy");

app.Run();

public partial class Program { }
