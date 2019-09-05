using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloomHarvester.LogEntries;
using NUnit.Framework;

namespace BloomHarvesterTests.LogEntry
{
	[TestFixture]
	class BaseLogEntryTests
	{
		[Test]
		public void ParseFromLogEntry_GarbageInput_ReturnsNull()
		{
			string logEntry = "asdfqwertyuiop";
			var parsedObj = BaseLogEntry.ParseFromLogEntry(logEntry);

			Assert.IsNull(parsedObj, "Input was invalid so parsedObj was expected to be null");
		}

		[Test]
		public void ParseFromLogEntry_MissingFontLog_ParsesMissingFontError()
		{
			string logEntry = "Error: Missing font Arial";
			var parsedObj = BaseLogEntry.ParseFromLogEntry(logEntry);

			Assert.IsInstanceOf(typeof(MissingFontError), parsedObj);
		}

		[Test]
		public void ParseFromLogEntry_MissingBaseUrl_ParsesMissingBaseUrlWarning()
		{
			string logEntry = "Warn: Missing baseUrl";
			var parsedObj = BaseLogEntry.ParseFromLogEntry(logEntry);

			Assert.IsInstanceOf(typeof(MissingBaseUrlWarning), parsedObj);
		}

	}
}

