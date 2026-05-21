# CLAUDE.md — CSPLab

## Project Overview

CSPLab is an ASP.NET Core 8 learning/demo solution for testing and debugging **Content Security Policy (CSP) violation reporting**. It has two runnable web applications that work together: one receives violation reports, one serves pages that intentionally trigger violations.

**Solution file:** `CspLab.sln`

---

## Architecture

```
CspLab/
├── CspLab.ReportServer/     # Receives & stores CSP violation reports
│   ├── Program.cs           # Minimal API endpoints
│   └── Models/Models.cs     # Data contracts + in-memory store
└── CspLab.TestSite/         # Serves static pages with CSP headers
    ├── Program.cs           # Middleware + static file serving
    └── wwwroot/             # HTML test pages
        ├── index.html       # Home / CSP config overview
        ├── page2.html       # Inline script/style/eval violations
        ├── page3.html       # External resource violations
        └── dashboard.html   # Live violation dashboard (polls every 3s)
```

### Data Flow

1. Browser loads a page from **TestSite** (:5010) — CSP headers are injected by middleware.
2. Browser detects a violation and POSTs a report to **ReportServer** (:5000/csp-report).
3. ReportServer stores the report in an in-memory ring buffer (max 1,000 entries).
4. **dashboard.html** polls `GET /csp-report` every 3 seconds and displays live results.

---

## Tech Stack

| Aspect | Details |
|--------|---------|
| Language | C# 12 |
| Framework | ASP.NET Core 8 (`net8.0`) |
| Build system | MSBuild (`CspLab.sln`) |
| API pattern | Minimal APIs (no controllers) |
| JSON | `System.Text.Json` |
| DI | Built-in `IServiceCollection` |
| NuGet packages | None — framework defaults only |
| Tests | None (manual browser-based) |
| CI/CD | None |

---

## Ports & URLs

| Service | HTTP | HTTPS |
|---------|------|-------|
| ReportServer | http://localhost:5000 | https://localhost:7000 |
| TestSite | http://localhost:5010 | https://localhost:7010 |

These ports are hardcoded in `launchSettings.json` and referenced in CORS/report endpoint configuration. Change them consistently in both places if you need different ports.

---

## Running the Project

### Visual Studio (recommended)

1. Right-click the Solution → **Set Startup Projects**
2. Select **Multiple startup projects**
3. Set both projects to **Start**
4. Press **F5**

TestSite opens automatically. ReportServer runs silently in the background.

### CLI

Run both projects simultaneously in separate terminals:

```bash
# Terminal 1
dotnet run --project CspLab.ReportServer

# Terminal 2
dotnet run --project CspLab.TestSite
```

### Build only

```bash
dotnet build
```

---

## Report Server Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/csp-report` | Receives violation reports from browsers |
| GET | `/csp-report` | Returns recent violations as JSON array |
| GET | `/csp-report/summary` | Aggregated counts by directive and disposition |
| DELETE | `/csp-report` | Clears the in-memory store |
| GET | `/debug/csp-headers` | (TestSite) Returns active CSP headers for inspection |

---

## Key Source Files

### `CspLab.ReportServer/Models/Models.cs`

All data contracts live here:

- `CspReport` / `CspViolationBody` — Reporting API (W3C) format
- `LegacyCspReport` / `LegacyCspReportBody` — Legacy `report-uri` format
- `ICspViolationStore` — Interface for the violation store (swap for persistence here)
- `InMemoryCspViolationStore` — `ConcurrentQueue<T>` ring buffer, max 1,000 entries
- `JsonOptions` — Shared `JsonSerializerOptions` (case-insensitive, nulls omitted)

### `CspLab.ReportServer/Program.cs`

- Registers `ICspViolationStore` as a singleton
- Configures CORS to allow TestSite origins
- Defines all four API endpoints
- POST endpoint handles both Reporting API (array body) and legacy (object body) formats

### `CspLab.TestSite/Program.cs`

- Custom middleware injects CSP + `Report-To` + `Reporting-Endpoints` response headers
- Report endpoint URL is scheme-aware (matches http vs https of the incoming request)
- Serves static files from `wwwroot/` with default document (`index.html`)

---

## Code Conventions

### C# Style

- **Nullable reference types** enabled — always annotate nullability correctly.
- **Implicit usings** enabled — don't add redundant `using System;` etc.
- **Records** for immutable data models (used throughout Models.cs).
- **PascalCase** for types, methods, properties; **camelCase** for local variables.
- **Async/await** throughout — use `async Task` for I/O handlers.
- Keep `Program.cs` minimal — data contracts and abstractions go in `Models/`.

### JSON

- Use `System.Text.Json` (not Newtonsoft).
- Use `[JsonPropertyName("kebab-case-name")]` attributes to match browser report format.
- The shared `JsonOptions` instance (case-insensitive, ignore nulls) is registered in DI — use it via `app.Services`.

### Error Handling

- Wrap JSON parsing in try-catch; log warnings for malformed input, return 400.
- Don't add try-catch for scenarios that cannot fail (internal method calls, DI-resolved services).

### HTML / Frontend

- 4-space indentation in HTML, 2-space for JSON (enforced by `.editorconfig`).
- No JS framework — plain vanilla JS only.
- Inline `<script>` and `<style>` tags are intentional on test pages (they trigger CSP violations by design).
- Dashboard polls via `setInterval` + `fetch` — no WebSockets.

### Line Endings & Encoding

- CRLF line endings (`.editorconfig` enforces this).
- UTF-8 charset everywhere.
- No trailing whitespace; files end with a newline.

---

## Extending the Project

### Swapping in a Persistent Store

Implement `ICspViolationStore` (defined in `Models/Models.cs`) and register it in `CspLab.ReportServer/Program.cs`:

```csharp
// Replace this:
builder.Services.AddSingleton<ICspViolationStore, InMemoryCspViolationStore>();

// With, e.g.:
builder.Services.AddSingleton<ICspViolationStore, SqliteCspViolationStore>();
```

### Adding a New Test Page

1. Create `wwwroot/pageN.html` following the existing page structure.
2. Add a link to it from `index.html`.
3. The CSP headers are applied globally by middleware — no per-page configuration needed.

### Changing the CSP Policy

Edit the middleware in `CspLab.TestSite/Program.cs`. The policy is assembled as a string — keep `report-to` and `report-uri` directives pointing at the ReportServer endpoint.

---

## Important Behaviors to Know

- **In-memory store resets on restart** — all recorded violations are lost when ReportServer stops.
- **Browsers batch reports** — violations may arrive seconds after the triggering page load, or only on navigation away. The 3-second dashboard poll reflects this lag.
- **Dual report format support** — both the modern Reporting API (`report-to`) and legacy `report-uri` are set. The server normalizes legacy reports to the modern shape before storing.
- **CORS** — ReportServer explicitly allows TestSite origins. If you change TestSite ports, update the CORS policy in `ReportServer/Program.cs`.
- **No authentication** — this is a local-only dev/demo tool. Do not expose it publicly.
