# AktieKoll Security Findings — 2026-07-20

Report-only findings from the hardening pass. None were auto-applied: each carries
frontend/infra risk or needs information that must not be guessed. Ordered by priority.

## 1. Forwarded-headers trust (medium)

**Where:** `src/AktieKoll/Program.cs` — `ForwardedHeadersOptions` with
`KnownIPNetworks.Clear()` and `KnownProxies.Clear()`.

**Issue:** With the known-proxy list cleared, ASP.NET Core accepts `X-Forwarded-For`
from any caller. Because rate limiting and the `RefreshToken.CreatedByIp` audit field
derive from `RemoteIpAddress` (set from `X-Forwarded-For` after `UseForwardedHeaders`),
a client can rotate a spoofed header to evade the per-IP `auth`/`sensitive` rate limits
and to poison the stored client IP.

**Why not auto-fixed:** The correct fix pins the hosting platform's proxy IP range(s)
via `KnownNetworks`/`KnownProxies`. Guessing wrong either re-breaks forwarded headers
(which recent commits were tuning) or drops legitimate traffic. Needs the platform's
documented ingress ranges.

**Suggested fix:** Populate `KnownNetworks` with the load balancer / App Service inbound
range(s) instead of clearing them, or set `ForwardLimit = 1` if exactly one trusted proxy
sits in front.

## 2. `AllowedHosts: "*"` not restricted in production (low–medium)

**Where:** `src/AktieKoll/appsettings.json` (`"AllowedHosts": "*"`), not overridden in
`appsettings.Production.json`.

**Issue:** No Host-header allow-list; enables Host-header–based attacks in some setups.

**Why not auto-fixed:** The backend sits behind the Next.js proxy on an Azure domain.
Setting the wrong inbound `Host` value rejects all traffic (outage). Owner chose
report-only.

**Suggested fix:** Capture the exact `Host` header the backend actually receives (log
`Request.Host` in a staging request, or read it from the platform config), then set
`"AllowedHosts"` to that value in `appsettings.Production.json`.

## 3. OAuth access token in redirect URL (low)

**Where:** `src/AktieKoll/Controllers/AuthController.cs` — `GoogleHandle` redirects to
`{frontendCallback}?token={accessToken}`.

**Issue:** Access tokens placed in a URL query string can leak via `Referer` headers,
browser history, and intermediary logs.

**Mitigations already in place:** token lifetime is 15 minutes and the frontend is
documented to strip it from the URL immediately.

**Why not auto-fixed:** Moving the token out of the URL (e.g. a one-time exchange code, or
posting it via a short-lived server-set cookie) requires coordinated changes in the
frontend callback handler.

**Suggested fix:** Replace the token-in-URL handoff with a single-use, short-TTL exchange
code the frontend redeems server-to-server, or set the access token in a short-lived
HttpOnly cookie the frontend reads once — coordinated with the frontend team.
