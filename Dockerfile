# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore .\src\SiteDownloader.App\SiteDownloader.App.csproj
RUN dotnet publish .\src\SiteDownloader.App\SiteDownloader.App.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

# App writes to ./downloads by default. Mount a volume if you want to persist.
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "SiteDownloader.App.dll"]
