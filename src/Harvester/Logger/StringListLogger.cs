using System;
using System.Collections.Generic;

namespace BloomHarvester.Logger
{
	/// <summary>
	/// A logger that writes the logged information to a list of strings.
	/// This is useful only for tests in all likelihood.
	/// </summary>
	public class StringListLogger : IMonitorLogger
	{
		public readonly List<string> LogList = new List<string>();

		public void Dispose()
		{
		}

		public void LogCritical(string messageFormat, params object[] args)
		{
			LogList.Add(GetTimeInfo() + "Log Critical: " + String.Format(messageFormat, args));
		}

		public void LogError(string messageFormat, params object[] args)
		{
			LogList.Add(GetTimeInfo() + "Log Error: " + String.Format(messageFormat, args));
		}

		public void LogWarn(string messageFormat, params object[] args)
		{
			LogList.Add(GetTimeInfo() + "Log Warn: " + String.Format(messageFormat, args));
		}

		public void LogInfo(string messageFormat, params object[] args)
		{
			LogList.Add(GetTimeInfo() + "Log Info: " + String.Format(messageFormat, args));
		}

		public void LogVerbose(string messageFormat, params object[] args)
		{
			LogList.Add(GetTimeInfo() + "Log Verbose: " + String.Format(messageFormat, args));
		}

		public void TrackEvent(string eventName)
		{
			LogList.Add($"{GetTimeInfo()}Event: {eventName}");
		}

		private string GetTimeInfo()
		{
			return $"[{DateTime.Now.ToString()} (UTC{TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours})] ";
		}
	}
}
