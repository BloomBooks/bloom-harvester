using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Logger
{
	/// <summary>
	/// A class that does nothing with the logs requested
	/// </summary>
	class ConsoleLogger : IMonitorLogger
	{
		public void Dispose()
		{
		}

		public void LogCritical(string messageFormat, params object[] args)
		{
			Console.Error.WriteLine("Log Critical: " + messageFormat, args);
		}

		public void LogError(string messageFormat, params object[] args)
		{
			Console.Error.WriteLine("Log Error: " + messageFormat, args);
		}

		public void LogInfo(string messageFormat, params object[] args)
		{
			Console.Error.WriteLine("Log Info: " + messageFormat, args);
		}

		public void LogVerbose(string messageFormat, params object[] args)
		{
			Console.Error.WriteLine("Log Verbose: " + messageFormat, args);
		}

		public void LogWarn(string messageFormat, params object[] args)
		{
			Console.Error.WriteLine("Log Warn: " + messageFormat, args);
		}

		public void TrackEvent(string eventName)
		{
			Console.Error.WriteLine($"Event: {eventName}");
		}
	}
}
