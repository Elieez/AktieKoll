# AktieKoll
**Swedish insider trading — tracked, enriched, and delivered in real time**

AktieKoll is a REST API backend built with **.NET 10** that automatically collects, processes, and serves insider trading data from Finansinspektionen. Instead of manually digging through FI's public register, users get a clean API with trend analysis, company search, real-time alerts, and a full authentication system.

---

## How It Works

```
1. FETCH          →   2. ENRICH         →   3. DELIVER
FI's register         Map ticker              Secure REST API
(CSV every 6h)        and ISIN via            + Email & Discord
                      EODHD                   notifications
```

1. **Fetch** — Every 6 hours, new insider trades are pulled directly from Finansinspektionen's public CSV register via GitHub Actions.
2. **Enrich** — Each transaction is matched to a company using a local database of tickers and ISIN codes (synced monthly from EODHD). Company names are automatically cleaned of suffixes like "AB" and "(publ)".
3. **Deliver** — All data is exposed through a secure REST API with JWT authentication, and followers of a company receive email or Discord alerts when new trades come in.

---

## Features

**Data pipeline**
- Automated sync with Finansinspektionen's insider register every 6 hours
- Raw CSV data normalized and cleaned — invalid/duplicate records filtered out
- Ticker and ISIN mapping without real-time external API calls
- Trend analysis endpoints (top trades, buy/sell counts, YTD statistics)

**Authentication**
- JWT access tokens (15-minute lifetime) + refresh tokens (7-day lifetime) stored in HTTP-only cookies (XSS protection)
- Google OAuth — sign in with Google, auto-links to existing accounts
- Email verification on registration, with resend support
- Password reset via email token (1-hour expiry)
- Account lockout after 5 failed login attempts (15-minute cooldown)
- GDPR-compliant two-step account deletion — request, confirm via email, permanently erased

**Notifications**
- Per-company follow system — users follow the companies they care about
- Email alerts with HTML-formatted trade details (insider name, role, type, share count, price, total value)
- Discord webhook notifications with rich embeds — color-coded buy/sell, up to 25 trades per message
- Per-user preferences — enable/disable email and Discord independently, set a custom Discord webhook URL
- Deduplication via `NotificationLog` — no duplicate alerts across batch runs

---

## Tech Stack

| Area | Tech |
| :--- | :--- |
| **Runtime** | .NET 10 / ASP.NET Core |
| **Database** | PostgreSQL with Entity Framework Core |
| **Security** | JWT, Refresh Tokens, HTTP-only Cookies, Google OAuth |
| **Email** | MailKit (SMTP) |
| **Notifications** | Discord Webhooks |
| **Testing** | xUnit, Moq, FluentAssertions, Verify |
| **External APIs** | EODHD (tickers & market data), Finansinspektionen (insider register) |
| **DevOps** | GitHub Actions (CI & cron jobs), Renovate Bot |

---

## Project Structure

Follows **Clean Architecture**, split into four modules:

| Module | Purpose |
| :--- | :--- |
| `AktieKoll/` | Core API — controllers, business logic, database models, auth, and notifications |
| `FetchTrades/` | Console app run as a cron job — fetches daily insider trades from FI |
| `FetchCompanies/` | Monthly job — syncs tickers and ISIN codes for all Swedish listed companies via EODHD |
| `AktieKoll.Tests/` | Unit and integration tests covering parsing, database logic, and more |

---

## API Overview

### Authentication — `/api/auth`

| Method | Endpoint | Auth | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/register` | — | Register a new account (sends verification email) |
| `POST` | `/login` | — | Authenticate with email/password, returns JWT and sets refresh-token cookie |
| `POST` | `/refresh` | Cookie | Rotate the refresh token, issue a new JWT |
| `POST` | `/logout` | Cookie | Revoke the refresh token |
| `GET` | `/google` | — | Initiate Google OAuth flow |
| `GET` | `/google/handle` | — | Google OAuth callback |
| `GET` | `/verify-email` | — | Verify email address via link from inbox |
| `POST` | `/resend-verification` | JWT | Resend the email verification link |
| `POST` | `/forgot-password` | — | Request a password reset email |
| `POST` | `/reset-password` | — | Reset password using the emailed token |
| `POST` | `/account/delete/request` | JWT | Initiate account deletion (sends confirmation email) |
| `POST` | `/account/delete/confirm` | JWT | Confirm and permanently delete the account |

### Insider Trades — `/api/insidertrades`

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/page` | Paginated list of all insider trades |
| `GET` | `/top` | Top 10 largest trades from the previous trading day |
| `GET` | `/count-buy` | Companies with the most buy transactions in a given period |
| `GET` | `/count-sell` | Companies with the most sell transactions in a given period |
| `GET` | `/company` | All trades for a specific company (by ticker symbol) |
| `GET` | `/ytd-stats` | Year-to-date aggregate statistics |

### Companies — `/api/company`

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/` | List all listed companies in the database |
| `GET` | `/{code}` | Get a company by ticker/code (name, ISIN, ticker) |
| `GET` | `/search?q={query}` | Search companies by name or ticker |

### Following — `/api/follow` *(requires auth)*

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/{companyId}` | Follow a company |
| `DELETE` | `/{companyId}` | Unfollow a company |
| `GET` | `/` | Get all followed companies |
| `GET` | `/{companyId}` | Check follow status for a specific company |

### Notification Preferences — `/api/notification/preferences` *(requires auth)*

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/` | Get current notification preferences |
| `PUT` | `/` | Update preferences (email on/off, Discord on/off, webhook URL) |

---

## Automation

Everything runs automatically via **GitHub Actions**:

| Job | Trigger | What it does |
| :--- | :--- | :--- |
| `ci.yml` | Every push | Builds the project and runs all tests |
| `cron-fetch.yml` | Every 6 hours | Fetches new insider trades from FI and processes notifications |
| `update-companies.yml` | 1st of every month | Updates the company database with tickers and ISIN via EODHD |

**Renovate Bot** automatically keeps NuGet packages up to date to minimize security exposure.

---

## Purpose

Built to deepen knowledge of .NET and explore real-world handling of financial transaction data. The focus has been on security, maintainability, and clear architecture — with a full test suite as the foundation.

The frontend for this project lives at [elieez/aktiekollwebb](https://github.com/Elieez/aktiekollwebb).
