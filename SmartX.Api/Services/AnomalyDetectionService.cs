using SmartX.Api.Domain.Collections;

namespace SmartX.Api.Services;

public enum AnomalyColour { Green, Amber, Red, Blue }

public record AnomalyResult(string SensorId, double Value, double Score, AnomalyColour Colour, DateTime Timestamp);

public class AnomalyDetectionService
{
    private readonly Dictionary<string, RingBuffer<double>> _windows = new();
    private readonly object _lock = new();
    private const int WindowSize = 20;

    public AnomalyResult Score(string sensorId, double value, bool sensorConnected = true)
    {
        if (!sensorConnected)
            return new AnomalyResult(sensorId, value, 0, AnomalyColour.Blue, DateTime.UtcNow);

        lock (_lock)
        {
            if (!_windows.TryGetValue(sensorId, out var window))
            {
                window = new RingBuffer<double>(WindowSize);
                _windows[sensorId] = window;
            }

            double score = 0;
            if (window.Count >= 5)
            {
                var values = window.ToArray();
                var mean = values.Average();
                var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Length);
                score = stdDev < 0.0001 ? 0 : (value - mean) / stdDev;
            }

            window.Add(value);

            var absScore = Math.Abs(score);
            var colour = absScore switch
            {
                > 2.0 => AnomalyColour.Red,
                > 1.0 => AnomalyColour.Amber,
                _ => AnomalyColour.Green
            };

            return new AnomalyResult(sensorId, value, Math.Round(score, 2), colour, DateTime.UtcNow);
        }
    }

    public static string SeverityFor(AnomalyColour colour) => colour switch
    {
        AnomalyColour.Red => "Critical",
        AnomalyColour.Amber => "Warning",
        AnomalyColour.Blue => "Disconnected",
        _ => "Normal"
    };
}