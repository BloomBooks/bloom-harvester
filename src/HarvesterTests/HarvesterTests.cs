using System;
using System.Collections.Generic;
using System.Linq;
using BloomHarvester;
using BloomHarvester.LogEntries;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using BloomHarvester.WebLibraryIntegration;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;

namespace BloomHarvesterTests
{
	[TestFixture]
	class HarvesterTests
	{
		private const bool PROCESS = true;
		private const bool SKIP = false;

		private IMonitorLogger _logger = new NullLogger();

		// Helper methods
		private static bool RunShouldProcessBook(BookModel book, string currentVersionStr, HarvestMode mode = HarvestMode.Default)
		{
			return Harvester.ShouldProcessBook(book, mode, new Version(currentVersionStr), out _);
		}

		private BookModel SetupDefaultBook(HarvestState bookState, string previousVersionStr)
		{
			Version previousVersion = new Version(previousVersionStr);

			// Create a default book if the caller didn't have a specific book it wanted to test.
			BookModel book = new BookModel()
			{
				HarvestState = bookState.ToString(),
				HarvesterMajorVersion = previousVersion.Major,
				HarvesterMinorVersion = previousVersion.Minor,
				HarvestLogEntries = new List<string>()
				{
					new BloomHarvester.LogEntries.LogEntry(LogLevel.Warn, LogType.MissingBaseUrl, "").ToString()
				}
			};

			return book;
		}

		#region ShouldProcessBook() tests
		// Process cases
		[TestCase("1.1", "0.9", PROCESS)]   // current is newer by a major version
		// Skip cases
		[TestCase("1.1", "1.0", SKIP)]    // current is newer by a minor version
		[TestCase("1.1", "1.1", SKIP)]    // same versions
		[TestCase("1.1", "1.2", SKIP)]    // current is older by a minor version
		[TestCase("1.1", "2.0", SKIP)]  // current is older by a major version
		public void ShouldProcessBook_DefaultMode_DoneState_SkipsUnlessNewerMajorVersion(string currentVersionStr, string previousVersionStr, bool expectedResult)
		{
			BookModel book = SetupDefaultBook(HarvestState.Done, previousVersionStr);
			bool result = RunShouldProcessBook(book, currentVersionStr);
			Assert.AreEqual(expectedResult, result);
		}

		// Process cases
		[TestCase("1.1", "0.9", PROCESS)]    // current is newer by a major version
		[TestCase("1.1", "1.0", PROCESS)]    // current is newer by a minor version
		// Skip cases
		[TestCase("1.1", "1.1", SKIP)]    // same versions
		[TestCase("1.1", "1.2", SKIP)]    // current is older by a minor version
		[TestCase("1.1", "2.0", SKIP)]    // current is older by a major version		
		public void ShouldProcessBook_DefaultMode_FailedState_SkipsUnlessNewerMajorOrMinorVersion(string currentVersionStr, string previousVersionStr, bool expectedResult)
		{
			BookModel book = SetupDefaultBook(HarvestState.Failed, previousVersionStr);
			bool result = RunShouldProcessBook(book, currentVersionStr);
			Assert.AreEqual(expectedResult, result);
		}

		#region HarvestState=InProgress
		[TestCase("1.1", "0.9")]    // In particular, even when the current version is newer than previousVersion, we should still skip it if it's recently marked InProgress
		[TestCase("1.1", "1.0")]	// In particular, even when the current version is newer than previousVersion, we should still skip it if it's recently marked InProgress
		[TestCase("1.1", "1.1")]
		[TestCase("1.1", "1.2")]
		[TestCase("1.1", "2.0")]
		public void ShouldProcessBook_DefaultMode_InProgressStateRecent_AlwaysSkips(string currentVersionStr, string previousVersionStr)
		{
			DateTime oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
			Version previousVersion = new Version(previousVersionStr);

			var book = new BookModel()
			{
				HarvestState = HarvestState.InProgress.ToString(),
				HarvestStartedAt = new BloomHarvester.Parse.Model.ParseDate(oneMinuteAgo),
				HarvesterMajorVersion = previousVersion.Major,
				HarvesterMinorVersion = previousVersion.Minor,
				HarvestLogEntries = new List<string>()
				{
					new BloomHarvester.LogEntries.LogEntry(LogLevel.Warn, LogType.MissingBaseUrl, "").ToString()
				}
			};

			bool result = RunShouldProcessBook(book, currentVersionStr);
			Assert.AreEqual(SKIP, result);
		}

