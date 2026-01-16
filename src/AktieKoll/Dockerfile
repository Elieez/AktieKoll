# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY AktieKoll.slnx .
COPY Directory.Packages.props .
COPY AktieKoll/AktieKoll.csproj AktieKoll/
COPY AktieKoll.Tests/AktieKoll.Tests.csproj AktieKoll.Tests/
COPY FetchTrades/FetchTrades.csproj FetchTrades/

# Restore dependencies
RUN dotnet restore AktieKoll.slnx

# Copy source code
COPY AktieKoll/ AktieKoll/
COPY FetchTrades/ FetchTrades/

# Build the API project
WORKDIR /src/AktieKoll
RUN dotnet build -c Release --no-restore

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release --no-build -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published files
COPY --from=publish /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Configure ASP.NET Core to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AktieKoll.dll"]
