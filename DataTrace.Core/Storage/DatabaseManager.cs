using Dapper;
using Microsoft.Data.Sqlite;
using DataTrace.Shared;
using DataTrace.Core.Rules; // Добавлено для работы с SecurityAlert

namespace DataTrace.Core.Storage
{
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string dbPath = "DataTrace.db")
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public void Initialize()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Включаем WAL режим для высокой производительности
            connection.Execute("PRAGMA journal_mode=WAL;");

            // 1. Таблица процессов
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Processes (
                    Pid INTEGER,
                    ParentPid INTEGER,
                    ImageName TEXT,
                    TimestampUtc INTEGER,
                    PRIMARY KEY (Pid, TimestampUtc)
                );");

            // 2. Таблица сетевых соединений
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Connections (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Pid INTEGER,
                    ProcessName TEXT,
                    RemoteIp TEXT,
                    RemotePort INTEGER,
                    Domain TEXT,
                    TimestampUtc INTEGER
                );");

            // 3. НОВАЯ Таблица для алертов безопасности
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Alerts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RuleId TEXT,
                    Severity TEXT,
                    Description TEXT,
                    ProcessName TEXT,
                    RemoteDomain TEXT,
                    Evidence TEXT,
                    TimestampUtc INTEGER
                );");
        }

        public void InsertBatch(IEnumerable<ProcessEvent> events)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            connection.Execute(
                "INSERT OR IGNORE INTO Processes (Pid, ParentPid, ImageName, TimestampUtc) VALUES (@Pid, @ParentPid, @ImageName, @TimestampUtc)",
                events, transaction);

            transaction.Commit();
        }

        public void InsertConnection(uint pid, string processName, string remoteIp, uint remotePort, string domain, long timestamp)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            connection.Execute(
                "INSERT INTO Connections (Pid, ProcessName, RemoteIp, RemotePort, Domain, TimestampUtc) VALUES (@Pid, @ProcessName, @RemoteIp, @RemotePort, @Domain, @Timestamp)",
                new { Pid = pid, ProcessName = processName, RemoteIp = remoteIp, RemotePort = remotePort, Domain = domain, Timestamp = timestamp });
        }

        // НОВЫЙ Метод для сохранения алертов
        public void InsertAlert(SecurityAlert alert)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            connection.Execute(
                "INSERT INTO Alerts (RuleId, Severity, Description, ProcessName, RemoteDomain, Evidence, TimestampUtc) VALUES (@RuleId, @Severity, @Description, @ProcessName, @RemoteDomain, @Evidence, @TimestampUtc)",
                new
                {
                    alert.RuleId,
                    alert.Severity,
                    alert.Description,
                    alert.ProcessName,
                    alert.RemoteDomain,
                    alert.Evidence,
                    TimestampUtc = (long)(alert.Timestamp - DateTime.UnixEpoch).TotalMilliseconds
                });
        }
    }
}