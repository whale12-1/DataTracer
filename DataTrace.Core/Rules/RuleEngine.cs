using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace DataTrace.Core.Rules
{
    public class RuleEngine
    {
        private static readonly Regex AwsKeyRegex = new(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled);
        private static readonly Regex GithubTokenRegex = new(@"ghp_[a-zA-Z0-9]{36}", RegexOptions.Compiled);
        private static readonly Regex PrivateKeyRegex = new(@"-----BEGIN (RSA|OPENSSH|EC) PRIVATE KEY-----", RegexOptions.Compiled);

        // ИСПРАВЛЕНО: принимаем строки processName и domain, а также pid
        public List<SecurityAlert> AnalyzePayload(string payload, string processName, string domain, uint pid)
        {
            var alerts = new List<SecurityAlert>();

            if (string.IsNullOrWhiteSpace(payload)) return alerts;

            // Красивое имя процесса (отрезаем длинный путь, если он есть)
            string shortProcessName = processName.Split('\\').Last();

            // 1. Проверка на AWS Keys
            if (AwsKeyRegex.IsMatch(payload))
            {
                alerts.Add(CreateAlert("DLP-001", "Critical", "Обнаружен AWS Access Key", shortProcessName, domain, pid, "AKIA..."));
            }

            // 2. Проверка на GitHub Tokens
            if (GithubTokenRegex.IsMatch(payload))
            {
                alerts.Add(CreateAlert("DLP-002", "Critical", "Обнаружен GitHub Token", shortProcessName, domain, pid, "ghp_..."));
            }

            // 3. Проверка на приватные ключи
            if (PrivateKeyRegex.IsMatch(payload))
            {
                alerts.Add(CreateAlert("DLP-003", "High", "Обнаружен приватный ключ", shortProcessName, domain, pid, "BEGIN PRIVATE KEY"));
            }

            // 4. Энтропийный анализ
            var words = payload.Split(new[] { ' ', '\n', '\r', '\t', '"', '\'', '=', ':' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.Length >= 20 && CalculateShannonEntropy(word) > 4.5)
                {
                    alerts.Add(CreateAlert("DLP-004", "Medium", "Обнаружена строка с высокой энтропией (возможен токен/ключ)", shortProcessName, domain, pid, word.Substring(0, 5) + "..."));
                    break;
                }
            }

            // 5. Поведенческое правило
            if (shortProcessName.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(domain) &&
                !domain.Contains("microsoft.com"))
            {
                alerts.Add(CreateAlert("BEH-001", "High", "Блокнот пытается отправить данные в сеть", shortProcessName, domain, pid, domain));
            }

            return alerts;
        }

        // ИСПРАВЛЕНО: обновлены параметры для создания алерта
        private SecurityAlert CreateAlert(string ruleId, string severity, string desc, string processName, string domain, uint pid, string evidence)
        {
            return new SecurityAlert
            {
                RuleId = ruleId,
                Severity = severity,
                Description = desc,
                Pid = pid,
                ProcessName = processName,
                RemoteDomain = domain,
                Evidence = evidence
            };
        }

        private double CalculateShannonEntropy(string input)
        {
            var frequencies = new Dictionary<char, int>();
            foreach (char c in input)
            {
                if (frequencies.ContainsKey(c)) frequencies[c]++;
                else frequencies[c] = 1;
            }

            double entropy = 0;
            foreach (var freq in frequencies.Values)
            {
                double p = (double)freq / input.Length;
                entropy -= p * Math.Log2(p);
            }
            return entropy;
        }
    }
}