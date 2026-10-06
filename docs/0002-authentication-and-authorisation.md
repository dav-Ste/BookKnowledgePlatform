# ADR 0002: Authentication and authorization architecture

## Status

Accepted

## Context

BookKnowledgePlatform contains two independently deployable Blazor frontends:

- Book Search
- Content Search

Both applications should provide a seamless authenticated user experience.

The platform also contains independently secured APIs:

- Identity
- Books
- Content Search

The APIs must not rely on the gateway as their only security boundary.

## Decision

Use OpenID Connect for user authentication and OAuth 2.0 bearer access tokens for API authorization.

The local deployment uses:

- ASP.NET Core Identity for users and credentials;
- OpenIddict as the OpenID Connect/OAuth authorization server;
- SQL Server for identity persistence.

Enterprise deployments can use Microsoft Entra ID as the identity authority.

Both frontends are separate OIDC clients but use the same authority.

The preferred flow is:

Authorization Code + PKCE.

The two Blazor applications therefore obtain authenticated application sessions through OIDC and access protected APIs using appropriately scoped access tokens.

### Gateway as an optional server-side OIDC client

The Gateway may act as a server-side OpenID Connect client for scenarios where a server-side session is preferred (for example: server-side Blazor, centralized UI, or when the gateway needs to call downstream APIs on behalf of an authenticated browser session).

- When enabled the gateway performs an Authorization Code flow (confidential client) and stores the resulting access_token and refresh_token in the authentication cookie (SaveTokens = true).
- The gateway exposes a confidential client registration (client_id: `gateway.client`) and must be configured with a client secret via configuration key `Authentication:Gateway:ClientSecret` (do not store secrets in plain text for production).
- Runtime behaviour:
  - On proxied requests the gateway attaches Authorization: Bearer <access_token> to the request so downstream APIs receive a standard OAuth2 bearer token.
  - If the access token is near expiry and a refresh_token is available, the gateway will attempt to refresh tokens by calling the identity token endpoint (/connect/token) and update the cookie with the new tokens.
  - If refresh fails the gateway currently falls back to requiring interactive re-authentication.

Security notes:

- Downstream APIs MUST still validate the bearer tokens (signature, issuer, audience, expiry and scopes). The gateway forwarding of tokens is a convenience and not a replacement for token validation by APIs.
- Treat the gateway client secret as sensitive configuration; in production store it in a secrets store (Key Vault, HashiCorp Vault) and reference it via configuration providers.

The repository includes a development seed that registers a `gateway.client` application in the local OpenIddict database and a sample client secret for local development only. Replace or remove the seeded secret for production.

## API security

Every API independently validates access tokens.

The gateway is not treated as a security boundary.

The intended flow is:

Browser
→ Blazor application
→ Gateway
→ protected API

The API validates:

- token signature;
- issuer;
- audience;
- expiration;
- scopes/permissions.

## Authorization

Authorization is based on permissions rather than hard-coded role checks throughout the application.

Initial permissions include:

- books.read
- books.manage
- content.read
- content.manage
- documents.upload
- documents.delete
- administration.users
- administration.system

Roles may group permissions.

Initial roles include:

- Reader
- BookManager
- ContentManager
- Administrator

## Consequences

The two frontends have SSO because they share the same identity authority.

The Books and Content APIs remain independently secure.

The application can move from local identity to Entra without changing domain/application authorization rules.

Authentication infrastructure is isolated from business functionality.
