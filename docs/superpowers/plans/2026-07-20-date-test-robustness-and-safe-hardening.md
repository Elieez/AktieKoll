# Date-Test Robustness + Safe Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `InsiderTradeService`'s date-window logic fully clock-injectable so the date-based snapshot tests stop rotting, add logging to three silent `catch {}` blocks, and deliver a written report of the riskier findings.

**Architecture:** `InsiderTradeService` already receives an injected `TimeProvider` and uses it in one method. Route its two remaining wall-clock reads (`DateTime.UtcNow`) through the same `TimeProvider`, then freeze the clock with `FakeTimeProvider` in the six window tests so their data window is stable forever. Production keeps using `TimeProvider.System` (registered in `Program.cs`), so runtime behavior is unchanged and the frontend is unaffected.

**Tech Stack:** .NET 10 / ASP.NET Core, EF Core (InMemory for these tests), xUnit v3, Verify.XunitV3, `Microsoft.Extensions.Time.Testing.FakeTimeProvider`, Moq.

## Global Constraints

- Target framework: **.NET 10** (`net10.0`).
- **Non-breaking:** no change to production runtime behavior; production resolves `TimeProvider.System` from DI. The connected Next.js frontend must be unaffected.
- **Never edit any `.verified.txt` snapshot file.** The committed snapshots already hold the correct expected output; the fix must make the produced output match them.
- `.received.*` files are gitignored — deleting them is local-only cleanup.
- Freeze the test clock to `new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)` — the exact convention already used by `GetTransactionStats_ReturnsModellSuccess`.
- Logging must not change any HTTP response (the forgot-password anti-enumeration behavior and all return values stay identical).

---

### Task 1: Make `InsiderTradeService` fully clock-injectable

Route the two remaining `DateTime.UtcNow` reads through the already-injected `TimeProvider`. This is a pure refactor: in production `TimeProvider.System` gives identical values.

**Files:**
- Modify: `src/AktieKoll/Services/InsiderTradeService.cs` (method `GetInsiderTradesTop` ~line 97; method `GetTransactionCountByType` ~line 110)

**Interfaces:**
- Consumes: the existing `TimeProvider timeProvider` primary-constructor parameter (already present at `InsiderTradeService.cs:14`).
- Produces: no signature changes. Public methods `GetInsiderTradesTop()`, `GetTransactionCountBuy(string?, int, int?)`, `GetTransactionCountSell(string?, int, int?)` keep identical signatures; only their internal "now" source changes.

- [ ] **Step 1: Change `GetInsiderTradesTop` to use the injected clock**

In `src/AktieKoll/Services/InsiderTradeService.cs`, find:

```csharp
    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop()
    {
        var today = DateTime.UtcNow.Date;
```

Replace the `today` line with:

```csharp
    public async Task<IEnumerable<InsiderTrade>> GetInsiderTradesTop()
    {
        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
```

- [ ] **Step 2: Change `GetTransactionCountByType` to use the injected clock**

In the same file, find:

```csharp
    private async Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountByType(string transactionType, string? symbol, int days, int? top)
    {
        var endDate = DateTime.UtcNow.Date.AddDays(1);
```

Replace the `endDate` line with:

```csharp
    private async Task<IEnumerable<CompanyTransactionStats>> GetTransactionCountByType(string transactionType, string? symbol, int days, int? top)
    {
        var endDate = timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1);
```

- [ ] **Step 3: Confirm no other `DateTime.UtcNow`/`DateTime.Today` remains in this file**

Run: `grep -n "DateTime.UtcNow\|DateTime.Today\|DateTime.Now" src/AktieKoll/Services/InsiderTradeService.cs`
Expected: **no output** (every clock read now goes through `timeProvider`).

- [ ] **Step 4: Build the solution**

Run: `dotnet build src/AktieKoll/AktieKoll.csproj`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 5: Confirm the non-window tests still pass**

Run: `dotnet test src/AktieKoll.Tests --filter "FullyQualifiedName~GetInsiderTradesTop_ReturnsTopByValue|FullyQualifiedName~GetTransactionStats_ReturnsModellSuccess"`
Expected: `Passed!  - Failed: 0`. (These two use relative dates / an already-frozen clock, so they are unaffected.)

> Note: the six `GetTransactionCount{Buy,Sell}_ReturnsMostActive` cases are **still expected to fail** after this task — their test clock is not frozen yet. Task 2 fixes them.

- [ ] **Step 6: Commit**

```bash
git add src/AktieKoll/Services/InsiderTradeService.cs
git commit -m "refactor: route InsiderTradeService date windows through injected TimeProvider

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014LfQYscgQwSSAbpzxoTuY1"
```

---

