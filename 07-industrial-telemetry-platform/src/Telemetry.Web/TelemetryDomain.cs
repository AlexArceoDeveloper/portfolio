using System.Collections.Concurrent;
using System.Text.Json;

namespace Telemetry.Web;

public sealed record TelemetryReading(
    Guid EventId,
    string DeviceId,
    double TemperatureCelsius,
    double VibrationMillimetersPerSecond,
    double PressureBar,
    DateTimeOffset RecordedAt,
    string Status);

public static class TelemetryRules
{
    public static string Evaluate(
        double temperatureCelsius,
        double vibrationMillimetersPerSecond,
        double pressureBar) =>
        temperatureCelsius >= 85 || vibrationMillimetersPerSecond >= 12 || pressureBar >= 14
            ? "critical"
            : temperatureCelsius >= 70 || vibrationMillimetersPerSecond >= 8 || pressureBar >= 11
                ? "warning"
                : "normal";

    public static TelemetryReading Normalize(TelemetryReading reading) => reading with
    {
        DeviceId = reading.DeviceId.Trim(),
        Status = Evaluate(
            reading.TemperatureCelsius,
            reading.VibrationMillimetersPerSecond,
            reading.PressureBar),
        RecordedAt = reading.RecordedAt.ToUniversalTime()
    };
}

public interface ITelemetryStore
{
    Task AppendAsync(TelemetryReading reading, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TelemetryReading>> LatestAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryTelemetryStore : ITelemetryStore
{
    private const int Capacity = 2_000;
    private readonly ConcurrentQueue<TelemetryReading> _readings = new();

    public Task AppendAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _readings.Enqueue(TelemetryRules.Normalize(reading));
        while (_readings.Count > Capacity)
        {
            _readings.TryDequeue(out _);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<TelemetryReading>> LatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyCollection<TelemetryReading> latest = _readings
            .OrderByDescending(reading => reading.RecordedAt)
            .Take(Math.Clamp(limit, 1, Capacity))
            .ToArray();
        return Task.FromResult(latest);
    }
}

public sealed class JsonLineTelemetryStore : ITelemetryStore
{
    private const int Capacity = 2_000;
    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentQueue<TelemetryReading> _readings = new();

    public JsonLineTelemetryStore(string path)
    {
        _path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Telemetry data path requires a directory.");
        Directory.CreateDirectory(directory);

        if (File.Exists(_path))
        {
            foreach (var line in File.ReadLines(_path).TakeLast(Capacity))
            {
                try
                {
                    var reading = JsonSerializer.Deserialize<TelemetryReading>(line);
                    if (reading is not null)
                    {
                        _readings.Enqueue(TelemetryRules.Normalize(reading));
                    }
                }
                catch (JsonException)
                {
                    // A malformed historic line is ignored; later events remain readable.
                }
            }
        }
    }

    public async Task AppendAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken = default)
    {
        var normalized = TelemetryRules.Normalize(reading);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(
                _path,
                JsonSerializer.Serialize(normalized) + Environment.NewLine,
                cancellationToken);
            _readings.Enqueue(normalized);
            while (_readings.Count > Capacity)
            {
                _readings.TryDequeue(out _);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<IReadOnlyCollection<TelemetryReading>> LatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyCollection<TelemetryReading> latest = _readings
            .OrderByDescending(reading => reading.RecordedAt)
            .Take(Math.Clamp(limit, 1, Capacity))
            .ToArray();
        return Task.FromResult(latest);
    }
}

public interface ITelemetryPublisher
{
    Task PublishAsync(TelemetryReading reading, CancellationToken cancellationToken = default);
}

public sealed class LocalTelemetryPublisher(ITelemetryStore store) : ITelemetryPublisher
{
    public Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken = default) =>
        store.AppendAsync(reading, cancellationToken);
}

public sealed class TelemetrySimulator(ITelemetryPublisher publisher)
{
    public async Task<TelemetryReading> PublishAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var temperature = Math.Round(Random.Shared.NextDouble() * 45 + 45, 1);
        var vibration = Math.Round(Random.Shared.NextDouble() * 14, 1);
        var pressure = Math.Round(Random.Shared.NextDouble() * 8 + 7, 1);
        var reading = new TelemetryReading(
            Guid.NewGuid(),
            deviceId.Trim(),
            temperature,
            vibration,
            pressure,
            DateTimeOffset.UtcNow,
            TelemetryRules.Evaluate(temperature, vibration, pressure));
        await publisher.PublishAsync(reading, cancellationToken);
        return reading;
    }
}
