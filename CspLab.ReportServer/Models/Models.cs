using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CspLab.ReportServer.Models;

// ── Reporting API shape (application/reports+json) ───────────────────────────

public record CspReport
{
    [JsonPropertyName("age")]
    public int Age { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("user_agent")]
    public string UserAgent { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public CspViolationBody Body { get; init; } = new();

    // Timestamp added server-side for display in the dashboard
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    public static CspReport FromLegacy(LegacyCspReport legacy) => new()
    {
        Type = "csp-violation",
        Url  = legacy.CspReport.DocumentUri ?? string.Empty,
        Body = new CspViolationBody
        {
            BlockedURL         = legacy.CspReport.BlockedUri,
            Disposition        = "enforce",
            DocumentURL        = legacy.CspReport.DocumentUri,
            EffectiveDirective = legacy.CspReport.ViolatedDirective,
            OriginalPolicy     = legacy.CspReport.OriginalPolicy,
            Referrer           = legacy.CspReport.Referrer,
            StatusCode         = legacy.CspReport.StatusCode,
            SourceFile         = legacy.CspReport.SourceFile,
            LineNumber         = legacy.CspReport.LineNumber,
            ColumnNumber       = legacy.CspReport.ColumnNumber,
        }
    };
}

public record CspViolationBody
{
    [JsonPropertyName("blockedURL")]
    public string? BlockedURL { get; init; }

    /// <summary>"enforce" | "reporting"</summary>
    [JsonPropertyName("disposition")]
    public string? Disposition { get; init; }

    [JsonPropertyName("documentURL")]
    public string? DocumentURL { get; init; }

    [JsonPropertyName("effectiveDirective")]
    public string? EffectiveDirective { get; init; }

    [JsonPropertyName("lineNumber")]
    public int? LineNumber { get; init; }

    [JsonPropertyName("columnNumber")]
    public int? ColumnNumber { get; init; }

    [JsonPropertyName("originalPolicy")]
    public string? OriginalPolicy { get; init; }

    [JsonPropertyName("referrer")]
    public string? Referrer { get; init; }

    [JsonPropertyName("sample")]
    public string? Sample { get; init; }

    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; init; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; init; }
}

// ── Legacy report-uri shape (application/csp-report) ─────────────────────────

public record LegacyCspReport
{
    [JsonPropertyName("csp-report")]
    public LegacyCspReportBody CspReport { get; init; } = new();
}

public record LegacyCspReportBody
{
    [JsonPropertyName("document-uri")]        public string? DocumentUri       { get; init; }
    [JsonPropertyName("referrer")]            public string? Referrer          { get; init; }
    [JsonPropertyName("blocked-uri")]         public string? BlockedUri        { get; init; }
    [JsonPropertyName("violated-directive")]  public string? ViolatedDirective { get; init; }
    [JsonPropertyName("original-policy")]     public string? OriginalPolicy    { get; init; }
    [JsonPropertyName("source-file")]         public string? SourceFile        { get; init; }
    [JsonPropertyName("status-code")]         public int?    StatusCode        { get; init; }
    [JsonPropertyName("line-number")]         public int?    LineNumber        { get; init; }
    [JsonPropertyName("column-number")]       public int?    ColumnNumber      { get; init; }
}

// ── JSON options ──────────────────────────────────────────────────────────────

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

// ── Store ─────────────────────────────────────────────────────────────────────

public interface ICspViolationStore
{
    void Add(CspReport report);
    IReadOnlyList<CspReport> GetRecent(int count);
    void Clear();
}

/// <summary>Thread-safe in-memory ring buffer. Swap for EF Core / Service Bus as needed.</summary>
public sealed class InMemoryCspViolationStore : ICspViolationStore
{
    private readonly ConcurrentQueue<CspReport> _queue = new();
    private const int MaxCapacity = 1_000;

    public void Add(CspReport report)
    {
        _queue.Enqueue(report);
        while (_queue.Count > MaxCapacity)
            _queue.TryDequeue(out _);
    }

    public IReadOnlyList<CspReport> GetRecent(int count) =>
        [.. _queue.TakeLast(count).Reverse()];

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
