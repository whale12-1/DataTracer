using DataTrace.Shared;
using DataTrace.Core.Rules;
using System.Collections.Generic;
using System.Linq;

namespace DataTrace.Core.Security
{
    public class SecurityManager
    {
        private readonly List<ISecurityDetector> _detectors;

        public SecurityManager()
        {
            // Регистрируем все наши детекторы
            _detectors = new List<ISecurityDetector>
            {
                new DlpDetector(),
                new KeyloggerDetector(),
                new CameraDetector(),      // <--- НОВЫЙ
                new MicrophoneDetector()   // <--- НОВЫЙ
            };
        }

        public IEnumerable<SecurityAlert> ProcessEvent(EventWrapper wrapper)
        {
            var allAlerts = new List<SecurityAlert>();
            foreach (var detector in _detectors)
            {
                allAlerts.AddRange(detector.Analyze(wrapper));
            }
            return allAlerts;
        }
    }
}