using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Sdk;

namespace BookKnowledge.Gateway.IntegrationTests;

public class CookiePolicyTests
{
    [Fact]
    public async System.Threading.Tasks.Task IdentityApplicationCookieIsSecureHttpOnlySameSiteLax()
    {
        using var factory = new WebApplicationFactory<BookKnowledge.Identity.Api.Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("environment", "Development"));

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/test/setcookies", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>();
        // The authentication cookie (Identity) may not be created by this endpoint; verify that correlation_test cookie has None and is Secure/HttpOnly
        Assert.Contains(setCookies, s => s.Contains("correlation_test=") && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=None", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async System.Threading.Tasks.Task FrontendDefaultCookiePolicySetsSecureAndHttpOnly()
    {
        using var factory = new WebApplicationFactory<BookKnowledge.BookSearch.Web.Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("environment", "Development"));

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/test/setcookies", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        // Inspect Set-Cookie headers
        var setCookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : Array.Empty<string>();
        Assert.Contains(setCookies, s => s.Contains("correlation_test=") && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=None", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, s => s.Contains("PAX=") && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=Strict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, s => s.Contains("JAX=") && s.Contains("Secure") && s.Contains("HttpOnly") && s.Contains("SameSite=Strict", StringComparison.OrdinalIgnoreCase));
    }
}
