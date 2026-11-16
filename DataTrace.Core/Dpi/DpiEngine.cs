using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DataTrace.Core.Dpi
{
    public class DpiEngine
    {
        private ILiveDevice _device;
        private CancellationTokenSource _cts;

        // Событие: (Тип, IP-адрес назначения, Домен)
        public event Action<string, string, string> OnDomainDetected;

        public void Start(string interfaceName)
        {
            var devices = CaptureDeviceList.Instance;
            Console.WriteLine("[DPI] Доступные интерфейсы:");
            foreach (var dev in devices)
            {
                Console.WriteLine($"  - Name: {dev.Name}, Description: {dev.Description}");
            }

            _device = devices.FirstOrDefault(d => d.Name.Contains(interfaceName))
                      ?? devices.FirstOrDefault();

            if (_device == null)
            {
                Console.WriteLine("[DPI] Сетевой интерфейс не найден!");
                return;
            }

            // Отключаем Promiscuous для Wi-Fi, чтобы не было проблем с драйвером
            _device.Open(DeviceModes.None, 1000);

            // Ловим только DNS (53) и TLS (443)
            _device.Filter = "udp port 53 or tcp port 443";

            _device.OnPacketArrival += PacketArrivalHandler;
            _device.StartCapture();

            _cts = new CancellationTokenSource();
            Console.WriteLine($"[DPI] Захват трафика запущен на {_device.Name}");
        }

        private void PacketArrivalHandler(object sender, PacketCapture e)
        {
            try
            {
                var rawPacket = e.GetPacket();
                var packet = Packet.ParsePacket(rawPacket.LinkLayerType, rawPacket.Data);

                var ipPacket = packet.Extract<IPPacket>();
                if (ipPacket == null) return;

                // Получаем IP-адрес, КУДА идет трафик (например, IP сервера Google)
                string destIp = ipPacket.DestinationAddress.ToString();

                // Игнорируем локальные адреса (роутеры, DNS-серверы провайдера)
                if (destIp.StartsWith("192.168.") || destIp.StartsWith("10.") || destIp.StartsWith("fe80:"))
                {
                    return; // Нам не нужны DNS-запросы к роутеру, SNI даст больше инфы
                }

                // Обработка DNS
                var udpPacket = packet.Extract<UdpPacket>();
                if (udpPacket != null && udpPacket.DestinationPort == 53)
                {
                    ExtractDnsQuery(udpPacket, destIp);
                }

                // Обработка TLS SNI
                var tcpPacket = packet.Extract<TcpPacket>();
                if (tcpPacket != null && tcpPacket.DestinationPort == 443)
                {
                    ExtractTlsSni(tcpPacket, destIp);
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки парсинга
            }
        }

        private void ExtractDnsQuery(UdpPacket udpPacket, string destIp)
        {
            var payload = udpPacket.PayloadData;
            if (payload == null || payload.Length < 12) return;

            try
            {
                var domain = ParseDomainName(payload, 12);
                if (!string.IsNullOrEmpty(domain))
                {
                    // Передаем IP и домен
                    OnDomainDetected?.Invoke("DNS", destIp, domain);
                }
            }
            catch { /* ignore */ }
        }

        private void ExtractTlsSni(TcpPacket tcpPacket, string destIp)
        {
            var payload = tcpPacket.PayloadData;
            if (payload == null || payload.Length < 5) return;

            if (payload[0] != 0x16 || payload[5] != 0x01) return;

            int sniExtIndex = FindSniExtension(payload);
            if (sniExtIndex == -1) return;

            int nameLengthOffset = sniExtIndex + 7;
            if (payload.Length <= nameLengthOffset + 2) return;

            int nameLength = (payload[nameLengthOffset] << 8) | payload[nameLengthOffset + 1];
            int nameStart = nameLengthOffset + 2;

            if (payload.Length >= nameStart + nameLength)
            {
                var sni = Encoding.ASCII.GetString(payload, nameStart, nameLength);

                // ДОБАВЛЯЕМ ПРОВЕРКУ: является ли строка валидным доменом?
                if (!string.IsNullOrEmpty(sni) && Regex.IsMatch(sni, @"^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                {
                    OnDomainDetected?.Invoke("SNI", destIp, sni);
                }
            }
        }

        private int FindSniExtension(byte[] payload)
        {
            for (int i = 40; i < payload.Length - 4; i++)
            {
                if (payload[i] == 0x00 && payload[i + 1] == 0x00)
                {
                    int extLength = (payload[i + 2] << 8) | payload[i + 3];
                    if (extLength > 0 && i + 4 + extLength <= payload.Length)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private string ParseDomainName(byte[] payload, int offset)
        {
            var sb = new StringBuilder();
            int pos = offset;
            while (pos < payload.Length && payload[pos] != 0)
            {
                int len = payload[pos];
                if (len == 0 || pos + 1 + len > payload.Length) break;
                if (sb.Length > 0) sb.Append('.');
                sb.Append(Encoding.ASCII.GetString(payload, pos + 1, len));
                pos += len + 1;
            }
            return sb.ToString();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _device?.StopCapture();
            _device?.Close();
            Console.WriteLine("[DPI] Захват трафика остановлен.");
        }
    }
}