		[TestCase("1.1", "0.9", PROCESS)]
		[TestCase("1.1", "1.0", PROCESS)]
		[TestCase("1.1", "1.1", PROCESS)]	// Very debatable what this should be
		// These cases can debatably be either Process or Skip.
		// But the current thinking is that if it's marked as InProgress for excessively long, we can basically consider it to have failed.
		// And if it were marked as in Failed state by a newer version, we would skip it
		[TestCase("1.1", "1.2", SKIP)]
		[TestCase("1.1", "2.0", SKIP)]
		public void ShouldProcessBook_DefaultMode_InProgressStateStale(string currentVersionStr, string previousVersionStr, bool expectedResult)
		{
			DateTime threeDaysAgo = DateTime.UtcNow.AddDays(-3);
			Version previousVersion = new Version(previousVersionStr);

			var book = new BookModel()
			{
				HarvestState = HarvestState.InProgress.ToString(),
				HarvestStartedAt = new BloomHarvester.Parse.Model.ParseDate(threeDaysAgo),
				HarvesterMajorVersion = previousVersion.Major,
				HarvesterMinorVersion = previousVersion.Minor,
				HarvestLogEntries = new List<string>()
				{
					new BloomHarvester.LogEntries.LogEntry(LogLevel.Warn, LogType.MissingBaseUrl, "").ToString()
				}
			};

			bool result = RunShouldProcessBook(book, currentVersionStr);
			Assert.AreEqual(expectedResult, result);
		}
		#endregion

		#region RetryFailuresOnly
		[TestCase("1.1", "0.9", PROCESS)]
		[TestCase("1.1", "1.0", PROCESS)]
		[TestCase("1.1", "1.1", PROCESS)]
		// Don't process failures of newer versions.
		[TestCase("1.1", "1.2", SKIP)]
		[TestCase("1.1", "2.0", SKIP)]
		public void ShouldProcessBook_RetryFailuresOnlyMode_FailuresOfNewerVersionsIgnored(string currentVersionStr, string previousVersionStr, bool expectedResult)
		{
			// Setup
			var book = SetupDefaultBook(HarvestState.Failed, previousVersionStr);

			// System under test
			bool result = RunShouldProcessBook(book, currentVersionStr, HarvestMode.RetryFailuresOnly);

			// Verification
			Assert.AreEqual(expectedResult, result);
		}

		#endregion

