# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy Directory.Packages.props first (required for restore)
COPY ["src/Directory.Packages.props", "src/"]

# Copy and restore dependencies (cached layer)
COPY ["src/AktieKoll/AktieKoll.csproj", "src/AktieKoll/"]
RUN dotnet restore "src/AktieKoll/AktieKoll.csproj"

# Copy source code and build
COPY src/AktieKoll/ src/AktieKoll/
WORKDIR "/src/src/AktieKoll"
RUN dotnet build "AktieKoll.csproj" -c Release -o /app/build

# Stage 2: Publish the application
FROM build AS publish
RUN dotnet publish "AktieKoll.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Create runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# Run as non-root user (security)
USER $APP_UID

# Document port
EXPOSE 8080

# Copy published app and set startup command
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AktieKoll.dll"]