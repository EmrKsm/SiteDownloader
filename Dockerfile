# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first for better layer caching
COPY ["Directory.Build.props", "./"]
COPY ["src/SiteDownloader.Core/SiteDownloader.Core.csproj", "src/SiteDownloader.Core/"]
COPY ["src/SiteDownloader.App/SiteDownloader.App.csproj", "src/SiteDownloader.App/"]

# Restore dependencies
RUN dotnet restore "src/SiteDownloader.App/SiteDownloader.App.csproj"

# Copy everything else and build
COPY . ./
RUN dotnet publish "src/SiteDownloader.App/SiteDownloader.App.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Create non-root user for better security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# App writes to ./downloads by default. Mount a volume if you want to persist.
COPY --from=build /app/publish ./

# Create directories with proper permissions
RUN mkdir -p downloads logs && \
    chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "SiteDownloader.App.dll"]
