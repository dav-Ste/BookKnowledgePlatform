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
