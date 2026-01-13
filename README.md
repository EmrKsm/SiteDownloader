# SiteDownloader

Asynchronous multi-page downloader with Channel-based concurrency control, built with .NET 10.

## Features

✅ Download multiple web pages concurrently  
✅ Channel-based concurrency control (configurable limit)  
✅ Multiple input methods (CLI, file, interactive)  
✅ Graceful error handling and cancellation support  
✅ Structured logging (console + daily rolling files)  
✅ Smart file naming based on URL structure  
✅ Docker support  
✅ Comprehensive test coverage (66 tests)

## Prerequisites

- .NET 10 SDK (or later)

## Quick Start

### Simple Example

Download two websites:

```powershell
dotnet run --project .\src\SiteDownloader.App -- --urls "https://example.com,https://httpbin.org/html"
```

**Console output:**
```
[23:11:49 INF] Starting download: 2 urls, maxConcurrency=8, timeout=30s
[23:11:49 INF] Downloading https://example.com/
[23:11:49 INF] Downloading https://httpbin.org/html
[23:11:49 INF] Saved https://example.com/ to downloads\example.com\index.html
[23:11:50 INF] Saved https://httpbin.org/html to downloads\httpbin.org\html\index.html
[23:11:50 INF] Download complete: 2 succeeded, 0 failed
```

**Check the downloaded files:**

```powershell
# List downloaded files
Get-ChildItem -Recurse downloads

# View a downloaded file
Get-Content downloads\example.com\index.html
```

**Check the logs:**

```powershell
# View today's log file
Get-Content logs\log-20260112.log -Tail 20
```

## Technologies & Architecture

### Core Technologies

- **.NET 10** - Latest .NET framework with modern C# features
- **System.Threading.Channels** - Efficient producer-consumer pattern for concurrency control
- **IHttpClientFactory** - Proper HTTP client lifecycle management and connection pooling
- **Microsoft.Extensions.Hosting** - Generic host for dependency injection and lifecycle management
- **Serilog** - Structured logging with multiple sinks (console + file)

### Design Patterns & Approaches

**Asynchronous Programming:**
- `async/await` throughout for non-blocking I/O operations
- `Channel<T>` for bounded concurrency and backpressure handling
- `Task.WhenAll` for parallel execution with coordination
- `CancellationToken` support for graceful shutdown

**Dependency Injection:**
- Constructor injection for all dependencies
- Interface-based abstractions (IPageDownloader, IContentWriter)
- Typed HttpClient registration with IHttpClientFactory
- Service lifetime management (Singleton, Scoped, Transient)

**Clean Architecture:**
- Separation of concerns (Core logic vs. App infrastructure)
- Domain-driven design with focused responsibilities
- Testable components with interface abstractions
- No circular dependencies

