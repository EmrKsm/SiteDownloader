# SiteDownloader

Asynchronous multi-page downloader exercise (Entain pre-interview task).

## Requirements covered

- Accepts a list of URLs (CLI args) **or** reads them from a text file
- Downloads asynchronously in parallel (no thread blocking)
- Limits concurrency using `Channel`
- Uses `async/await` + `Task.WhenAll`
- Uses DI + `IHttpClientFactory` (no incorrect multiple `HttpClient` instances)
- Handles failures gracefully
- Supports cancellation (`Ctrl+C`)
- Structured logging
- Saves into `downloads/` at repo root, grouped by domain
- Unit + integration tests
- Dockerfile

## Prerequisites

- .NET 10 SDK (or later).

## Run

From the repo root:

### 1) Provide URLs via CLI

```powershell
# multiple --url entries
 dotnet run --project .\src\SiteDownloader.App -- --url https://example.com --url https://httpbin.org/html

# comma-separated list
 dotnet run --project .\src\SiteDownloader.App -- --urls "https://example.com,https://httpbin.org/html"

# 20 URLs with concurrency 10 (copy/paste)
 dotnet run --project .\src\SiteDownloader.App -- `
	 --max-concurrency 10 `
	 --timeout-seconds 30 `
	 --output .\downloads `
	 --log-format json `
	 --urls "https://www.microsoft.com,https://learn.microsoft.com,https://github.com,https://www.nuget.org,https://dotnet.microsoft.com,https://stackoverflow.com,https://www.wikipedia.org,https://www.bbc.com,https://www.nytimes.com,https://www.reuters.com,https://www.theguardian.com,https://www.mozilla.org,https://www.python.org,https://nodejs.org,https://www.oracle.com,https://kubernetes.io,https://www.docker.com,https://www.cloudflare.com,https://www.reddit.com,https://news.ycombinator.com"

# Same, but also download same-origin assets (CSS/JS/images) and rewrite HTML for offline viewing
 dotnet run --project .\src\SiteDownloader.App -- `
	 --download-assets `
	 --max-concurrency 10 `
	 --urls "https://example.com"
```

### 2) Provide URLs from a file

Create `urls.txt`:

```text
https://example.com
https://httpbin.org/html
```

Run:

```powershell
 dotnet run --project .\src\SiteDownloader.App -- --file .\urls.txt

# file input + concurrency 10
 dotnet run --project .\src\SiteDownloader.App -- --file .\urls.txt --max-concurrency 10
```

### Interactive input (if no args)

If you run without `--url/--urls/--file`, the app will prompt you to paste URLs (one per line) and finish with an empty line.

## Output layout

Files are written to:

- `downloads/<domain>/<path>/index.html` (or a best-effort filename based on URL/content-type)

Examples:

- `downloads/example.com/index.html`
- `downloads/httpbin.org/html/index.html`

## Logs

Logs are written in two places:

- Console: text or structured JSON (see `--log-format`)
- Files: daily rolling structured logs in `./logs/` as newline-delimited JSON

Example file name:

- `logs/log-20260112.ndjson`

## Options

- `--max-concurrency <N>` (default: 8)
- `--timeout-seconds <N>` (default: 30)
- `--output <path>` (default: `./downloads`)
- `--log-format text|json` (default: json)
- `--download-assets` (default: off; downloads same-origin assets and rewrites HTML)
- `--include-third-party-assets` (default: off; enables CDN/3rd-party assets)

## Tests

```powershell
 dotnet test
```

## Docker

Build:

```powershell
 docker build -t sitedownloader .
```

Run:

```powershell
 docker run --rm -v ${PWD}/downloads:/app/downloads sitedownloader --urls "https://example.com,https://httpbin.org/html"
```

## Notes

- This repository includes `downloads/` as a folder; typically you would add it to `.gitignore`.
- Integration tests start a local HTTP server on a dynamic port.
