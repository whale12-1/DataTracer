using DataTrace.Shared;
using DataTrace.Core.Rules;

namespace DataTrace.Core.Security
{
    public class CameraDetector : ISecurityDetector
    {
        // Белый список процессов, которым разрешено использовать камеру
        private static readonly HashSet<string> AllowedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Teams.exe", "Zoom.exe", "chrome.exe", "msedge.exe", "firefox.exe"
        };

        public IEnumerable<SecurityAlert> Analyze(EventWrapper wrapper)
        {
            if (wrapper.PayloadCase != EventWrapper.PayloadOneofCase.CameraEvent)
                yield break;

            var evt = wrapper.CameraEvent;
            string processName = $"PID_{evt.Pid}"; // В реальности имя процесса можно получить из трекера

            // Если процесс не в белом списке — это подозрительно
            if (!AllowedProcesses.Contains(processName))
            {
                yield return new SecurityAlert
                {
                    RuleId = "CAM-001",
                    Severity = "High",
                    Description = "Неавторизованный доступ к камере",
                    ProcessName = processName,
                    Evidence = $"Camera access detected",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
    }
}