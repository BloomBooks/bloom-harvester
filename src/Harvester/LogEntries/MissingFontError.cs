using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.LogEntries
{
	class MissingFontError : BaseLogEntry
	{
		private const string kMissingFont = "Missing font ";

		public string FontName { get; set; }

		public MissingFontError(string fontName)
			: base(LogLevel.Error)
		{
			this.FontName = fontName;
		}

		public override string Message
		{
			get
			{
				return $"{kMissingFont}{this.FontName}";
			}
		}

		public override bool TryParse(string message, out BaseLogEntry value)
		{
			bool isParsedSuccessfully = false;
			value = null;
			if (message != null && message.StartsWith(kMissingFont))
			{
				string fontName = message.Substring(kMissingFont.Length);
				value = new MissingFontError(fontName);
				isParsedSuccessfully = true;
			}

			return isParsedSuccessfully;
		}
	}
}
