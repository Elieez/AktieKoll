# AktieKoll 📈
**Financial Intelligence & Insider Trade Analytics**

AktieKoll är en robust backend-tjänst utvecklad i **.NET 10** för att spåra och analysera insiderhandel på den svenska aktiemarknaden. Systemet automatiserar hela flödet: från datainsamling via Finansinspektionen till berikning av data via externa finansiella API:er, allt presenterat genom ett säkert och väldokumenterat REST API.

---

## 🚀 Huvudfunktioner

* **Automatiserad Datainsamling:** Ett schemalagt arbetsflöde som hämtar dagliga rapporter över insiderhandel direkt från Finansinspektionens publika register.
* **Datatvätt & Normalisering:** Avancerad parsing av rå CSV-data där irrelevant brus filtreras bort och bolagsnamn normaliseras (t.ex. automatisk rensning av suffix som "AB" och "(publ)") för högre datakvalitet.
* **Symbol Enrichment (OpenFIGI):** Berikar transaktionsdata genom att mappa ISIN-koder till börstickers via OpenFIGI API, vilket möjliggör sömlös integration med andra finansiella plattformar.
* **Säkerhet i Bankklass:** Implementerat JWT-baserat autentiseringssystem med **Refresh Tokens** lagrade i säkra, HTTP-only cookies för att minimera risker som XSS-attacker.
* **Finansiell Analys:** Skräddarsydda endpoints för att identifiera marknadstrender, såsom de mest köpta/sålda aktierna över specifika tidsperioder baserat på volym och transaktionsvärde.

---

## 🛠 Teknisk Stack

| Område | Teknik |
| :--- | :--- |
| **Runtime** | .NET 10 / ASP.NET Core |
| **Databas** | PostgreSQL med Entity Framework Core |
| **Säkerhet** | JWT, Refresh Tokens, HTTP-only Cookies |
| **Testning** | xUnit, Moq, FluentAssertions, Verify |
| **Externa API:er** | OpenFIGI (Financial Instrument Global Identifier) |
| **DevOps** | GitHub Actions (CI & Cron-jobs), Renovate Bot |

---

## 🏗 Arkitektur & Struktur

Projektet är byggt med fokus på **Clean Architecture** och separationsprincipen (*Separation of Concerns*), fördelat på tre huvudmoduler:

* **`AktieKoll/`** – **Core API**: Innehåller controllers, affärslogik, databasmodeller och den säkra autentiseringsmodulen.
* **`FetchTrades/`** – **Data Ingestion**: En specialiserad konsolapplikation designad för att köras som ett schemalagt jobb (Cron) för att driva datapipe-linen.
* **`AktieKoll.Tests/`** – **Quality Assurance**: Omfattande enhets- och integrationstester som validerar allt från parsing-logik till databasintegritet.

---

## 📡 API Översikt (Urval)

### Autentisering (`/api/auth`)
* `POST /login` – Autentiserar användare och utfärdar access tokens.
* `POST /refresh` – Förnyar sessionen via säkra cookies.

### Insynsdata (`/api/insidertrades`)
* `GET /top` – De 10 tyngsta transaktionerna från föregående handelsdag.
* `GET /count-buy` – Trendanalys: Bolag med flest köptransaktioner över vald period.
* `GET /company` – Full historik för specifika bolag.

---

## ⚙️ Automatisering & Drift

Projektet nyttjar **GitHub Actions** för att säkerställa en modern utvecklingscykel:
* **CI-Pipeline (`ci.yml`):** Automatisk byggnation och testkörning vid varje push för att garantera stabilitet.
* **Data-Sync (`cron-fetch.yml`):** Körs var 6:e timme för att synkronisera databasen med de senaste transaktionerna från Finansinspektionen.
* **Dependency Management:** Integrerad med **Renovate** för att automatiskt hantera och uppdatera NuGet-paket, vilket håller systemet säkert och up-to-date.

---

### Syfte & Lärdomar
Det här projektet skapades för att fördjupa mina kunskaper inom .NET och utforska komplexiteten i att hantera finansiell transaktionsdata. Fokus har legat på att bygga ett system som är både säkert och lätt att underhålla genom tydlig arkitektur och hög testtäckning.
