# End-to-end login and token propagation flow

This document explains the end-to-end login flow when the gateway acts as a server-side OpenID Connect client and how tokens are propagated to downstream APIs.

Sequence (browser -> gateway -> identity -> gateway -> API)

1. Browser requests a protected page on the gateway.
2. Gateway detects no local cookie-based authentication and challenges the user using OpenID Connect (redirect to Identity:Authority /connect/authorize).
3. The user authenticates at the Identity server (username/password or external provider). Identity issues an authorization code and redirects back to the gateway (`/signin-oidc`).
4. The gateway redeems the code at the token endpoint (`/connect/token`) and receives an access_token, refresh_token and id_token. The gateway stores tokens in the authentication cookie (SaveTokens = true) and establishes a local session.
5. Browser continues to call the gateway; when the gateway proxies requests to downstream APIs, it reads the stored access_token from the cookie and attaches `Authorization: Bearer <access_token>` to the proxied request.
6. Downstream APIs validate the bearer token (signature, issuer, expiry, and scopes) and authorize access.

Token refresh behavior

- When the gateway sees the access token will expire soon, it attempts to refresh it using the stored refresh_token by POSTing to `/connect/token` with grant_type=refresh_token.
- If refresh succeeds the gateway updates the stored tokens in the cookie and proceeds with the request.
- If refresh fails (revoked token, invalid refresh token), the gateway should redirect the user to re-authenticate. Current implementation logs and falls back to interactive re-authentication.

Troubleshooting

- If the downstream API returns 401:
  - Verify the gateway attached an Authorization header (inspect gateway logs or use a proxy).
  - Confirm the API validates the token (issuer/audience) against the same authority used by the gateway.
  - Check the gateway token refresh logs for failures.

- If login redirect fails or the callback returns an error:
  - Ensure the gateway `RedirectUri` is registered for the `gateway.client` in Identity and matches the gateway runtime URL (e.g., `https://localhost:52192/signin-oidc`).
  - Check cookie SameSite settings and ensure the correlation cookie is allowed (SameSite=None) so the OIDC flow correlation data is retained across redirects.

Logs and diagnostics

- Gateway logs: configure logging at Information/Debug to capture OIDC handshake and token refresh operations.
- Identity logs: review OpenIddict logs to see client authentication, token issuance and refresh events.
