using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BloomHarvester;
using BloomHarvester.Parse.Model;
using BloomHarvester.LogEntries;

namespace BloomHarvesterTests
{
	[TestFixture]
	class HarvesterTests
	{
		private Harvester _harvester = new Harvester(new HarvestAllOptions() { SuppressLogs = true, ReadOnly = true });

		[TestCase("Updated")]
		[TestCase("New")]
		[TestCase("Unknown")]
		public void ShouldSkipProcessing_Updated_ReturnsFalse(string state)
		{
			Book book = new Book()
			{
				HarvestState = state,
				HarvestLogEntries = new List<string>()
				{
					new MissingFontError("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			bool result = _harvester.ShouldSkipProcessing(book);

			Assert.AreEqual(false, result);
		}


		[Test]
		public void ShouldSkipProcessing_MissingFont_ReturnsTrue()
		{
			Book book = new Book()
			{
				HarvestState = "Failed",
				HarvestLogEntries = new List<string>()
				{
					new MissingFontError("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			bool result = _harvester.ShouldSkipProcessing(book);

			Assert.AreEqual(true, result);
		}

		[Test]
		public void ShouldSkipProcessing_AllMissingFontsNowFound_ReturnsFalse()
		{
			Book book = new Book()
			{
				HarvestState = "Failed",
				HarvestLogEntries = new List<string>()
				{
					new MissingFontError("Arial").ToString()    // Hopefully on the test machine...
				}
			};

			bool result = _harvester.ShouldSkipProcessing(book);

			Assert.AreEqual(false, result);
		}

		[Test]
		public void ShouldSkipProcessing_OnlySomeMissingFontsNowFound_ReturnsTrue()
		{
			Book book = new Book()
			{
				HarvestState = "Failed",
				HarvestLogEntries = new List<string>()
				{
					new MissingFontError("Arial").ToString(),	// Hopefully on the test machine...
					new MissingFontError("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			bool result = _harvester.ShouldSkipProcessing(book);

			Assert.AreEqual(true, result);
		}

		[Test]
		public void RemoveBookTitleFromBaseUrl_DecodedWithTitle_DecodedWithoutTitle()
		{
			string input = "https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists@gmail.com/8cba3b47-2ceb-47fd-9ac7-3172824849e4/How+Snakes+Came+to+Be/";
			string output = Harvester.RemoveBookTitleFromBaseUrl(input);
			Assert.AreEqual("https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists@gmail.com/8cba3b47-2ceb-47fd-9ac7-3172824849e4", output);
		}
	}
}
