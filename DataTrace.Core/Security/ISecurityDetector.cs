using DataTrace.Core.Rules;
using DataTrace.Shared;

namespace DataTrace.Core.Security
{
    public interface ISecurityDetector
    {
        // Возвращает список алертов, если событие оказалось опасным
        IEnumerable<SecurityAlert> Analyze(EventWrapper wrapper);
    }
}