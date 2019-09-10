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
		private Harvester _harvester; 

		[SetUp]
		public void TestSetup()
		{
			_harvester = new Harvester(new HarvesterOptions() { Mode = HarvestMode.Default, SuppressLogs = true, ReadOnly = true });
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_DoneState_ThisIsNewer_Redo()
		{
			bool result = RunShouldProcessBookSetup(HarvestState.Done, "1.1", "1.0");
			Assert.AreEqual(true, result);
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_DoneState_ThisIsSame_Skip()
		{
			bool result = RunShouldProcessBookSetup(HarvestState.Done, "1.1", "1.1");
			Assert.AreEqual(false, result);
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_DoneState_ThisIsOlder_Skips()
		{
			bool result = RunShouldProcessBookSetup(HarvestState.Done, "1.1", "2.0");
			Assert.AreEqual(false, result);
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_FailedState_ThisIsNewer_Retry()
		{
			bool result = RunShouldProcessBookSetup(HarvestState.Failed, currentVersion: "1.1", previousVersion: "1.0");
			Assert.AreEqual(true, result);
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_FailedState_ThisIsSame_ConsidersRetry()
		{
			bool result = RunShouldProcessBookSetup(HarvestState.Failed, currentVersion: "1.1", previousVersion: "1.1");
			Assert.AreEqual(true, result);
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_FailedState_ThisIsOlder_Skips()
		{
			bool result = RunShouldProcessBookSetup(HarvestState.Failed, currentVersion: "1.1", previousVersion: "2.0");
			Assert.AreEqual(false, result);
		}

		[TestCase("1.0")]
		[TestCase("1.1")]
		[TestCase("2.0")]
		public void ShouldProcessBook_DefaultMode_InProgressStateRecent_AlwaysSkips(string previousVersion)
		{
			DateTime oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
			var book = new Book()
			{
				HarvestState = HarvestState.InProgress.ToString(),
				HarvestStartedAt = new BloomHarvester.Parse.Model.Date(oneMinuteAgo),
				HarvesterVersion = previousVersion,
				HarvestLogEntries = new List<string>()
					{
						new MissingBaseUrlWarning().ToString()
					}
			};

			bool result = RunShouldProcessBookSetup(HarvestState.InProgress, currentVersion: "1.1", previousVersion: previousVersion, book: book);
			Assert.AreEqual(false, result);
		}

		[TestCase("1.0")]
		[TestCase("1.1")]
		[TestCase("2.0")]
		public void ShouldProcessBook_DefaultMode_InProgressStateStale_AlwaysSkips(string previousVersion)
		{
			DateTime threeDaysAgo = DateTime.UtcNow.AddDays(-3);
			var book = new Book()
			{
				HarvestState = HarvestState.InProgress.ToString(),
				HarvestStartedAt = new BloomHarvester.Parse.Model.Date(threeDaysAgo),
				HarvesterVersion = previousVersion,
				HarvestLogEntries = new List<string>()
					{
						new MissingBaseUrlWarning().ToString()
					}
			};

			bool result = RunShouldProcessBookSetup(HarvestState.InProgress, currentVersion: "1.1", previousVersion: previousVersion, book: book);
			Assert.AreEqual(true, result);
		}

		private bool RunShouldProcessBookSetup(HarvestState bookState, string currentVersion, string previousVersion, Book book = null)
		{
			_harvester = new Harvester(new HarvesterOptions() { Mode = HarvestMode.Default, SuppressLogs = true, ReadOnly = true });
			_harvester.Version = new Version(currentVersion);

			if (book == null)
			{
				// Create a default book if the caller didn't have a specific book it wanted to test.
				book = new Book()
				{
					HarvestState = bookState.ToString(),
					HarvesterVersion = previousVersion,
					HarvestLogEntries = new List<string>()
					{
						new MissingBaseUrlWarning().ToString()
					}
				};
			}

			bool result = _harvester.ShouldProcessBook(book);
			return result;
		}

		[TestCase("Updated")]
		[TestCase("New")]
		public void ShouldProcessBook_StatesWhichAreAlwaysProcessed_ReturnsTrue(string state)
		{
			var majorVersionNums = new int[] { 1, 2, 3 };

			foreach (int majorVersion in majorVersionNums)
			{
				_harvester.Version = new Version(majorVersion, 0);

				// Even though it might look like we should skip processing because we still don't have this non-existent font,
				// this test covers the case where the new/updated state of the book causes us to want to reprocess it anyway.
				Book book = new Book()
				{
					HarvestState = state,
					HarvesterVersion = "2.0",
					HarvestLogEntries = new List<string>()
					{
						new MissingFontError("SomeCompletelyMadeUpNonExistentFont").ToString()
					}
				};

				bool result = _harvester.ShouldProcessBook(book);

				Assert.AreEqual(true, result, $"Failed for this.Version={_harvester.Version}, previousVersion={book.HarvesterVersion}");
			}
		}


		[Test]
		public void ShouldProcessBook_MissingFont_ReturnsFalse()
		{
			Book book = new Book()
			{
				HarvestState = "Failed",
				HarvestLogEntries = new List<string>()
				{
					new MissingFontError("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			bool result = _harvester.ShouldProcessBook(book);

			Assert.AreEqual(false, result);
		}

		[Test]
		public void ShouldProcessBook_AllMissingFontsNowFound_ReturnsTrue()
		{
			Book book = new Book()
			{
				HarvestState = "Failed",
				HarvestLogEntries = new List<string>()
				{
					new MissingFontError("Arial").ToString()    // Hopefully on the test machine...
				}
			};

			bool result = _harvester.ShouldProcessBook(book);

			Assert.AreEqual(true, result);
		}

		[Test]
		public void ShouldProcessBook_OnlySomeMissingFontsNowFound_ReturnsFalse()
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

			bool result = _harvester.ShouldProcessBook(book);

			Assert.AreEqual(false, result);
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
