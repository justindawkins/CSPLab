# CspLab

A Visual Studio solution for testing and debugging ASP.NET Core CSP violation reporting.

## Projects

| Project | Port | Purpose |
|---|---|---|
| `CspLab.ReportServer` | http://localhost:5000 | Receives CSP violation reports, exposes a REST API |
| `CspLab.TestSite` | http://localhost:5010 | Serves static test pages with CSP headers set |

## Running in Visual Studio

1. Right-click the **Solution** > **Set Startup Projects**
2. Select **Multiple startup projects**
3. Set both `CspLab.ReportServer` and `CspLab.TestSite` to **Start**
4. Press **F5**

The test site opens automatically at http://localhost:5010.
The report server runs silently at http://localhost:5000.

## Test Pages

- **/** — Home, confirms the config
- **/page2.html** — Inline script / style / eval violations
- **/page3.html** — External resource violations (script, img, fetch, iframe, font)
- **/dashboard.html** — Live violation dashboard, polls the report server every 3s

## Report Server Endpoints

| Method | Path | Description |
|---|---|---|
| POST | /csp-report | Receives violation reports from browsers |
| GET | /csp-report | Returns recent violations (JSON) |
| GET | /csp-report/summary | Aggregated counts by directive and disposition |
| DELETE | /csp-report | Clears the in-memory store |

## Notes

- The in-memory store holds the last 1,000 violations and resets on restart.
- Browsers batch and send reports asynchronously — reports may arrive seconds after the violation fires,
  or on page navigation. The dashboard polls every 3 seconds.
- Both `report-to` (Reporting API) and `report-uri` (legacy fallback) are set in the CSP header.
- To swap in a persistent store, implement `ICspViolationStore` and register it in `ReportServer/Program.cs`.
