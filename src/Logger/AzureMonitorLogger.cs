using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		private TelemetryClient _telemetry = new TelemetryClient();

		public AzureMonitorLogger(EnvironmentSetting environment)
		{
			// Get the Instrumentation Key for Azure from an environment variable.
			string environmentVarName = "BloomHarvesterAzureAppInsightsKeyDev";
			if (environment == EnvironmentSetting.Test)
			{
				environmentVarName = Environment.GetEnvironmentVariable("BloomHarvesterAzureAppInsightsKeyTest");
			}
			else if (environment == EnvironmentSetting.Prod)
			{
				environmentVarName = Environment.GetEnvironmentVariable("BloomHarvesterAzureAppInsightsKeyProd");
			}
			
			string instrumentationKey = Environment.GetEnvironmentVariable(environmentVarName);
			Debug.Assert(!String.IsNullOrWhiteSpace(instrumentationKey), "Azure Instrumentation Key is invalid. Azure logging probably won't work.");


			Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.Active.InstrumentationKey = instrumentationKey;

			_telemetry.Context.User.Id = "BloomHarvester";
			_telemetry.Context.Session.Id = Guid.NewGuid().ToString();
			_telemetry.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
		}

		#region IMonitorLogger
		/// <summary>
		/// Very important that Dispose() should be called before the program concludes, or else events/telemetry may not be persisted to remote storage!
		/// </summary>
		public void Dispose()
		{
			_telemetry.Flush();
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
			_telemetry.TrackEvent(eventName);
		}
		#endregion


		protected void TrackTrace(SeverityLevel severityLevel, string messageFormat, params object[] args)
		{
			_telemetry.TrackTrace(String.Format(messageFormat, args), severityLevel);
		}
	}
}
