using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using DataTrace.Core.Dpi;
using DataTrace.Core.Processing; // Добавили для ConnectionTracker
using System.Linq;

namespace DataTrace.Service
{
    public class DpiWorker : BackgroundService
    {
        private readonly DpiEngine _dpiEngine;
        private readonly ConnectionTracker _tracker; // Добавили трекер

        // Внедряем трекер через конструктор (DI)
        public DpiWorker(ConnectionTracker tracker)
        {
            _tracker = tracker;
            _dpiEngine = new DpiEngine();

            // Обновленный коллбэк: теперь принимает IP-адрес
            _dpiEngine.OnDomainDetected += (source, ip, domain) =>
            {
                // Сохраняем связку в память!
                _tracker.AddDomain(ip, domain);

                // Для наглядности выводим в консоль
                Console.WriteLine($"[DPI] ({source}) {ip} -> {domain}");
            };
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Автоматически ищем активный интерфейс (Wi-Fi или Ethernet)
            string activeInterface = GetActiveInterface();

            if (string.IsNullOrEmpty(activeInterface))
            {
                Console.WriteLine("[DPI] Не удалось найти активный сетевой интерфейс!");
                return Task.CompletedTask;
            }

            _dpiEngine.Start(activeInterface);
            return Task.CompletedTask;
        }

        private string GetActiveInterface()
        {
            var devices = SharpPcap.CaptureDeviceList.Instance;

            // Ищем интерфейс, который не является виртуальным, не Loopback и имеет IP
            // (Упрощенная логика: ищем по ключевым словам в описании)
            foreach (var dev in devices)
            {
                if (dev.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                    dev.Description.Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
                {
                    // Проверяем, что интерфейс действительно активен (опционально)
                    return dev.Name;
                }
            }

            // Fallback: если ничего не нашли, берем первый попавшийся не виртуальный
            return devices.FirstOrDefault(d => !d.Description.Contains("Virtual") && !d.Description.Contains("Loopback"))?.Name ?? "";
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _dpiEngine.Stop();
            await base.StopAsync(cancellationToken);
        }
    }
}