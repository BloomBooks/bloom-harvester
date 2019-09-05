using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.LogEntries
{
	// Base class for log entries populated in the HarvesterLog column
	//
	// Requires subclasses to implement a TryParseLogMessage for them to create an instance of themselves from a log message,
	//    and also add to this class's ParseFromLogEntry() method.
	abstract class BaseLogEntry
	{
		// Constructors
		public BaseLogEntry()
		{
		}

		public BaseLogEntry(LogLevel level)
		{
			this.LogLevel = level;
		}

		///////////////////////////////////////////
		// Assets provided by the abstract class //
		///////////////////////////////////////////
		public LogLevel LogLevel { get; set; }
		public virtual string Message { get; set; }

		public override string ToString()
		{
			return $"{this.LogLevel}: {this.Message}";
		}

		////////////////////////////////////////////
		// Obligations imposed on derived classes //
		////////////////////////////////////////////
		
		// Subclasses should implement this method
		// If the message represents a valid instance of the subclass, the subclass should return true and populate the out parameter with an instance of the subclass accordingly
		// If the message is not valid, the method should return false (and populate out parameter with null)
		public abstract bool TryParse(string logMessage, out BaseLogEntry value);

		// Dummy instances of each of the derived types to call its TryParse method (which is semantically a static abstract, except that's not technically possible)
		private static MissingFontError _missingFontError = new MissingFontError("");
		private static MissingBaseUrlWarning _missingBaseUrlWarning = new MissingBaseUrlWarning();

		/// <summary>
		/// Reads an entry from harvestLog field and attempts to parse it
		/// </summary>
		/// <param name="logEntry">An entry from the harvestLog field in Parse's books class</param>
		/// <returns>An object which is some type of BaseLogEntry if the string could be parsed, or null otherwise</returns>
		public static BaseLogEntry ParseFromLogEntry(string logEntry)
		{
			BaseLogEntry value;

			if (String.IsNullOrEmpty(logEntry))
			{
				return null;
			}

			int endOfLevelIndex = logEntry.IndexOf(":");
			if (endOfLevelIndex < 0)
				return null;

			string message = logEntry.Substring(endOfLevelIndex + 1).TrimStart();
			if (_missingFontError.TryParse(message, out value))
			{
				return value;
			}
			else if (_missingBaseUrlWarning.TryParse(message, out value))
			{
				return value;
			}
			else
			{
				return null;
			}
		}
	}
}
