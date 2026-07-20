# Design: Date-Test Robustness + Safe Hardening

**Date:** 2026-07-20
**Status:** Approved (pending user spec review)
**Scope:** Non-breaking backend improvements only. No frontend-visible behavior changes.

## Context

AktieKoll is a production ASP.NET Core (.NET 10) REST API backend with a separate
Next.js frontend (aktiekoll.com). This pass has two goals agreed with the owner:

1. Make the date-based tests robust so they never rot again ("date is out of range"
   snapshot failures must not recur).
2. Apply clearly-safe, non-breaking security/quality improvements, and produce a
   written report for riskier findings that need owner decisions.

Constraint: **do not break the connected frontend.** Any change that could alter
runtime behavior the frontend depends on is out of scope for automatic implementation
and is reported instead.

## Part 1 — Date/time test rot (core fix)

### Root cause

`InsiderTradeService` already receives an injected `TimeProvider` and uses it correctly
in `GetYtdTransactionStatsAsync`. Two other methods bypass it and read the wall clock:

- `GetTransactionCountByType` (`InsiderTradeService.cs:110`) → `DateTime.UtcNow`.
  This drives the `days`-window filter. The 6 `GetTransactionCount{Buy,Sell}_ReturnsMostActive`
  tests pin their data to June 2025, but the window is measured from *now*. Once real
  time moved past `data + 365 days`, the window stopped covering the data and the query
  returns `[]`, so the Verify snapshot (`TransactionCount: 8`) no longer matches.
- `GetInsiderTradesTop` (`InsiderTradeService.cs:97`) → `DateTime.UtcNow`. Same latent
  bug, currently masked because its test seeds data with relative `DateTime.Today`.

Verified: for the failing tests, the committed `.verified.txt` files hold the correct
expected output; only the freshly produced `.received.txt` values are empty. Freezing the
test clock restores the match **without editing any snapshot file**.

### Fix — production (non-breaking)

Replace both `DateTime.UtcNow` reads with `timeProvider.GetUtcNow().UtcDateTime`. In
production the DI container supplies `TimeProvider.System` (registered in `Program.cs`),
so behavior is byte-for-byte identical. This only makes the whole service consistently
clock-injectable, matching the existing `GetYtdTransactionStatsAsync`.

### Fix — tests (makes rot impossible)

For `GetTransactionCountBuy_ReturnsMostActive` and `GetTransactionCountSell_ReturnsMostActive`,
construct the service with a `FakeTimeProvider` pinned to `2025-12-31` (the same convention
already used by `GetTransactionStats_ReturnsModellSuccess`) via the existing
`CreateInsiderTradeService(ctx, timeProvider)` overload. The window then always covers the
June 2025 data regardless of the real date.

Also delete the orphaned `.received.txt` files left over from a previous test signature
(e.g. `..._top=3.received.txt`, `..._days=null.received.txt`). `.received.*` is gitignored,
so this is purely local cleanup; Verify auto-removes current-signature received files on pass.

### Why this approach

Freezing the clock is the idiomatic .NET pattern and all the infrastructure already exists
(`TimeProvider` DI, `FakeTimeProvider`, the optional-param helper) — one method already does
it correctly. The alternative (rewriting test data to relative dates) also works but leaves
production still on the wall clock and reads less clearly than fixed real-world dates.

## Part 2 — Safe, additive hardening

### Empty `catch {}` → real logging

`AuthController.cs` lines 70, 320, 440 swallow email-send failures with comments promising
logging that never happens. Inject `ILogger<AuthController>` and log a warning at each site.

- Line 70: registration verification email failure.
- Line 320: forgot-password email failure (must still never reveal to caller — unchanged).
- Line 440: account-deleted confirmation email failure.

Purely additive: no response changes, no new throw paths, no frontend impact. Turns silent
SMTP failures into visible warnings.

## Part 3 — Report only (no code changes this pass)

Documented for owner decision; not implemented because each carries frontend/infra risk or
needs information not safely inferable.

- **Forwarded-headers trust** (`Program.cs`): `KnownProxies/KnownNetworks.Clear()` trusts
  `X-Forwarded-For` from any source → IP-based rate-limit bypass and `CreatedByIp` poisoning.
  Correct fix pins the hosting proxy's IP range; must not be guessed. Recent commits were
  actively tuning forwarded-header handling, so this needs owner input.
- **`AllowedHosts: "*"`** not overridden in production. Tightening it is recommended, but the
  backend sits behind the Next.js proxy on an Azure domain; setting the wrong inbound `Host`
  value rejects all traffic (outage). Owner chose report-only. To tighten later: determine the
  exact `Host` header the backend receives and set `"AllowedHosts"` to it in
  `appsettings.Production.json`.
- **OAuth access token in redirect URL** (`AuthController.cs:230`, `?token=...`): can leak via
  referrer/history/logs; mitigated by the 15-minute access-token lifetime. Changing it needs
  coordinated frontend work.

## Out of scope

- Frontend changes of any kind.
- Refactors not required by the two goals above.
- The report-only items in Part 3.

## Success criteria

- `dotnet test` passes with no date-related snapshot failures.
- The fix holds regardless of the current real date (verified by the frozen clock).
- No snapshot `.verified.txt` file is modified.
- Production runtime behavior is unchanged (frontend unaffected).
- Empty catch blocks now log warnings.
- A written report of Part 3 findings is delivered to the owner.
