using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.LogEntries
{
	class GetFontsError : BaseLogEntry
	{
		private const string kMessageText = "Error calling getFonts";

		public GetFontsError()
		{
			LogLevel = LogLevel.Error;
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
