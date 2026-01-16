# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY AktieKoll.slnx .
COPY Directory.Packages.props .
COPY AktieKoll/AktieKoll.csproj AktieKoll/
COPY FetchTrades/FetchTrades.csproj FetchTrades/

# Restore dependencies
RUN dotnet restore AktieKoll.slnx

# Copy source code
COPY AktieKoll/ AktieKoll/
COPY FetchTrades/ FetchTrades/

# Build the FetchTrades project
WORKDIR /src/FetchTrades
RUN dotnet build -c Release --no-restore

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release --no-build -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published files
COPY --from=publish /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "FetchTrades.dll"]
