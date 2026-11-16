using System.Collections.Concurrent;
using System.Diagnostics;

namespace DataTrace.Core.Processing
{
    public class ConnectionTracker
    {
        // Словарь: IP-адрес -> Домен
        private readonly ConcurrentDictionary<string, string> _ipToDomainCache = new();

        // Словарь: PID -> Имя процесса (чтобы не искать в БД каждый раз)
        private readonly ConcurrentDictionary<uint, string> _pidToProcessCache = new();

        public void AddDomain(string ip, string domain)
        {
            _ipToDomainCache[ip] = domain;
        }

        public void AddProcess(uint pid, string processName)
        {
            _pidToProcessCache[pid] = processName;
        }

        public string GetDomain(string ip)
        {
            return _ipToDomainCache.TryGetValue(ip, out var domain) ? domain : "Неизвестно";
        }

        public string GetProcessName(uint pid)
        {
            if (_pidToProcessCache.TryGetValue(pid, out var name))
                return name;

            // Если в кэше нет, спрашиваем у Windows
            try
            {
                var process = Process.GetProcessById((int)pid);
                string procName = process.ProcessName;

                // Сохраняем в кэш, чтобы в следующий раз не тормозить
                _pidToProcessCache[pid] = procName;
                return procName;
            }
            catch
            {
                return $"PID_{pid}"; // Если процесс уже умер
            }
        }
    }
}