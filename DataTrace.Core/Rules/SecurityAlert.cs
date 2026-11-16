namespace DataTrace.Core.Rules
{
    public class SecurityAlert
    {
        public string RuleId { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
        public string Description { get; set; } = string.Empty;
        public uint Pid { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string RemoteDomain { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty; // Фрагмент данных, вызвавших подозрение
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}