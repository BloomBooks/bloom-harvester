using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.Logger
{
    // A common logging interface, but we can swap out which logger we actually use.
    // Mostly based on the Azure ApplicationInsights API, but in theory you could swap out for a different implementation and easily translate the Azure terminology into somebody else's
    public interface IMonitorLogger
    {
        void Dispose();

        void LogCritical(string messageFormat, params object[] args);
        void LogError(string messageFormat, params object[] args);
        void LogWarn(string messageFormat, params object[] args);
        void LogInfo(string messageFormat, params object[] args);
        void LogVerbose(string messageFormat, params object[] args);

        void TrackEvent(string eventName);
    }
}
