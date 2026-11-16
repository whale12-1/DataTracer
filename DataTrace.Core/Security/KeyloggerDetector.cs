using DataTrace.Shared;
using DataTrace.Core.Rules;

namespace DataTrace.Core.Security
{
    public class KeyloggerDetector : ISecurityDetector
    {
        public IEnumerable<SecurityAlert> Analyze(EventWrapper wrapper)
        {
            if (wrapper.PayloadCase != EventWrapper.PayloadOneofCase.Win32KEvent)
                yield break;

            var evt = wrapper.Win32KEvent;

            // Правило 1: Установка низкоуровневого хука клавиатуры
            if (evt.EventId == 1002 && evt.Details.Contains("WH_KEYBOARD_LL"))
            {
                yield return new SecurityAlert
                {
                    RuleId = "KEY-001",
                    Severity = "High",
                    Description = "Обнаружен низкоуровневый хук клавиатуры",
                    ProcessName = $"PID_{evt.Pid}",
                    Evidence = "SetWindowsHookEx",
                    Timestamp = DateTime.UtcNow
                };
            }
            // Правило 2: Слишком частый опрос состояния клавиш (Polling)
            else if (evt.EventId == 1003 && evt.Details.Contains("CallCount"))
            {
                // Извлекаем число из строки "CallCount: 500"
                var parts = evt.Details.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int count))
                {
                    if (count > 400) // Порог, взятый из практик Elastic EDR
                    {
                        yield return new SecurityAlert
                        {
                            RuleId = "KEY-002",
                            Severity = "Critical",
                            Description = "Аномально частый опрос клавиатуры (Keylogger)",
                            ProcessName = $"PID_{evt.Pid}",
                            Evidence = $"CallCount: {count}",
                            Timestamp = DateTime.UtcNow
                        };
                    }
                }
            }
        }
    }
}