### Task 2: Freeze the clock in the window tests

Make the six `GetTransactionCount{Buy,Sell}_ReturnsMostActive` cases deterministic by injecting a `FakeTimeProvider` pinned to 2025-12-31, so their 365-day window always covers the June 2025 seed data.

**Files:**
- Modify: `src/AktieKoll.Tests/Integration/Services/TransactionsDbTests.cs` (method `GetTransactionCountBuy_ReturnsMostActive` ~line 322; method `GetTransactionCountSell_ReturnsMostActive` ~line 350)
- Delete: stale `*.received.txt` under `src/AktieKoll.Tests/Integration/Services/`

**Interfaces:**
- Consumes: `ServiceTestHelpers.CreateInsiderTradeService(ApplicationDbContext ctx, TimeProvider? timeProvider = null)` (exists at `ServiceTestHelpers.cs:27`); `Microsoft.Extensions.Time.Testing.FakeTimeProvider` (already imported at `TransactionsDbTests.cs:8`).
- Produces: no new symbols.

- [ ] **Step 1: Run the six cases and confirm they currently FAIL**

Run: `dotnet test src/AktieKoll.Tests --filter "FullyQualifiedName~GetTransactionCountBuy_ReturnsMostActive|FullyQualifiedName~GetTransactionCountSell_ReturnsMostActive"`
Expected: FAIL. The Verify diff shows produced `[]` vs the verified snapshot (e.g. `TransactionCount: 8`), because the real-time 365-day window no longer reaches the June 2025 data.

- [ ] **Step 2: Freeze the clock in `GetTransactionCountBuy_ReturnsMostActive`**

In `src/AktieKoll.Tests/Integration/Services/TransactionsDbTests.cs`, inside `GetTransactionCountBuy_ReturnsMostActive`, find:

```csharp
        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTransactionCountBuy(symbol, days, top);
```

Replace it with:

```csharp
        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx, fakeTime);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTransactionCountBuy(symbol, days, top);
```

- [ ] **Step 3: Freeze the clock in `GetTransactionCountSell_ReturnsMostActive`**

In the same file, inside `GetTransactionCountSell_ReturnsMostActive`, find:

```csharp
        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTransactionCountSell(symbol, days, top);
```

Replace it with:

```csharp
        var csvDto = await csvFetchService.FetchInsiderTradesAsync(fromDate, toDate);
        var trades = InsiderTradeMapper.MapDtosToTrades(csvDto);

        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero));

        var service = ServiceTestHelpers.CreateInsiderTradeService(ctx, fakeTime);
        await service.AddInsiderTrades(trades);

        var result = await service.GetTransactionCountSell(symbol, days, top);
```

- [ ] **Step 4: Run the six cases and confirm they now PASS**

Run: `dotnet test src/AktieKoll.Tests --filter "FullyQualifiedName~GetTransactionCountBuy_ReturnsMostActive|FullyQualifiedName~GetTransactionCountSell_ReturnsMostActive"`
Expected: `Passed!  - Failed: 0`. No `.verified.txt` file was modified (confirm with `git status` — only `.cs` changes should appear tracked).

- [ ] **Step 5: Delete stale received-snapshot artifacts**

Verify deletes current-signature `.received.txt` on pass, but leftovers from an old signature remain. Remove all received artifacts in the test tree (gitignored, safe):

Run: `find src/AktieKoll.Tests -name "*.received.txt" -delete`
Then run: `find src/AktieKoll.Tests -name "*.received.txt"`
Expected: no output.

- [ ] **Step 6: Commit**

```bash
git add src/AktieKoll.Tests/Integration/Services/TransactionsDbTests.cs
git commit -m "test: freeze clock in transaction-count window tests to stop date rot

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014LfQYscgQwSSAbpzxoTuY1"
```

---

### Task 3: Log the three silent `catch {}` blocks in `AuthController`

Inject `ILogger<AuthController>` and log a warning where email sends are currently swallowed. Additive only — no response changes.

**Files:**
- Modify: `src/AktieKoll/Controllers/AuthController.cs` (primary constructor ~lines 20-25; catch blocks ~lines 70, 320, 440)

