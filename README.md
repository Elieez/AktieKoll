# AktieKoll 📈
**Spårning och analys av insiderhandel på den svenska aktiemarknaden**

AktieKoll är ett backend-system byggt i **.NET 10** som automatiskt hämtar, bearbetar och presenterar insiderhandelsdata från Finansinspektionen. Tanken är enkel: istället för att manuellt leta i FI:s register kan du via ett REST API direkt fråga efter trender, bolagshistorik och de senaste affärerna.

---

## 🔄 Hur det fungerar – flödet i tre steg

```
1. HÄMTA       →   2. BERIKA       →   3. PRESENTERA
FI:s register      Koppla ticker       REST API
(CSV dagligen)     och ISIN via        
                   EODHD
```

1. **Hämta** – Var 6:e timme hämtas nya insideraffärer direkt från Finansinspektionens publika CSV-register.
2. **Berika** – Varje transaktion kopplas till rätt bolag via en lokal databas med tickers och ISIN-koder (synkad månadsvis från EODHD). Bolagsnamn rensas automatiskt från suffix som "AB" och "(publ)".
3. **Presentera** – All data görs tillgänglig via ett säkert REST API med JWT-autentisering.

---

## 🚀 Funktioner

- **Daglig datainsamling** – Automatisk synkronisering med FI:s insiderregister.
- **Datatvätt** – Rå CSV-data normaliseras och felaktig/irrelevant data filtreras bort.
- **Ticker-mapping** – Transaktioner kopplas till börssymboler (tickers) och ISIN utan externa API-anrop i realtid.
- **Trendanalys** – Endpoints för att hitta de mest köpta/sålda aktierna under en vald period.
- **Säker inloggning** – JWT-autentisering med refresh tokens i HTTP-only cookies (skydd mot XSS).

---

## 🛠 Teknisk stack

| Område | Teknik |
| :--- | :--- |
| **Runtime** | .NET 10 / ASP.NET Core |
| **Databas** | PostgreSQL med Entity Framework Core |
| **Säkerhet** | JWT, Refresh Tokens, HTTP-only Cookies |
| **Testning** | xUnit, Moq, FluentAssertions, Verify |
| **Externa API:er** | EODHD (börskurser & ticker-data) |
| **DevOps** | GitHub Actions (CI & Cron-jobb), Renovate Bot |

---

## 🏗 Projektstruktur

Projektet följer **Clean Architecture** och är uppdelat i fyra delar:

| Modul | Syfte |
| :--- | :--- |
| `AktieKoll/` | Core API – controllers, affärslogik, databasmodeller och autentisering |
| `FetchTrades/` | Konsolapp som körs som cron-jobb och hämtar dagliga insideraffärer från FI |
| `FetchCompanies/` | Månatligt jobb som synkroniserar tickers och ISIN för alla svenska börsbolag |
| `AktieKoll.Tests/` | Enhets- och integrationstester för parsing, databaslogik m.m. |

---

## 📡 API-översikt

### Autentisering – `/api/auth`
| Metod | Endpoint | Beskrivning |
| :--- | :--- | :--- |
| `POST` | `/login` | Loggar in och returnerar en access token |
| `POST` | `/refresh` | Förnyar sessionen med hjälp av refresh token-cookie |

### Insiderhandel – `/api/insidertrades`
| Metod | Endpoint | Beskrivning |
| :--- | :--- | :--- |
| `GET` | `/top` | De 10 tyngsta transaktionerna från föregående handelsdag |
| `GET` | `/count-buy` | Bolag med flest köptransaktioner under en vald period |
| `GET` | `/company/{ticker}` | Alla transaktioner för ett specifikt bolag |

### Bolagsregister – `/api/company`
| Metod | Endpoint | Beskrivning |
| :--- | :--- | :--- |
| `GET` | `/` | Lista alla börsnoterade bolag i databasen |
| `GET` | `/{ticker}` | Hämta information om ett specifikt bolag (namn, ISIN, ticker) |
| `GET` | `/search?q={query}` | Sök efter bolag på namn eller ticker |

---

## ⚙️ Automatisering

Allt körs automatiskt via **GitHub Actions**:

| Jobb | Trigger | Vad det gör |
| :--- | :--- | :--- |
| `ci.yml` | Varje push | Bygger projektet och kör alla tester |
| `cron-fetch.yml` | Var 6:e timme | Hämtar och sparar nya insideraffärer från FI |
| `update-companies.yml` | 1:a varje månad | Uppdaterar databasen med tickers och ISIN via EODHD |

**Renovate Bot** håller automatiskt NuGet-paket uppdaterade för att minimera säkerhetsrisker.

---

## 💡 Syfte & lärdomar

Projektet skapades för att fördjupa kunskaper inom .NET och utforska hur man hanterar finansiell transaktionsdata i praktiken. Fokus har legat på säkerhet, enkel underhållbarhet och tydlig arkitektur – med hög testtäckning som grund.
