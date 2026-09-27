using BookKnowledge.ServiceDefaults;
using BookKnowledge.BookSearch.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder=WebApplication.CreateBuilder(args);
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

// Configure authentication for the Blazor client using OpenID Connect (Authorization Code + PKCE)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddCookie()
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Identity:Authority"] ?? "https://localhost:52194";
        options.ClientId = "booksearch.client";
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");
        options.GetClaimsFromUserInfoEndpoint = true;
        options.CallbackPath = "/authentication/login-callback";
        options.SignedOutCallbackPath = "/authentication/logout-callback";
    });

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app=builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapDefaultEndpoints();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
// Development-only endpoint to assist integration tests
if (app.Environment.IsDevelopment())
{
    app.MapGet("/test/setcookies", (HttpContext http) =>
    {
        http.Response.Cookies.Append("correlation_test", "v1", new Microsoft.AspNetCore.Http.CookieOptions { Path = "/" });
        http.Response.Cookies.Append("PAX", "pax", new Microsoft.AspNetCore.Http.CookieOptions { Path = "/" });
        http.Response.Cookies.Append("JAX", "jax", new Microsoft.AspNetCore.Http.CookieOptions { Path = "/" });
        return Results.Ok();
    });
}
app.Run(); public partial class Program { }
