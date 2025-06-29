# AktieKoll

AktieKoll is a C# project for tracking and analyzing insider trades on the stock market. It provides functionality for fetching, storing, and querying insider trade data, with supporting services and tests for robust functionality.

## Features

- **Fetch Insider Trades**: Retrieve insider trade data (e.g., from CSV sources) using `CsvFetchService`.
- **Database Storage**: Store trades in a PostgreSQL database via Entity Framework Core.
- **Data Analysis**: Query for top companies by transaction volume, filter and process insider trades.
- **REST API**: Expose controllers for integration with frontend applications (CORS support included).
- **Test Coverage**: Includes unit tests for core services and database interactions.

## Technologies Used

- C#
- .NET Core / ASP.NET Core
- Entity Framework Core (PostgreSQL)
- CsvHelper
- xUnit (for testing)

## Project Structure

```
src/
  AktieKoll/            # Main Web API application
  FetchTrades/          # Standalone tool for fetching trades via CLI
  AktieKoll.Tests/      # Unit tests for services and database
```

## Prerequisites

- .NET 9 SDK
- PostgreSQL database
- [CsvHelper](https://joshclose.github.io/CsvHelper/) NuGet package
