using System.Management; // Потребуется NuGet пакет System.Management

namespace DataTrace.Core.Security
{
    public class DeviceNameResolver
    {
        public string GetFriendlyName(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId) || !deviceId.Contains("\\")) return deviceId;

            try
            {
                // Ищем устройство в системе по его DeviceID (через WMI)
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT Name FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'");

                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString() ?? deviceId;
                }
            }
            catch (Exception ex)
            {
                // Если не нашли (или нет прав), возвращаем сырой ID
            }
            return deviceId;
        }
    }
}