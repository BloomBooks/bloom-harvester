using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.LogEntries
{
	class MissingBaseUrlWarning : BaseLogEntry
	{
		private const string kMessageText = "Missing baseUrl";

		public MissingBaseUrlWarning()
		{
			LogLevel = LogLevel.Warn;
			Message = kMessageText;
		}

		public override bool TryParse(string logMessage, out BaseLogEntry value)
		{
			bool isParsedSuccessfully = false;
			value = null;

			if (logMessage.StartsWith(kMessageText))
			{
				isParsedSuccessfully = true;
				value = this;
			}

			return isParsedSuccessfully;
		}
	}
}