**Error Handling:**
- Graceful degradation (failed downloads don't stop others)
- Structured error logging with context
- HTTP status code validation
- Timeout and cancellation support

**Testing Strategy:**
- 66 comprehensive tests (unit + integration)
- xUnit testing framework
- Integration tests with real HTTP server (Microsoft.AspNetCore.TestHost)
- Isolated test fixtures with temporary directories

## Run Options

From the repo root:

### 1) Provide URLs via CLI

```powershell
# Single URL
dotnet run --project .\src\SiteDownloader.App -- --url https://example.com

# Multiple --url entries
dotnet run --project .\src\SiteDownloader.App -- --url https://example.com --url https://httpbin.org/html

# Comma-separated list (recommended for many URLs)
dotnet run --project .\src\SiteDownloader.App -- --urls "https://example.com,https://httpbin.org/html"

# 20 URLs with custom concurrency and timeout
dotnet run --project .\src\SiteDownloader.App -- `
    --max-concurrency 10 `
    --timeout-seconds 30 `
    --output .\downloads `
    --log-format text `
    --urls "https://www.microsoft.com,https://learn.microsoft.com,https://github.com,https://www.nuget.org,https://dotnet.microsoft.com,https://stackoverflow.com,https://www.wikipedia.org,https://www.bbc.com,https://www.nytimes.com,https://www.reuters.com,https://www.theguardian.com,https://www.mozilla.org,https://www.python.org,https://nodejs.org,https://www.oracle.com,https://kubernetes.io,https://www.docker.com,https://www.cloudflare.com,https://www.reddit.com,https://news.ycombinator.com"
```

### 2) Provide URLs from a file

Create `urls.txt`:

```text
# You can add comments in the file
https://example.com
https://httpbin.org/html

# Empty lines and duplicates are automatically ignored
https://example.com
```

Run:

# File input with custom settings
dotnet run --project .\src\SiteDownloader.App -- --output .\downloads --log-format text --file .\data\urls.txt --max-concurrency 10
```

### 3) Interactive input (if no args)

If you run without `--url/--urls/--file`, the app will prompt for interactive input:

```powershell
dotnet run --project .\src\SiteDownloader.App
```

Then paste URLs (one per line) and press Enter on an empty line to start:

```
Enter URLs (one per line). Finish with an empty line:
https://example.com
https://httpbin.org/html
[press Enter on empty line]
```

## Output Structure

### Downloaded Files

Files are saved to `downloads/<domain>/<path>/` with intelligent naming:

**Directory Structure:**
```
downloads/
├── example.com/
│   └── index.html                      # Root page
├── httpbin.org/
│   └── html/
│       └── index.html                  # /html path
├── github.com/
│   └── index.html
└── www.microsoft.com/
    └── index.html
```

**Filename Rules:**
- URL with trailing slash → `index.html`
- URL without extension → `index.html` in a folder
- URL with extension → preserves the extension
- URL with query string → adds hash suffix (e.g., `index__a1b2c3d4.html`)

**Examples:**

| URL | Saved As |
|-----|----------|
| `https://example.com/` | `downloads/example.com/index.html` |
| `https://example.com/about` | `downloads/example.com/about/index.html` |
| `https://example.com/page.html` | `downloads/example.com/page.html` |
| `https://example.com/api/data` | `downloads/example.com/api/data/index.html` |
| `https://example.com/search?q=test` | `downloads/example.com/search/index__3a8f9c2e.html` |

**Inspecting Downloads:**

```powershell
# List all downloaded domains
Get-ChildItem downloads -Directory

# List all files from a specific domain
Get-ChildItem downloads\example.com -Recurse

# Count total downloaded files
(Get-ChildItem downloads -File -Recurse).Count

# View file size
Get-ChildItem downloads -File -Recurse | Select-Object FullName, Length

# View content of a specific file
Get-Content downloads\example.com\index.html
```

### Log Files

Logs are written to **two locations**:

#### 1. Console Output (Real-time)

**Text format (default):**
```
[23:11:49 INF] Starting download: 20 urls, maxConcurrency=10, timeout=30s
[23:11:49 INF] Downloading https://www.microsoft.com/
[23:11:49 INF] Downloading https://github.com/
[23:11:49 INF] Saved https://github.com/ to downloads\github.com\index.html
[23:11:50 WRN] Failed https://www.reuters.com/ with status 401
[23:11:52 INF] Download complete: 19 succeeded, 1 failed
```

**JSON format (for log aggregation):**
```powershell
dotnet run --project .\src\SiteDownloader.App -- --urls "https://example.com" --log-format json
```
```json
{"@t":"2026-01-12T23:11:49.1290000Z","@mt":"Starting download: {Count} urls","Count":1}
{"@t":"2026-01-12T23:11:49.3590000Z","@mt":"Saved {Url} to {Path}","Url":"https://example.com/","Path":"downloads\\example.com\\index.html"}
```

#### 2. Log Files (Daily Rolling)

Log files are saved to `logs/log-YYYYMMDD.log` with structured text format:

```
logs/
└── log-20260112.log
```

**Log file content:**
```
2026-01-12 23:11:49.129 +01:00 [INF] Starting download: 20 urls, maxConcurrency=10, timeout=30s, output=D:\Repo\GitRepo\SiteDownloader\downloads
2026-01-12 23:11:49.172 +01:00 [INF] Downloading "https://www.microsoft.com/"
2026-01-12 23:11:49.179 +01:00 [INF] Start processing HTTP request GET https://www.nuget.org/
2026-01-12 23:11:49.359 +01:00 [INF] Received HTTP response headers after 144.0009ms - 200
2026-01-12 23:11:49.388 +01:00 [INF] Saved "https://www.bbc.com/" to D:\Repo\GitRepo\SiteDownloader\downloads\www.bbc.com\index.html
2026-01-12 23:11:49.398 +01:00 [WRN] Failed "https://www.reuters.com/" with status 401
```

**Viewing Logs:**

```powershell
# View today's log
Get-Content logs\log-20260112.log

# View last 50 lines
Get-Content logs\log-20260112.log -Tail 50

# Follow log in real-time (PowerShell 7+)
Get-Content logs\log-20260112.log -Wait -Tail 10

# Search for errors/warnings
Select-String -Path logs\*.log -Pattern "WRN|ERR"

# Count successful downloads
Select-String -Path logs\*.log -Pattern "Saved" | Measure-Object
```

**Log Levels:**
- `INF` - Normal operation (downloads, saves)
- `WRN` - Warnings (HTTP errors like 404, 401)
- `ERR` - Errors (timeouts, network failures)

## Command-Line Options

All available options:

| Option | Description | Default | Example |
|--------|-------------|---------|---------|
| `--url <URL>` | Single URL (can repeat) | - | `--url https://example.com` |
| `--urls <LIST>` | Comma-separated URLs | - | `--urls "https://a.com,https://b.com"` |
| `--file <PATH>` | Read URLs from file | - | `--file urls.txt` |
| `--max-concurrency <N>` | Max parallel downloads | 8 | `--max-concurrency 15` |
| `--timeout-seconds <N>` | HTTP request timeout | 30 | `--timeout-seconds 60` |
| `--output <PATH>` | Download directory | `./downloads` | `--output C:\Downloads` |
| `--log-format <FORMAT>` | Console log format | `text` | `--log-format json` |
| `--help`, `-h`, `-?` | Show help | - | `--help` |

**Notes:**
- Options can be combined in any order
- Use `Ctrl+C` to cancel downloads gracefully (in-progress downloads will complete)
- File log format is always structured text, regardless of `--log-format`

## Tests

Run all tests:

```powershell
dotnet test
```

**Expected output:**
```
Test summary: total: 66, failed: 0, succeeded: 66, skipped: 0
Build succeeded in 6.3s
```

**Test Coverage:**
- 66 unit and integration tests
- CLI argument parsing (20 tests)
- URL to file path conversion (18 tests)
- URL input processing (11 tests)
- HTTP error handling (5 tests)
- Download orchestration (12 tests)

Run tests with detailed output:

```powershell
dotnet test --verbosity normal
```

## Docker

### Build Image

```powershell
docker build -t sitedownloader .
```

### Run in Container

Basic usage:

```powershell
# Windows PowerShell
docker run --rm -v ${PWD}/downloads:/app/downloads sitedownloader --urls "https://example.com,https://httpbin.org/html"

# PowerShell Core
docker run --rm -v "$(pwd)/downloads:/app/downloads" sitedownloader --urls "https://example.com,https://httpbin.org/html"
```

With custom options:

```powershell
docker run --rm `
  -v ${PWD}/downloads:/app/downloads `
  -v ${PWD}/logs:/app/logs `
  sitedownloader `
  --urls "https://example.com,https://github.com" `
  --max-concurrency 5 `
  --log-format json
```

Using a URL file:

```powershell
# Create urls.txt in current directory
docker run --rm `
  -v ${PWD}/downloads:/app/downloads `
  -v ${PWD}/urls.txt:/app/urls.txt:ro `
  sitedownloader --file /app/urls.txt
```

**Volume mounts:**
- `-v ${PWD}/downloads:/app/downloads` - Mount downloads directory
- `-v ${PWD}/logs:/app/logs` - Mount logs directory
- `-v ${PWD}/urls.txt:/app/urls.txt:ro` - Mount input file (read-only)

## Troubleshooting

### Common Issues

**1. Permission Denied (Downloads Directory)**

```powershell
# Ensure the directory exists and is writable
New-Item -ItemType Directory -Force -Path downloads
```

**2. SSL/TLS Certificate Errors**

Some websites may have certificate issues. Check logs for details:

```powershell
Select-String -Path logs\*.log -Pattern "ERR"
```

**3. Timeout Issues**

Increase timeout for slow websites:

```powershell
dotnet run --project .\src\SiteDownloader.App -- --urls "https://slow-site.com" --timeout-seconds 120
```

**4. Too Many Concurrent Requests**

Some servers rate-limit requests. Reduce concurrency:

```powershell
dotnet run --project .\src\SiteDownloader.App -- --urls "https://example.com" --max-concurrency 3
```

**5. Viewing Error Details**

Check the log file for detailed HTTP errors:

```powershell
# View all warnings and errors
Select-String -Path logs\*.log -Pattern "\[WRN\]|\[ERR\]"

# View specific failed URL
Select-String -Path logs\*.log -Pattern "Failed.*example.com" -Context 2,2
```

## Project Structure

```
SiteDownloader/
├── src/
│   ├── SiteDownloader.App/           # Entry point, DI, CLI
│   │   ├── Program.cs
│   │   └── Cli/                      # CLI argument parsing
│   │       ├── AppArguments.cs
│   │       └── UrlInputs.cs
│   └── SiteDownloader.Core/          # Core logic
│       ├── Downloading/              # Download orchestration
│       │   ├── DownloadOrchestrator.cs
│       │   └── HttpPageDownloader.cs
│       ├── IO/                       # File writing
│       │   └── FileSystemContentWriter.cs
│       └── Pathing/                  # URL to path conversion
│           └── UrlOutputPath.cs
├── tests/
│   ├── SiteDownloader.UnitTests/     # Unit tests
│   └── SiteDownloader.IntegrationTests/  # Integration tests
├── downloads/                        # Output directory (generated)
├── logs/                            # Log files (generated)
├── Dockerfile                       # Docker build configuration
├── Directory.Build.props            # Global MSBuild properties
└── README.md
```

## Advanced Usage

### Cancellation

Press `Ctrl+C` to cancel gracefully. In-progress downloads will complete:

```
[23:11:49 INF] Downloading https://example.com/
[User presses Ctrl+C]
[23:11:50 INF] Cancellation requested
[23:11:50 INF] Saved https://example.com/ to downloads\example.com\index.html
[23:11:50 INF] Download cancelled: 1 succeeded, 2 cancelled
```

### Performance Tuning

**High concurrency for many small files:**

```powershell
dotnet run --project .\src\SiteDownloader.App -- `
  --file urls.txt `
  --max-concurrency 20 `
  --timeout-seconds 15
```

**Low concurrency for large files or rate-limited servers:**

```powershell
dotnet run --project .\src\SiteDownloader.App -- `
  --urls "https://cdn.example.com/largefile1,https://cdn.example.com/largefile2" `
  --max-concurrency 2 `
  --timeout-seconds 300
```

### Batch Processing

Create multiple URL files for different domains:

```powershell
# Download GitHub repositories
dotnet run --project .\src\SiteDownloader.App -- --file github-urls.txt --max-concurrency 5

# Download documentation sites
dotnet run --project .\src\SiteDownloader.App -- --file docs-urls.txt --max-concurrency 10

# Download news sites
dotnet run --project .\src\SiteDownloader.App -- --file news-urls.txt --max-concurrency 8
```

### Log Analysis

**Count successes and failures:**

```powershell
$log = Get-Content logs\log-20260112.log
$succeeded = ($log | Select-String "Saved").Count
$failed = ($log | Select-String "Failed").Count
Write-Host "Succeeded: $succeeded, Failed: $failed"
```

**Extract all failed URLs:**

```powershell
Select-String -Path logs\*.log -Pattern 'Failed "([^"]+)"' | 
  ForEach-Object { $_.Matches.Groups[1].Value } | 
  Sort-Object -Unique
```

**Calculate average download time:**

```powershell
Select-String -Path logs\*.log -Pattern "Received HTTP response headers after ([\d.]+)ms" |
  ForEach-Object { [double]$_.Matches.Groups[1].Value } |
  Measure-Object -Average
```

## Notes

- `downloads/` and `logs/` are runtime output folders; typically you add them to `.gitignore`
- Integration tests start a local HTTP server on a dynamic port
- The application uses `Channel<T>` for efficient concurrency control
- All HTTP requests use `IHttpClientFactory` for proper connection pooling
- File names are sanitized to ensure Windows/Linux compatibility
