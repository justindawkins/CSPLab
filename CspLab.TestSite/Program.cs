var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (ctx, next) =>
{
    // Choose report server URL that matches the current request scheme to avoid mixed-content blocking.
    var reportUrl = ctx.Request.IsHttps ? "https://localhost:7000/csp-report" : "http://localhost:5000/csp-report";
    var reportOrigin = new Uri(reportUrl).GetLeftPart(UriPartial.Authority);

    // Reporting-Endpoints (newer header) - endpoints by name
    ctx.Response.Headers.Append(
        "Reporting-Endpoints",
        $"csp-endpoint=\"{reportUrl}\"");

    // Report-To header (required for CSP 'report-to' directive) — JSON describing the group
    var reportToJson = System.Text.Json.JsonSerializer.Serialize(new
    {
        group = "csp-endpoint",
        max_age = 86400,
        endpoints = new[] { new { url = reportUrl } }
    });
    ctx.Response.Headers.Append("Report-To", reportToJson);

    // Allow 'unsafe-inline' for styles and scripts in this local test site so the
    // inline <style> and <script> blocks in wwwroot/*.html render and run.
    // Also permit connecting to the report server origin in connect-src so the
    // dashboard's fetches to the report server are allowed by the CSP.
    ctx.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        $"img-src 'self'; " +
        $"connect-src 'self' {reportOrigin}; " +
        "frame-src 'none'; " +
        $"report-to csp-endpoint; " +
        $"report-uri {reportUrl}");   // fallback for older browsers

    await next();
});

// Debug endpoint to return the effective CSP-related headers for the current request
app.MapGet("/debug/csp-headers", (HttpContext ctx) =>
{
    // Read the headers that were appended by the middleware above
    ctx.Response.Headers.TryGetValue("Content-Security-Policy", out var csp);
    ctx.Response.Headers.TryGetValue("Report-To", out var reportTo);
    ctx.Response.Headers.TryGetValue("Reporting-Endpoints", out var reportingEndpoints);

    var result = new
    {
        ContentSecurityPolicy = csp.ToString(),
        ReportTo = reportTo.ToString(),
        ReportingEndpoints = reportingEndpoints.ToString(),
        // also include which report server URL / origin was chosen for convenience
        ReportServer = ctx.Request.IsHttps ? "https://localhost:7000/csp-report" : "http://localhost:5000/csp-report",
        ReportOrigin = ctx.Request.IsHttps ? "https://localhost:7000" : "http://localhost:5000"
    };

    return Results.Json(result);
});

// ── Serve static files from wwwroot/ ─────────────────────────────────────────
app.UseDefaultFiles();   // serves index.html for /
app.UseStaticFiles();

app.Run();
