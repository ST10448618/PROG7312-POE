using System.Globalization;
using SmartX.Api.Domain.Entities;

namespace SmartX.Api.Infrastructure.Seeding;

public static class TelemetryHistoryGenerator
{
    public static IEnumerable<TelemetryLog> Generate(MeshSensorSpec sensor, DateTime endUtc, int days, int intervalSeconds)
    {
        var rnd = new Random(StableSeed(sensor.Mac));
        var readings = (int)(TimeSpan.FromDays(days).TotalSeconds / intervalSeconds);
        var start = endUtc.AddSeconds(-(double)readings * intervalSeconds);
        var maxJitter = Math.Max(0, intervalSeconds / 2 - 1); // jitter stays under half an interval, so time only moves forward

        for (var i = 0; i < readings; i++)
        {
            var at = start.AddSeconds((double)i * intervalSeconds + (maxJitter > 0 ? rnd.Next(0, maxJitter + 1) : 0));
            var hour = at.Hour + at.Minute / 60.0;

            var value = Math.Round(Math.Clamp(DailyShape(sensor.Category, hour) + Noise(rnd, sensor.Category), 0, 100), 2);

            yield return new TelemetryLog
            {
                SensorId = sensor.Mac,
                ValueType = "Single",
                RawValue = value.ToString(CultureInfo.InvariantCulture),
                Unit = "%",
                Timestamp = DateTime.SpecifyKind(at, DateTimeKind.Utc)
            };
        }
    }

    private static double DailyShape(string category, double hour) => category switch
    {
        // Soil moisture: highest mid-morning after the dawn irrigation, lowest in the evening.
        "Environmental" => 55 + 18 * Math.Sin((hour - 3) / 24.0 * 2 * Math.PI),

        // Load: a broad bump around mid-afternoon.
        "Power Consumption" => 30 + 45 * Math.Exp(-Math.Pow(hour - 14, 2) / (2 * 3.5 * 3.5)),

        // Actuator duty: runs hard in the morning and evening irrigation windows, near idle otherwise.
        "Actuator" => hour is >= 6 and < 8 or >= 17 and < 19 ? 82 : 6,

        _ => 50
    };

    private static double Noise(Random rnd, string category)
    {
        var sigma = category == "Power Consumption" ? 3.0 : 2.0;
        // Box-Muller transform: two uniform numbers become one normally distributed one.
        var u1 = 1.0 - rnd.NextDouble();
        var u2 = rnd.NextDouble();
        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>string.GetHashCode is randomised per process, so build a stable seed from the characters instead.</summary>
    private static int StableSeed(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in text) hash = hash * 31 + c;
            return hash;
        }
    }
}