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
