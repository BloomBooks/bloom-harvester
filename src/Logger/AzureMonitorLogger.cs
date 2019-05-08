using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.Logger
{
    /// <summary>
    /// Implementation of a logger which sends data to Azure Monitor / Azure Application Insights.
    /// </summary>
    class AzureMonitorLogger : IMonitorLogger, IDisposable
    {
        private TelemetryClient tc = new TelemetryClient();

        public AzureMonitorLogger(bool isDebug)
        {
            string instrumentationKey = "20f7661f-2c68-4d47-af44-842f27084d2f"; // The key for the Develop instance
            if (!isDebug)
            {
                // Get the Release mode key from an environment variable.
                string keyFromEnvVariable = Environment.GetEnvironmentVariable("BloomHarvesterAzureAppInsightsKey");
                if (!String.IsNullOrWhiteSpace(keyFromEnvVariable))
                {
                    instrumentationKey = keyFromEnvVariable;
                }
            }

            Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;

            tc.Context.User.Id = "BloomHarvester";
            tc.Context.Session.Id = Guid.NewGuid().ToString();
            tc.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
        }

        #region IMonitorLogger
        /// <summary>
        /// Very important that Dispose() should be called before the program concludes, or else events/telemetry may not be persisted to remote storage!
        /// </summary>
        public void Dispose()
        {
            tc.Flush();
            Console.Out.WriteLine("Flushing AzureMonitor");
            System.Threading.Thread.Sleep(5000);    // Allow some time to flush before shutdown. It might not flush right away. (https://docs.microsoft.com/en-us/azure/azure-monitor/app/api-custom-events-metrics#tracktrace)
        }

        // Log a trace with Severity = Critical
        public void LogCritical(string messageFormat, params object[] args)
        {
            this.TrackTrace(SeverityLevel.Critical, messageFormat, args);
        }

        // Log a trace with Severity = Error
        public void LogError(string messageFormat, params object[] args)
        {
            this.TrackTrace(SeverityLevel.Error, messageFormat, args);
        }

        // Log a trace with Severity = Warning
        public void LogWarn(string messageFormat, params object[] args)
        {
            this.TrackTrace(SeverityLevel.Warning, messageFormat, args);
        }

        // Log a trace with Severity = Information
        public void LogInfo(string messageFormat, params object[] args)
        {
            this.TrackTrace(SeverityLevel.Information, messageFormat, args);
        }

        // Log a trace with Severity = Verbose
        public void LogVerbose(string messageFormat, params object[] args)
        {
            this.TrackTrace(SeverityLevel.Verbose, messageFormat, args);
        }

        // Suggest that this be used to log more significant things like actions to the Events table as opposed to Traces which can represent diagnostic/debugging log messages
        public void TrackEvent(string eventName)
        {
            tc.TrackEvent(eventName);
        }
        #endregion


        public void TrackTrace(SeverityLevel severityLevel, string messageFormat, params object[] args)
        {
            tc.TrackTrace(String.Format(messageFormat, args), severityLevel);
        }
    }
}
