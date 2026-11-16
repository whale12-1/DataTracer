using DataTrace.Core.Processing;
using DataTrace.Core.Security;
using DataTrace.Core.Storage;
using DataTrace.Shared;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DataTrace.Core.Ingestion
{
    public class PipeReader
    {
        private const string PipeName = "DataTraceEvents";
        private readonly Channel<ProcessEvent> _channel;
        private readonly DatabaseManager _db;
        private readonly ConnectionTracker _tracker;

        // Теперь у нас есть только модульный менеджер безопасности
        private readonly SecurityManager _securityManager;

        public PipeReader(DatabaseManager db, ConnectionTracker tracker)
        {
            _db = db;
            _tracker = tracker;

            // Инициализируем менеджер (он внутри себя создаст DlpDetector, KeyloggerDetector и т.д.)
            _securityManager = new SecurityManager();

            _channel = Channel.CreateBounded<ProcessEvent>(new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public async Task StartReadingAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => ConsumeChannelAsync(cancellationToken));

            Console.WriteLine("[Core] Ожидание подключения от C++ коллектора...");

            while (!cancellationToken.IsCancellationRequested)
            {
                using var pipeServer = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(cancellationToken);
                Console.WriteLine("[Core] C++ коллектор подключен!");

                try
                {
                    while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        byte[] lengthBuffer = new byte[4];
                        int bytesRead = await pipeServer.ReadAsync(lengthBuffer, 0, 4, cancellationToken);
                        if (bytesRead == 0) break;

                        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                        if (messageLength <= 0 || messageLength > 1024 * 1024) continue;

                        byte[] messageBuffer = new byte[messageLength];
                        int totalRead = 0;
                        while (totalRead < messageLength)
                        {
                            int read = await pipeServer.ReadAsync(messageBuffer, totalRead, messageLength - totalRead, cancellationToken);
                            if (read == 0) throw new EndOfStreamException();
                            totalRead += read;
                        }

                        // Распаковываем конверт
                        var wrapper = EventWrapper.Parser.ParseFrom(messageBuffer);

                        // =========================================================
                        // 1. МОДУЛЬНЫЙ АНАЛИЗ БЕЗОПАСНОСТИ (для всех типов событий)
                        // =========================================================
                        var alerts = _securityManager.ProcessEvent(wrapper);
                        foreach (var alert in alerts)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"🚨 [ALERT: {alert.Severity}] {alert.Description}");
                            Console.WriteLine($"   Процесс: {alert.ProcessName} | Доказательство: {alert.Evidence}");
                            Console.ResetColor();

                            // Сохраняем в БД
                            try { _db.InsertAlert(alert); } catch { /* ignore db error */ }
                        }

                        // =========================================================
                        // 2. ОБРАБОТКА СОБЫТИЙ ДЛЯ ТРЕКИНГА (Процессы и Сеть)
                        // =========================================================
                        if (wrapper.PayloadCase == EventWrapper.PayloadOneofCase.ProcessEvent)
                        {
                            var proc = wrapper.ProcessEvent;
                            _tracker.AddProcess(proc.Pid, proc.ImageName);
                            await _channel.Writer.WriteAsync(proc, cancellationToken);
                        }
                        else if (wrapper.PayloadCase == EventWrapper.PayloadOneofCase.NetworkEvent)
                        {
                            var net = wrapper.NetworkEvent;
                            string processName = _tracker.GetProcessName(net.Pid);
                            string domain = _tracker.GetDomain(net.RemoteIp);

                            Console.WriteLine($"[СЕТЬ] {processName} (PID: {net.Pid}) -> {net.RemoteIp}:{net.RemotePort} [{domain}]");
                            _db.InsertConnection(net.Pid, processName, net.RemoteIp, net.RemotePort, domain, net.TimestampUtc);
                        }
                        // Примечание: ClipboardEvent и Win32kEvent больше не нужно обрабатывать здесь.
                        // SecurityManager и его детекторы (DlpDetector, KeyloggerDetector) сделали это выше.
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Core] Ошибка: {ex.Message}");
                }
                finally
                {
                    Console.WriteLine("[Core] Подключение потеряно...");
                }
            }
        }

        private async Task ConsumeChannelAsync(CancellationToken cancellationToken)
        {
            var buffer = new List<ProcessEvent>();

            while (!cancellationToken.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(1000);

                try
                {
                    var evt = await _channel.Reader.ReadAsync(timeoutCts.Token);
                    buffer.Add(evt);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Core] Ошибка при чтении канала: {ex.Message}");
                }

                if (buffer.Count >= 50 || buffer.Count > 0)
                {
                    _db.InsertBatch(buffer);
                    buffer.Clear();
                }
            }
        }
    }
}