**Interfaces:**
- Consumes: `ILogger<AuthController>` from DI (resolves automatically; `ILogger<T>` is available via the web SDK's implicit usings — `InsiderTradeService` already uses `ILogger<T>` with no explicit `using`).
- Produces: no signature changes to any action method.

- [ ] **Step 1: Add the logger to the primary constructor**

In `src/AktieKoll/Controllers/AuthController.cs`, find:

```csharp
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IAuthService authService,
    IEmailService emailService,
    IConfiguration config,
    ApplicationDbContext db) : ControllerBase
```

Replace with:

```csharp
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IAuthService authService,
    IEmailService emailService,
    IConfiguration config,
    ApplicationDbContext db,
    ILogger<AuthController> logger) : ControllerBase
```

- [ ] **Step 2: Log in the registration email catch (~line 70)**

Find:

```csharp
            await emailService.SendEmailVerificationAsync(user.Email!, code);
        }
        catch { /* log but don't surface */ }
```

Replace with:

```csharp
            await emailService.SendEmailVerificationAsync(user.Email!, code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send verification email during registration for user {UserId}.", user.Id);
        }
```

- [ ] **Step 3: Log in the forgot-password catch (~line 320)**

Find:

```csharp
                await emailService.SendPasswordResetAsync(user.Email!, code);
            }
            catch { /* log but never reveal */ }
```

Replace with:

```csharp
                await emailService.SendPasswordResetAsync(user.Email!, code);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process password-reset email for user {UserId}.", user.Id);
            }
```

- [ ] **Step 4: Log in the account-deleted confirmation catch (~line 440)**

Find:

```csharp
        try { await emailService.SendAccountDeletedConfirmationAsync(email); } catch { }
```

Replace with:

```csharp
        try
        {
            await emailService.SendAccountDeletedConfirmationAsync(email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send account-deleted confirmation email.");
        }
```

- [ ] **Step 5: Build**

Run: `dotnet build src/AktieKoll/AktieKoll.csproj`
Expected: `Build succeeded.` with 0 errors. (If the compiler reports `ILogger<>` not found, add `using Microsoft.Extensions.Logging;` to the top of the file — normally unnecessary.)

- [ ] **Step 6: Confirm the auth tests still pass**

The integration tests build `AuthController` through the real DI container, so they prove the new constructor dependency resolves.

Run: `dotnet test src/AktieKoll.Tests --filter "FullyQualifiedName~AuthControllerTests|FullyQualifiedName~AuthFeatureTests"`
Expected: `Passed!  - Failed: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/AktieKoll/Controllers/AuthController.cs
git commit -m "fix: log swallowed email failures in AuthController

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014LfQYscgQwSSAbpzxoTuY1"
```

---

### Task 4: Write the Part 3 findings report

Deliver the report-only security findings as a committed document so the owner can act on them later.

**Files:**
- Create: `docs/superpowers/reports/2026-07-20-security-findings.md`

**Interfaces:** none (documentation deliverable).

- [ ] **Step 1: Create the report directory and file**

Create `docs/superpowers/reports/2026-07-20-security-findings.md` with exactly this content:

```markdown
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
```

- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/reports/2026-07-20-security-findings.md
git commit -m "docs: security findings report (forwarded headers, AllowedHosts, OAuth token-in-URL)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_014LfQYscgQwSSAbpzxoTuY1"
```

---

### Task 5: Full-suite verification

Confirm the whole test suite is green and nothing regressed.

**Files:** none (verification only).

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test src/AktieKoll.Tests`
Expected: `Passed!  - Failed: 0`, with no date-related snapshot failures.

> If unrelated integration tests that require a live PostgreSQL/`WebApplicationFactory`
> backend fail in this environment, note them explicitly — they are outside this plan's
> scope. The date tests (`TransactionsDbTests`) use the EF Core InMemory provider and must
> pass.

- [ ] **Step 2: Confirm no snapshot files changed**

Run: `git status --porcelain "*.verified.txt"`
Expected: no output (no `.verified.txt` was modified or added).

- [ ] **Step 3: Confirm production clock-read consistency once more**

Run: `grep -rn "DateTime.UtcNow\|DateTime.Now\|DateTime.Today" src/AktieKoll/Services/InsiderTradeService.cs`
Expected: no output.

---

## Self-Review

**Spec coverage:**
- Part 1 (production clock consistency) → Task 1. ✓
- Part 1 (test fake clock, no snapshot edits, orphan cleanup) → Task 2. ✓
- Part 2 (empty catch → logging, 3 sites) → Task 3. ✓
- Part 3 (report-only findings) → Task 4. ✓
- Success criteria (suite green, snapshots untouched, prod behavior unchanged) → Task 5. ✓

**Placeholder scan:** No TBD/TODO; every code step shows exact before/after; report content is fully written. ✓

**Type consistency:** `CreateInsiderTradeService(ctx, fakeTime)` matches the helper's `(ApplicationDbContext, TimeProvider?)` signature; `FakeTimeProvider().SetUtcNow(DateTimeOffset)` matches the existing usage at line 657; `ILogger<AuthController>` constructor injection matches ASP.NET Core DI; `timeProvider.GetUtcNow().UtcDateTime` matches the existing `GetYtdTransactionStatsAsync` usage. ✓
