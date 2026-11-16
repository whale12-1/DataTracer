using DataTrace.Shared;
using DataTrace.Core.Rules;

namespace DataTrace.Core.Security
{
    public class MicrophoneDetector : ISecurityDetector
    {
        private static readonly HashSet<string> AllowedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Teams.exe", "Zoom.exe", "Discord.exe", "chrome.exe", "firefox.exe"
        };

        public IEnumerable<SecurityAlert> Analyze(EventWrapper wrapper)
        {
            if (wrapper.PayloadCase != EventWrapper.PayloadOneofCase.AudioEvent)
                yield break;

            var evt = wrapper.AudioEvent;
            string processName = $"PID_{evt.Pid}";

            if (!AllowedProcesses.Contains(processName))
            {
                yield return new SecurityAlert
                {
                    RuleId = "MIC-001",
                    Severity = "High",
                    Description = "Неавторизованный доступ к микрофону",
                    ProcessName = processName,
                    Evidence = $"Microphone access detected",
                    Timestamp = DateTime.UtcNow
                };
            }
        }
    }
}