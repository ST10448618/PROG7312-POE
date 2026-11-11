using System.Globalization;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/history").WithTags("History");

        // Linear scan vs AVL range query at several history sizes.  e.g. /api/history/benchmark?sizes=1000,10000,100000
        group.MapGet("/benchmark", (string? sizes, int? queries) =>
        {
            var parsed = (sizes ?? "1000,10000,100000")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : throw new ArgumentException($"'{s}' is not a whole number."))
                .ToList();
            if (parsed.Count == 0) throw new ArgumentException("Give at least one size.");

            return Results.Ok(HistoryBenchmark.Run(parsed, queries ?? 300));
        });

        // How much history a sensor has, its time span, and whether its AVL tree is balanced.
        group.MapGet("/{sensorId}/summary", (string sensorId, TelemetryHistoryService history) =>
        {
            if (!history.IsReady) return Loading();
            var summary = history.Summarise(sensorId) ?? throw new KeyNotFoundException($"No history for sensor '{sensorId}'.");
            return Results.Ok(summary);
        });

        // Readings in a time range, paged. Dates are ISO 8601, e.g. from=2026-11-12T14:00:00Z&to=2026-11-12T16:00:00Z
        // Defaults to the last 24 hours, oldest first (order=desc for newest first).
        group.MapGet("/{sensorId}", (string sensorId, string? from, string? to, int? skip, int? take, string? order,
            TelemetryHistoryService history) =>
        {
            if (!history.IsReady) return Loading();

            var end = ParseUtc(to, DateTime.UtcNow, "to");
            var begin = ParseUtc(from, end.AddHours(-24), "from");
            if (begin > end) throw new ArgumentException("'from' must not be later than 'to'.");

            var descending = (order ?? "asc").ToLowerInvariant() switch
            {
                "asc" => false,
                "desc" => true,
                _ => throw new ArgumentException("'order' must be asc or desc.")
            };

            var result = history.Query(sensorId, begin, end, skip ?? 0, take ?? 100, descending)
                         ?? throw new KeyNotFoundException($"No history for sensor '{sensorId}'.");
            return Results.Ok(result);
        });
    }

    private static IResult Loading() =>
        Results.Json(new { message = "Telemetry history is still loading. Try again in a moment." }, statusCode: 503);

    private static DateTime ParseUtc(string? text, DateTime fallback, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            throw new ArgumentException($"'{name}' is not a valid date and time. Use ISO 8601, for example 2026-11-12T14:00:00Z.");

        return parsed.UtcDateTime;
    }
}