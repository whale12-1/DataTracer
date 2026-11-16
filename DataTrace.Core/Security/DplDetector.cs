using DataTrace.Shared;
using DataTrace.Core.Rules;
using System.Text.RegularExpressions;

namespace DataTrace.Core.Security
{
    public class DlpDetector : ISecurityDetector
    {
        private static readonly Regex AwsKeyRegex = new(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled);

        public IEnumerable<SecurityAlert> Analyze(EventWrapper wrapper)
        {
            if (wrapper.PayloadCase != EventWrapper.PayloadOneofCase.ClipboardEvent)
                yield break;

            var text = wrapper.ClipboardEvent.Text;
            if (AwsKeyRegex.IsMatch(text))
            {
                yield return new SecurityAlert
                {
                    RuleId = "DLP-001",
                    Severity = "Critical",
                    Description = "Обнаружен AWS Key в буфере",
                    ProcessName = "Clipboard",
                    Evidence = "AKIA...",
                    Timestamp = DateTime.UtcNow
                };
            }
            // Сюда же можно добавить проверку энтропии, как было раньше
        }
    }
}