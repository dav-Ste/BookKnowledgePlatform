using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookKnowledge.Identity.Infrastructure.Data
{
    public static class IdentityDataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");

            try
            {
                var context = services.GetRequiredService<IdentityDbContext>();
                await context.Database.MigrateAsync();

                var userManager = services.GetRequiredService<UserManager<Entities.ApplicationUser>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                // Ensure roles
                string[] roles = new[] { "Admin", "User" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }

                // Ensure OpenIddict client registrations for the Blazor frontends
                var appManager = services.GetService<OpenIddict.Abstractions.IOpenIddictApplicationManager>();
                var env = services.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                var isProduction = string.Equals(env?.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase);
                if (appManager != null)
                {
                    // BookSearch Blazor WebAssembly client
                    var bookClientId = "booksearch.client";
                    if (await appManager.FindByClientIdAsync(bookClientId) == null)
                    {
                        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
                        {
                            ClientId = bookClientId,
                            DisplayName = "BookSearch Blazor Client",
                        };
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Email);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Profile);
                        // Refresh token grant is enabled; offline_access scope is not required as a permission constant here
                        descriptor.RedirectUris.Add(new Uri("https://localhost:52188/authentication/login-callback"));
                        descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:52188/authentication/logout-callback"));
                        await appManager.CreateAsync(descriptor);
                    }

                    // ContentSearch Blazor WebAssembly client
                    var contentClientId = "contentsearch.client";
                    if (await appManager.FindByClientIdAsync(contentClientId) == null)
                    {
                        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
                        {
                            ClientId = contentClientId,
                            DisplayName = "ContentSearch Blazor Client",
                        };
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Email);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Scopes.Profile);
                        // Refresh token grant is enabled; offline_access scope is not required as a permission constant here
                        descriptor.RedirectUris.Add(new Uri("https://localhost:52186/authentication/login-callback"));
                        descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:52186/authentication/logout-callback"));
                        await appManager.CreateAsync(descriptor);
                    }
                    // Gateway server-side client (confidential)
                    var gatewayClientId = "gateway.client";
                    if (await appManager.FindByClientIdAsync(gatewayClientId) == null)
                    {
                        // Read a gateway client secret from configuration (supports Azure Key Vault via config provider)
                        var configuration = services.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                        var gatewaySecret = configuration != null
                            ? configuration["Authentication:Gateway:ClientSecret"] ?? configuration["GATEWAY_CLIENT_SECRET"]
                            : null;

                            if (string.IsNullOrEmpty(gatewaySecret))
                            {
                                // Fallback for local development only
                                gatewaySecret = "gateway-secret";
                                // Use LoggerMessage pattern for high-performance logging
                                LoggerMessage.Define(LogLevel.Warning, new EventId(1001, "DevSecret"), "Using development gateway client secret. Replace with a secret store in production.")
                                    (logger, null);
                            }

                        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
                        {
                            ClientId = gatewayClientId,
                            DisplayName = "Gateway Server Client",
                            ClientSecret = gatewaySecret
                        };
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Authorization);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.Endpoints.Token);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                        descriptor.Permissions.Add(OpenIddict.Abstractions.OpenIddictConstants.Permissions.ResponseTypes.Code);
                        descriptor.RedirectUris.Add(new Uri("https://localhost:52192/signin-oidc"));
                        descriptor.PostLogoutRedirectUris.Add(new Uri("https://localhost:52192/signout-callback-oidc"));
                        await appManager.CreateAsync(descriptor);
                    }
                }
                }

                // Ensure an admin user exists for local development
                var adminEmail = "admin@local.bookknowledge";
                var admin = await userManager.FindByEmailAsync(adminEmail);
                if (admin == null)
                {
                    admin = new Entities.ApplicationUser
                    {
                        UserName = "admin",
                        Email = adminEmail,
                        EmailConfirmed = true
                    };
                    var result = await userManager.CreateAsync(admin, GetDefaultAdminPassword());
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to create admin user: {string.Join(',', result.Errors)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while seeding the identity database: {ex}");
                throw;
            }
        }

        private static string GetDefaultAdminPassword()
        {
            // Use a reasonable default for local development only.
            // In production, create users via an admin UI or external identity provider.
            return "Admin@12345";
        }
    }
}
