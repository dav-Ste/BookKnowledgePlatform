# Secrets and client secret rotation

This document describes approaches for managing the gateway client secret and other sensitive configuration in development and production.

Local development
- For local development the repository seeds a `gateway.client` with a sample client secret (see Identity seeder). This is intentionally convenient for developer feedback loops only.
- Do NOT use the seeded secret in production. Replace it with a secret managed by a secure store.

Production
- Use a secret store: Azure Key Vault, HashiCorp Vault, AWS Secrets Manager, or another secret provider.
- Use the ASP.NET Core configuration providers to load secrets at startup. For example, to use Azure Key Vault, configure the `AddAzureKeyVault` provider using a managed identity or service principal.

Rotating the gateway client secret
1. Create the new client secret in the identity authority (rotate on the Identity/OpenIddict side — create a new client secret value for `gateway.client`).
2. Add the new secret to your secret store (Key Vault/Vault) and update configuration access.
3. Roll update the gateway application to read the new secret (ideally via dynamic configuration refresh or a deployment that restarts the gateway). Support blue/green deployments by creating overlapping secrets if your identity authority supports multiple valid secrets.
4. Remove the old secret from the secret store after verifying the new secret is in use.

Using HashiCorp Vault (example)
- Store `Authentication/Gateway/ClientSecret` in Vault.
- Configure the gateway to use the Vault provider at startup (community providers or the Vault Agent pattern are common).

ASP.NET Core configuration example (pseudocode)

```csharp
var builder = WebApplication.CreateBuilder(args);
if (env.IsProduction())
{
	// Use Key Vault or Vault provider here
	builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), credential);
}
var clientSecret = builder.Configuration["Authentication:Gateway:ClientSecret"];
```

Security considerations
- Rotate secrets periodically and audit access.
- Limit which principals can read secrets used by the gateway.
- Use managed identities when possible to avoid distributing credentials.
