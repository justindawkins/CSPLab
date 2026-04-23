using System.Text.Json;
using CspLab.ReportServer.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICspViolationStore, InMemoryCspViolationStore>();

// Allow the test site origin to POST reports (CORS)
builder.Services.AddCors(options =>
{
    options.AddPolicy("CspReports", policy =>
        policy.WithOrigins("http://localhost:5010", "https://localhost:7010")
              .WithMethods("POST", "GET", "DELETE")
              .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors("CspReports");

// ── POST /csp-report ─────────────────────────────────────────────────────────
// Accepts both:
//   application/reports+json  (Reporting API — array)
//   application/csp-report    (legacy report-uri — single object)
app.MapPost("/csp-report", async (
    HttpRequest request,
    ICspViolationStore store,
    ILogger<Program> logger) =>
{
    List<CspReport>? reports = null;

    try
    {
        using var doc = await JsonDocument.ParseAsync(request.Body);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            reports = root.Deserialize<List<CspReport>>(JsonOptions.Default);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            var legacy = root.Deserialize<LegacyCspReport>(JsonOptions.Default);
            reports = legacy is null ? null : [CspReport.FromLegacy(legacy)];
        }
    }
    catch (JsonException ex)
    {
        logger.LogWarning(ex, "Failed to parse CSP report payload");
        return Results.BadRequest("Invalid JSON payload");
    }

    if (reports is null || reports.Count == 0)
        return Results.BadRequest("Empty or unrecognized CSP report payload");

    foreach (var report in reports)
    {
        store.Add(report);

        logger.LogWarning(
            "CSP [{Disposition}] directive={Directive} blocked={Blocked} doc={Document} {Source}:{Line}:{Col}",
            report.Body.Disposition ?? "enforce",
            report.Body.EffectiveDirective,
            report.Body.BlockedURL ?? "(inline)",
            report.Body.DocumentURL,
            report.Body.SourceFile,
            report.Body.LineNumber,
            report.Body.ColumnNumber);
    }

    return Results.NoContent();
});

// ── GET /csp-report ──────────────────────────────────────────────────────────
app.MapGet("/csp-report", (ICspViolationStore store, int count = 100) =>
    Results.Ok(store.GetRecent(count)));

// ── GET /csp-report/summary ──────────────────────────────────────────────────
app.MapGet("/csp-report/summary", (ICspViolationStore store) =>
{
    var all = store.GetRecent(1000);
    var summary = new
    {
        Total = all.Count,
        ByDirective = all
            .GroupBy(r => r.Body.EffectiveDirective ?? "unknown")
            .Select(g => new { Directive = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count),
        ByDisposition = all
            .GroupBy(r => r.Body.Disposition ?? "enforce")
            .Select(g => new { Disposition = g.Key, Count = g.Count() }),
        Recent = all.Take(10)
    };
    return Results.Ok(summary);
});

// ── DELETE /csp-report ───────────────────────────────────────────────────────
app.MapDelete("/csp-report", (ICspViolationStore store) =>
{
    store.Clear();
    return Results.NoContent();
});

app.Run();
