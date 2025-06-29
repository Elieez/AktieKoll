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

## Getting Started

### Prerequisites

- .NET 9 SDK
- PostgreSQL database
- [CsvHelper](https://joshclose.github.io/CsvHelper/) NuGet package

### Configuration

Set the PostgreSQL connection string in your environment variables:

```
ConnectionStrings__PostgresConnection="Host=localhost;Username=youruser;Password=yourpass;Database=aktiekoll"
```

### Running the Application

```bash
cd src/AktieKoll
dotnet run
```
The API will be available at `https://localhost:5001` (default).

### Running the FetchTrades CLI

```bash
cd src/FetchTrades
dotnet run
```

### Running Tests

```bash
cd src/AktieKoll.Tests
dotnet test
```

## Usage

- **API**: Use the exposed controllers to fetch, add, and analyze insider trades.
- **FetchTrades**: Command-line tool to pull and process trades into the database.
