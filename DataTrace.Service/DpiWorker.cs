using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using DataTrace.Core.Dpi;
using DataTrace.Core.Processing; // Добавили для ConnectionTracker

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
            // ВАЖНО: Убедитесь, что здесь ваше реальное имя интерфейса
            _dpiEngine.Start("\\Device\\NPF_{4B55C5E1-AF04-4C58-9470-30E93E5ACEA6}");

            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _dpiEngine.Stop();
            await base.StopAsync(cancellationToken);
        }
    }
}