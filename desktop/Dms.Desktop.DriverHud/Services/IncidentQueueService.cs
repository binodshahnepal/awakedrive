using System.IO;
using System.Text.Json;
using Dms.Client.Api;
using Dms.Shared.Contracts.Telemetry;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>
/// Offline incident queue: an incident is always written to a local SQLite
/// db first, then a background loop tries to POST each queued row to
/// TelemetryController and deletes it on success. If the device is in a
/// cellular/network dead zone, incidents just accumulate locally until the
/// next successful sync — same contract as the mobile app's Room queue.
/// </summary>
public class IncidentQueueService : IAsyncDisposable
{
    private readonly DmsApiClient _api;
    private readonly ILogger<IncidentQueueService> _logger;
    private readonly string _dbPath;
    private readonly string _connectionString;
    private CancellationTokenSource? _cts;
    private Task? _syncLoop;

    public event Action<int>? PendingCountChanged;
    public event Action<IncidentReport>? IncidentSynced;

    public IncidentQueueService(DmsApiClient api, ILogger<IncidentQueueService> logger)
    {
        _api = api;
        _logger = logger;

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AwakeDrive");
        Directory.CreateDirectory(directory);
        _dbPath = Path.Combine(directory, "incident-queue.db");
        _connectionString = $"Data Source={_dbPath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PendingIncidents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PayloadJson TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public async Task EnqueueAsync(IncidentReport report)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO PendingIncidents (PayloadJson, CreatedAtUtc) VALUES ($payload, $createdAt);";
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(report));
        command.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();

        PendingCountChanged?.Invoke(await GetPendingCountAsync());
    }

    public async Task<int> GetPendingCountAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PendingIncidents;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public void StartBackgroundSync(TimeSpan? interval = null)
    {
        if (_syncLoop is not null)
        {
            return;
        }
        _cts = new CancellationTokenSource();
        var syncInterval = interval ?? TimeSpan.FromSeconds(15);
        _syncLoop = Task.Run(() => SyncLoop(syncInterval, _cts.Token));
    }

    private async Task SyncLoop(TimeSpan interval, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await FlushOnceAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Incident queue flush failed; will retry");
            }

            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task FlushOnceAsync(CancellationToken ct)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = "SELECT Id, PayloadJson FROM PendingIncidents ORDER BY Id ASC LIMIT 20;";

        var rows = new List<(long Id, string Payload)>();
        await using (var reader = await selectCommand.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var (id, payloadJson) in rows)
        {
            var report = JsonSerializer.Deserialize<IncidentReport>(payloadJson);
            if (report is null)
            {
                continue; // corrupt row — drop it rather than blocking the queue forever
            }

            try
            {
                await _api.PostIncidentAsync(report, ct);
            }
            catch (DmsApiException)
            {
                // Backend unreachable or rejected it — stop this pass, leave
                // remaining rows queued, try again next interval.
                return;
            }

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM PendingIncidents WHERE Id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", id);
            await deleteCommand.ExecuteNonQueryAsync(ct);

            IncidentSynced?.Invoke(report);
            PendingCountChanged?.Invoke(await GetPendingCountAsync());
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        if (_syncLoop is not null)
        {
            try
            {
                await _syncLoop;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
        _cts?.Dispose();
    }
}