		[TestCase("Updated")]
		[TestCase("New")]
		public void ShouldProcessBook_StatesWhichAreAlwaysProcessed_ActuallyProcessed(string state)
		{
			var majorVersionNums = new int[] { 1, 2, 3 };

			// Even though it might look like we should skip processing because we still don't have this non-existent font,
			// this test covers the case where the new/updated state of the book causes us to want to reprocess it anyway.
			BookModel book = new BookModel()
			{
				HarvestState = state,
				HarvesterMajorVersion = 2,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
				{
					CreateMissingFontLogEntry("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			foreach (int majorVersion in majorVersionNums)
			{
				var currentVersion = new Version(majorVersion, 0);

				bool result = Harvester.ShouldProcessBook(book, HarvestMode.Default, currentVersion, out _);

				Assert.AreEqual(PROCESS, result, $"Failed for currentVersion={currentVersion}, previousVersion={book.HarvesterMajorVersion}.{book.HarvesterMinorVersion}");
			}
		}
		
		[Test]
		public void ShouldProcessBook_MissingFont_Skipped()
		{
			BookModel book = new BookModel()
			{
				HarvestState = "Failed",
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
				{
					CreateMissingFontLogEntry("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			bool result = RunShouldProcessBook(book, "1.1");

			Assert.AreEqual(SKIP, result);
		}

		[Test]
		public void ShouldProcessBook_AllMissingFontsNowFound_Processed()
		{
			BookModel book = new BookModel()
			{
				HarvestState = "Failed",
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
				{
					CreateMissingFontLogEntry("Arial").ToString()    // Hopefully on the test machine...
				}
			};

			bool result = RunShouldProcessBook(book, "1.1");

			Assert.AreEqual(PROCESS, result);
		}

		[Test]
		public void ShouldProcessBook_OnlySomeMissingFontsNowFound_Skipped()
		{
			BookModel book = new BookModel()
			{
				HarvestState = "Failed",
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
				{
					CreateMissingFontLogEntry("Arial").ToString(),	// Hopefully on the test machine...
					CreateMissingFontLogEntry("SomeCompletelyMadeUpNonExistentFont").ToString()
				}
			};

			bool result = RunShouldProcessBook(book, "1.1");

			Assert.AreEqual(SKIP, result);
		}
		#endregion

		#region MissingFonts tests
		[Test]
		public void GetMissingFonts_BookContainsEmptyStringFont_NotMarkedAsMissing()
		{
			IEnumerable<string> fontNamesUsedInBook = new string[] { "Arial", "" }; // Assumes that Arial is on the machine running the test
			var invoker = new VSUnitTesting.PrivateType(typeof(Harvester));

			List<string> missingFontsResult = (List<string>)(invoker.InvokeStatic("GetMissingFonts", fontNamesUsedInBook));

			Assert.AreEqual(0, missingFontsResult.Count, "No missing fonts were expected.");
		}

		private static BloomHarvester.LogEntries.LogEntry CreateMissingFontLogEntry(string fontName)
		{
			return new BloomHarvester.LogEntries.LogEntry(LogLevel.Error, LogType.MissingFont, fontName);
		}
		#endregion

		#region QueryWhereOptimization tests
		[Test]
		public void GetQueryWhereOptimizations_NewOrUpdatedOnlyMode()
		{
			using (Harvester harvester = new Harvester(new HarvesterOptions() { Mode = HarvestMode.NewOrUpdatedOnly, SuppressLogs = true, Environment = EnvironmentSetting.Local, ParseDBEnvironment = EnvironmentSetting.Local }))
			{
				string result = harvester.GetQueryWhereOptimizations();
				Assert.AreEqual("\"harvestState\" : { \"$in\": [\"New\", \"Updated\", \"Unknown\"]}", result);
			}
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("{}")]
		public void InsertQueryWhereOptimizations_DefaultMode_EmptyUserInput_InsertsJustOptimizations(string userInputWhere)
		{
			string combined = Harvester.InsertQueryWhereOptimizations(userInputWhere, "\"harvesterMajorVersion\":{\"$lt\":2}");
			Assert.AreEqual("{\"harvesterMajorVersion\":{\"$lt\":2}}", combined);
		}

		[Test]
		public void InsertQueryWhereOptimizations_DefaultMode_UserInputWhere_CombinesBoth()
		{
			string userInputWhere = "{ \"title\":{\"$regex\":\"^^A\"},\"tags\":\"bookshelf:Ministerio de Educación de Guatemala\" }";
			string combined = Harvester.InsertQueryWhereOptimizations(userInputWhere, "\"harvesterMajorVersion\":{\"$lt\":2}");
			Assert.AreEqual("{ \"title\": { \"$regex\": \"^^A\" }, \"tags\": \"bookshelf:Ministerio de Educación de Guatemala\", \"harvesterMajorVersion\": { \"$lt\": 2 }}", combined);
		}
		#endregion

		#region ProcessOneBook() tests
		private HarvesterOptions GetHarvesterOptionsForProcessOneBookTests() => new HarvesterOptions() { Mode = HarvestMode.All, SuppressLogs = true, Environment = EnvironmentSetting.Local, ParseDBEnvironment = EnvironmentSetting.Local };

		private Harvester GetSubstituteHarvester(IBloomCliInvoker bloomCli = null, IParseClient parseClient = null, IBookTransfer transferClient = null, IBookAnalyzer bookAnalyzer = null)
		{
			var options = GetHarvesterOptionsForProcessOneBookTests();
			var harvester = Substitute.ForPartsOf<Harvester>(options);

			harvester.Configure().CheckForMissingFontErrors(default, default).ReturnsForAnyArgs(new List<LogEntry>());

			harvester.BloomCli = bloomCli ?? Substitute.For<IBloomCliInvoker>();

			harvester.ParseClient = parseClient ?? Substitute.For<IParseClient>();

			harvester.Transfer = transferClient ?? Substitute.For<IBookTransfer>();

			harvester.Configure().GetAnalyzer(default).ReturnsForAnyArgs(bookAnalyzer ?? Substitute.For<IBookAnalyzer>());

			return harvester;
		}

		[Test]
		public void ProcessOneBook_BloomCliReturnsNonZeroExitCode_BloomCliErrorRecorded()
		{
			// Stub setup
			var bloomStub = Substitute.For<IBloomCliInvoker>();
			bloomStub.StartAndWaitForBloomCli(default, default, out int exitCode, out string stdOut, out string stdErr)
				.ReturnsForAnyArgs(x =>
				{
					x[2] = 2;	// exit code
					return true;
				});

			using (var harvester = GetSubstituteHarvester(bloomCli: bloomStub))
			{
				// Test Setup
				var book = new Book(new BookModel(), _logger);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyBloomCliErrors = logEntries.Any(x => x.Type == LogType.BloomCLIError);
				Assert.That(anyBloomCliErrors, Is.True);

				bloomStub.ReceivedWithAnyArgs(1).StartAndWaitForBloomCli(default, default, out exitCode, out stdOut, out stdErr);
			}
		}

		[Test]
		public void ProcessOneBook_BloomCliTimesOut_TimeoutErrorRecorded()
		{
			// Stub setup
			var bloomStub = Substitute.For<IBloomCliInvoker>();
			bloomStub.StartAndWaitForBloomCli(default, default, out int exitCode, out string stdOut, out string stdErr).ReturnsForAnyArgs(false);
			using (var harvester = GetSubstituteHarvester(bloomCli: bloomStub))
			{
				// Test Setup
				var book = new Book(new BookModel(), _logger);				

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyTimeoutErrors = logEntries.Any(x => x.Type == LogType.TimeoutError);
				Assert.That(anyTimeoutErrors, Is.True);
			}
		}


		[Test]
		public void ProcessOneBook_HarvesterException_ProcessBookErrorRecorded()
		{
			using (var harvester = GetSubstituteHarvester())
			{
				// Test Setup
				var book = new Book(new BookModel() { ObjectId = "123" }, _logger);
				harvester.ParseClient = null;	// Generate a null reference exception

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyRelevantErrors = logEntries.Any(x => x.Type == LogType.ProcessBookError);
				Assert.That(anyRelevantErrors, Is.True);
			}
		}
		#endregion

		[Test]
		public void RemoveBookTitleFromBaseUrl_DecodedWithTitle_DecodedWithoutTitle()
		{
			string input = "https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists@gmail.com/8cba3b47-2ceb-47fd-9ac7-3172824849e4/How+Snakes+Came+to+Be/";
			string output = Harvester.RemoveBookTitleFromBaseUrl(input);
			Assert.AreEqual("https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists@gmail.com/8cba3b47-2ceb-47fd-9ac7-3172824849e4", output);
		}		
	}
}
