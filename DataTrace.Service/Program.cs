using DataTrace.Core.Ingestion;
using DataTrace.Core.Storage;
using DataTrace.Core.Processing;
using DataTrace.Core.Rules; // Добавьте, если нужно
using Dapper;
using Microsoft.Data.Sqlite;
using DataTrace.Shared;

namespace DataTrace.Service
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<DatabaseManager>(sp => new DatabaseManager("DataTrace.db"));
            builder.Services.AddSingleton<ConnectionTracker>();
            builder.Services.AddSingleton<PipeReader>();

            builder.Services.AddHostedService<IngestionWorker>();
            builder.Services.AddHostedService<DpiWorker>();

            var app = builder.Build();

            var db = app.Services.GetRequiredService<DatabaseManager>();
            db.Initialize();

            // --- ЭНДПОИНТЫ API ---

            // 1. Дерево процессов
            app.MapGet("/api/processes/tree", () =>
            {
                using var connection = new SqliteConnection("Data Source=DataTrace.db");
                var events = connection.Query<ProcessEvent>("SELECT Pid, ParentPid, ImageName, TimestampUtc FROM Processes ORDER BY TimestampUtc DESC LIMIT 1000").ToList();
                var builder = new ProcessTreeBuilder();
                var tree = builder.BuildTree(events);
                return Results.Ok(tree);
            });

            // 2. Плоский список процессов
            app.MapGet("/api/processes", () =>
            {
                using var connection = new SqliteConnection("Data Source=DataTrace.db");
                var events = connection.Query<ProcessEvent>("SELECT * FROM Processes ORDER BY TimestampUtc DESC LIMIT 1000").ToList();
                return Results.Ok(events);
            });

            // 3. АЛЕРТЫ (Инциденты)
            app.MapGet("/api/alerts", () =>
            {
                using var connection = new SqliteConnection("Data Source=DataTrace.db");
                var alerts = connection.Query<SecurityAlert>("SELECT * FROM Alerts ORDER BY TimestampUtc DESC LIMIT 100").ToList();
                return Results.Ok(alerts);
            });
            // --------------------

            Console.WriteLine("[Service] Запуск Web API на http://localhost:5000");
            await app.RunAsync("http://localhost:5000");
        }
    }

    public class IngestionWorker : BackgroundService
    {
        private readonly PipeReader _reader;
        public IngestionWorker(PipeReader reader) { _reader = reader; }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) => await _reader.StartReadingAsync(stoppingToken);
    }
}