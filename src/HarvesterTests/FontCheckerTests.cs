using System;
using System.Collections.Generic;
using System.Linq;
using BloomHarvester;
using NUnit.Framework;

namespace BloomHarvesterTests
{
	[TestFixture]
	class FontCheckerTests
	{
		[Test]
		public void GetMissingFonts_BookContainsEmptyStringFont_NotMarkedAsMissing()
		{
			IEnumerable<string> fontNamesUsedInBook = new string[] { "Arial", "" }; // Assumes that Arial is on the machine running the test

			List<string> missingFontsResult = FontChecker.GetMissingFonts(fontNamesUsedInBook);

			Assert.That(missingFontsResult.Count, Is.EqualTo(0), "No missing fonts were expected.");
		}

		[Test]
		public void GetMissingFonts_BookIsMissingFont_MarkedAsMissing()
		{
			string fontName = "qwertuiopasdfghjk";	// a gibberish font name
			IEnumerable<string> fontNamesUsedInBook = new string[] { fontName };

			List<string> missingFontsResult = FontChecker.GetMissingFonts(fontNamesUsedInBook);

			Assert.That(missingFontsResult.Count, Is.EqualTo(1));
			Assert.That(missingFontsResult.First(), Is.EqualTo(fontName));
		}

		[TestCase("serif")]
		[TestCase("sans-serif")]
		[TestCase("monospace")]
		public void GetMissingFonts_BookContainsFontFamily_NotMarkedAsMissing(string fontFamily)
		{
			IEnumerable<string> fontNamesUsedInBook = new string[] { fontFamily }; // Assumes that Arial is on the machine running the test

			List<string> missingFontsResult = FontChecker.GetMissingFonts(fontNamesUsedInBook);

			Assert.That(missingFontsResult.Count, Is.EqualTo(0), "No error should be reported if the only missing font is font family");
		}
	}
}
