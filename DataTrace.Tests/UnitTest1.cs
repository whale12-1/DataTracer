using Xunit;
using DataTrace.Core.Rules;

namespace DataTrace.Tests
{
    public class RuleEngineTests
    {
        [Fact]
        public void Should_Detect_Aws_Key_In_Payload()
        {
            // Arrange (Подготовка)
            var engine = new RuleEngine();
            string payload = "Here is my config: { aws_access_key_id = AKIAIOSFODNN7EXAMPLE }";
            string processName = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            string domain = "unknown-site.com";
            uint pid = 1234;

            // Act (Действие)
            var alerts = engine.AnalyzePayload(payload, processName, domain, pid);

            // Assert (Проверка)
            Assert.Single(alerts);
            Assert.Equal("DLP-001", alerts[0].RuleId);
            Assert.Equal("Critical", alerts[0].Severity);
            Assert.Equal("chrome.exe", alerts[0].ProcessName); // Проверяем, что путь обрезался до имени файла
        }

        [Fact]
        public void Should_Not_Alert_On_Normal_Text()
        {
            // Arrange
            var engine = new RuleEngine();
            string payload = "Hello, how are you doing today? I am just writing a normal test message.";
            string processName = "telegram.exe";
            string domain = "api.telegram.org";
            uint pid = 5678;

            // Act
            var alerts = engine.AnalyzePayload(payload, processName, domain, pid);

            // Assert
            Assert.Empty(alerts); // Никаких алертов быть не должно
        }

        [Fact]
        public void Should_Detect_High_Entropy_String()
        {
            // Arrange
            var engine = new RuleEngine();
            // Случайная строка (похожа на сгенерированный пароль/токен длиной > 20 символов)
            string payload = "Data: a8Fj3Ksl9Dm2Nq7Pz1Xc4Vb6Rt0Wy5U";
            string processName = "cmd.exe";
            string domain = "api.pastebin.com";
            uint pid = 9012;

            // Act
            var alerts = engine.AnalyzePayload(payload, processName, domain, pid);

            // Assert
            Assert.Contains(alerts, a => a.RuleId == "DLP-004");
        }

        [Fact]
        public void Should_Detect_Notepad_Sending_To_Unknown_Domain()
        {
            // Arrange
            var engine = new RuleEngine();
            // Даже если payload безобидный, сработать должно поведенческое правило
            string payload = "Just some harmless text.";
            string processName = @"C:\Windows\System32\notepad.exe";
            string domain = "evil-hacker.ru";
            uint pid = 4444;

            // Act
            var alerts = engine.AnalyzePayload(payload, processName, domain, pid);

            // Assert
            Assert.Contains(alerts, a => a.RuleId == "BEH-001");
        }

        [Fact]
        public void Should_Allow_Notepad_Sending_To_Microsoft()
        {
            // Arrange
            var engine = new RuleEngine();
            string payload = "Harmless text.";
            string processName = "notepad.exe";
            string domain = "support.microsoft.com";
            uint pid = 5555;

            // Act
            var alerts = engine.AnalyzePayload(payload, processName, domain, pid);

            // Assert
            // Блокнот стучится в Microsoft — это разрешено, алерта BEH-001 быть не должно
            Assert.DoesNotContain(alerts, a => a.RuleId == "BEH-001");
        }
    }
}