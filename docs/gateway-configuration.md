# Gateway configuration examples

This document shows example configuration snippets for the gateway when it acts as a server-side OIDC client and for YARP route-level authorization.

appsettings (gateway)

```json
{
  "Identity": {
	"Authority": "https://localhost:52194"
  },
  "Authentication": {
	"Gateway": {
	  "ClientSecret": "<replace-with-secret-or-secret-ref>"
	}
  },
  "Logging": { /* ... */ },
  "ReverseProxy": {
	"Routes": [
	  {
		"RouteId": "books_api",
		"ClusterId": "books_cluster",
		"Match": { "Path": "/api/books/{**catch-all}" },
		"AuthorizationPolicy": "books.read"
	  },
	  {
		"RouteId": "content_api",
		"ClusterId": "content_cluster",
		"Match": { "Path": "/api/content/{**catch-all}" },
		"AuthorizationPolicy": "content.read"
	  }
	],
	"Clusters": {
	  "books_cluster": {
		"Destinations": { "book1": { "Address": "https://localhost:52184/" } }
	  },
	  "content_cluster": {
		"Destinations": { "content1": { "Address": "https://localhost:52187/" } }
	  }
	}
  }
}
```

Notes
- `Identity:Authority` should point at your identity server (local OpenIddict during development).
- `Authentication:Gateway:ClientSecret` should not be checked into source control for production. Use a secret store and configuration provider instead.
- YARP `AuthorizationPolicy` maps a route to an ASP.NET Core authorization policy defined in the gateway (e.g., `books.read`). When set, the gateway enforces the policy before proxying.

How routes and policies interact
- Define your authorization policies in Program.cs (see docs/0002-authentication-and-authorisation.md).
- Use `AuthorizationPolicy` per route in the ReverseProxy configuration to require specific scopes for that route.
