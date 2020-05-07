using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloomHarvester;
using BloomHarvester.IO;
using BloomHarvester.LogEntries;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using BloomHarvester.WebLibraryIntegration;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace BloomHarvesterTests
{
	[TestFixture]
	class HarvesterTests
	{
		private const bool PROCESS = true;
		private const bool SKIP = false;

		private IMonitorLogger _logger = new NullLogger();

		// Caches the fake tests objects passed in to the Harvester constructor
		private IParseClient _fakeParseClient;
		private IS3Client _fakeBloomS3Client;
		private IS3Client _fakeS3UploadClient;
		private IBookTransfer _fakeTransfer;
		private IIssueReporter _fakeIssueReporter;
		private IBloomCliInvoker _fakeBloomCli;
		private IFontChecker _fakeFontChecker;
		private IFileIO _fakeFileIO;

		[SetUp]
		public void ClearCachedValues()
		{
			_fakeParseClient= null;
			_fakeBloomS3Client = null;
			_fakeS3UploadClient = null;
			_fakeTransfer = null;
			_fakeIssueReporter = null;
			_fakeBloomCli = null;
			_fakeFileIO = null;
		}

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

		[TestCase("Dev", "Dev")]
		[TestCase("Dev", "Local")]
		public void HarvesterGetUniqueIdentifier_TwoInstances_ReturnDifferentValues(string env1, string env2)
		{
			using (var harvester1 = new Harvester(new HarvesterOptions() { Environment = (EnvironmentSetting)Enum.Parse(typeof(EnvironmentSetting), env1), SuppressLogs = true}))
			{
				var obj = new VSUnitTesting.PrivateObject(harvester1);
				obj.SetField("_initTime", DateTime.Now.AddSeconds(-1));

				using (var harvester2 = new Harvester(new HarvesterOptions() { Environment = (EnvironmentSetting)Enum.Parse(typeof(EnvironmentSetting), env2), SuppressLogs = true }))
				{
					var id1 = harvester1.GetUniqueIdentifier();
					var id2 = harvester2.GetUniqueIdentifier();

					Assert.That(id1, Is.Not.EqualTo(id2));
				}
			}
		}

		#region ShouldProcessBook() tests
		#region Default mode
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

		[Test]
		public void ShouldProcessBook_DefaultMode_FailedState_Version3Error()
		{
			BookModel book = SetupDefaultBook(HarvestState.Failed, "3.0");
			book.HarvestLogEntries = new List<string>();
			book.HarvestLogEntries.Add("MissingFontError madeUpFontName");	// This won't be parsed as a valid log entry anymore

			bool result = RunShouldProcessBook(book, "4.0");

			Assert.AreEqual(true, result);
		}

		[Test]
		public void ShouldProcessBook_DefaultMode_FailedState_HarvestVersionNull()
		{
			BookModel book = SetupDefaultBook(HarvestState.Failed, "3.0");
			book.HarvestLogEntries = null;

			// Hopefully won't throw a Source must not be null (basically a Null Reference Exception)
			bool result = RunShouldProcessBook(book, "4.0");

			Assert.AreEqual(true, result);
		}
		#endregion

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

		private Harvester GetSubstituteHarvester(HarvesterOptions options, IBloomCliInvoker bloomCli = null, IParseClient parseClient = null, IBookTransfer transferClient = null, IS3Client s3DownloadClient = null, IS3Client s3UploadClient = null, IBookAnalyzer bookAnalyzer = null, IFontChecker fontChecker = null, IFileIO fileIO = null, IMonitorLogger logger = null)
		{
			if (logger == null)
				logger = _logger;
			options.SuppressLogs = true;	// Probably not meaningful for tests

			string identifier = "UnitTestHarvester";

			// We don't want the unit tests to actually create any YouTrack issues
			_fakeIssueReporter = Substitute.For<IIssueReporter>();

			_fakeBloomCli = bloomCli;
			if (_fakeBloomCli == null)
			{
				// Setup a mock which returns the parameters for the normal case
				_fakeBloomCli = Substitute.For<IBloomCliInvoker>();
				_fakeBloomCli.Configure().StartAndWaitForBloomCli(default, default, out int exitCode, out string stdOut, out string stdErr)
					.ReturnsForAnyArgs(args =>
					{
						args[2] = 0; // exit code
						return true;
					});
			}

			_fakeParseClient = parseClient ?? Substitute.For<IParseClient>();
			_fakeBloomS3Client = s3DownloadClient ?? Substitute.For<IS3Client>();
			_fakeS3UploadClient = s3UploadClient ?? Substitute.For<IS3Client>();
			_fakeTransfer = transferClient ?? Substitute.For<IBookTransfer>();

			_fakeFileIO = fileIO ?? Substitute.For<IFileIO>();

			if (fontChecker == null)
			{
				_fakeFontChecker = Substitute.For<IFontChecker>();
				_fakeFontChecker.Configure().GetMissingFonts(default, out _).ReturnsForAnyArgs(args =>
				{
					args[1] = true;	// the success code
					return new List<string>();	// return no missing fonts
				});
			}
			else
				_fakeFontChecker = fontChecker;

			var harvester = Substitute.ForPartsOf<Harvester>(options, EnvironmentSetting.Test, identifier, _fakeParseClient, _fakeBloomS3Client, _fakeS3UploadClient, _fakeTransfer, _fakeIssueReporter, logger, _fakeBloomCli, _fakeFontChecker, _fakeFileIO);

			harvester.Configure().GetAnalyzer(default).ReturnsForAnyArgs(bookAnalyzer ?? Substitute.For<IBookAnalyzer>());

			// Only execute this part if the caller didn't manually specify their own fileIO handler.
			if (fileIO == null)
			{
				// This needs to run after the harvester is initialized (because it calls an instance method),
				// so we need this 2nd phase of initializatino
				//
				// Pretend there's an index.htm file, so that the code which verifies it was created won't fail us
				// It may be pretty commonplace that most unit tests would prefer for this check not to happen, so we'll
				// take care of it here for all unit tests by default
				var indexPath = Path.Combine(Path.GetTempPath(), harvester.GetBloomDigitalArtifactsPath(), "index.htm");
				_fakeFileIO.Configure().Exists(indexPath).Returns(true);
			}

			return harvester;
		}

		[Test]
		public void ProcessOneBook_BloomCliReturnsNonZeroExitCode_BloomCliErrorRecorded()
		{
			// Stub setup
			var bloomStub = Substitute.For<IBloomCliInvoker>();
			bloomStub.StartAndWaitForBloomCli(default, default, out int exitCode, out string stdOut, out string stdErr)
				.ReturnsForAnyArgs(args =>
				{
					args[2] = 2;	// exit code
					return true;
				});

			var options = GetHarvesterOptionsForProcessOneBookTests();
			using (var harvester = GetSubstituteHarvester(options, bloomCli: bloomStub))
			{
				// Test Setup
				var book = new Book(new BookModel(), _logger);
				book.SetHarvesterEvaluation("epub", true);
				book.SetHarvesterEvaluation("bloomReader", true);
				book.SetHarvesterEvaluation("readOnline", true);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyBloomCliErrors = logEntries.Any(x => x.Type == LogType.BloomCLIError);
				Assert.That(anyBloomCliErrors, Is.True, "The relevant error type was not found");
				Assert.That(book.Model.HarvestState, Is.EqualTo("Failed"), "HarvestState should be failed");

				// Validate that the failure set the approvals to false.  (BL-8413)
				Assert.That(book.Model.Show.epub.harvester.Value, Is.False);
				Assert.That(book.Model.Show.bloomReader.harvester.Value, Is.False);
				Assert.That(book.Model.Show.readOnline.harvester.Value, Is.False);

				// Validate that the code did in fact attempt to report an error
				_fakeIssueReporter.ReceivedWithAnyArgs().ReportError(default, default, default, default);
			}
		}

		[Test]
		public void ProcessOneBook_BloomCliTimesOut_TimeoutErrorRecorded()
		{
			// Stub setup
			var bloomStub = Substitute.For<IBloomCliInvoker>();
			bloomStub.StartAndWaitForBloomCli(default, default, out int exitCode, out string stdOut, out string stdErr).ReturnsForAnyArgs(false);

			var options = GetHarvesterOptionsForProcessOneBookTests();
			using (var harvester = GetSubstituteHarvester(options, bloomCli: bloomStub))
			{
				// Test Setup
				var book = new Book(new BookModel(), _logger);				

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyTimeoutErrors = logEntries.Any(x => x.Type == LogType.TimeoutError);
				Assert.That(anyTimeoutErrors, Is.True, "The relevant error type was not found");
				Assert.That(book.Model.HarvestState, Is.EqualTo("Failed"), "HarvestState should be failed");

				// Validate that the code did in fact attempt to report an error
				_fakeIssueReporter.ReceivedWithAnyArgs().ReportError(default, default, default, default);
			}
		}

		[Test]
		public void ProcessOneBook_HarvesterException_ProcessBookErrorRecorded()
		{
			var options = GetHarvesterOptionsForProcessOneBookTests();
			var parseClient = Substitute.For<IParseClient>();
			parseClient.Configure().UpdateObject(default, default, default).ReturnsForAnyArgs(args =>
			{
				throw new ApplicationException("Simulated exception for unit tests");
			});

			using (var harvester = GetSubstituteHarvester(options, parseClient: parseClient))
			{
				// Test Setup
				var book = new Book(new BookModel() { ObjectId = "123" }, _logger);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyRelevantErrors = logEntries.Any(x => x.Type == LogType.ProcessBookError);
				Assert.That(anyRelevantErrors, Is.True, "The relevant error type was not found");
				Assert.That(book.Model.HarvestState, Is.EqualTo("Failed"), "HarvestState should be failed");

				// Validate that the code did in fact attempt to report an error
				_fakeIssueReporter.ReceivedWithAnyArgs().ReportException(default, default, default, default);
			}
		}

		[Test]
		public void ProcessOneBook_GetMissingFontFails_MissingFontErrorRecorded()
		{
			var options = GetHarvesterOptionsForProcessOneBookTests();
			var fakeFontChecker = Substitute.For<IFontChecker>();
			fakeFontChecker.Configure().GetMissingFonts(default, out bool success)
				.ReturnsForAnyArgs(args =>
				{
					args[1] = false; // Report failure
					return new List<string>();
				});

			using (var harvester = GetSubstituteHarvester(options, fontChecker: fakeFontChecker))
			{
				// Test Setup
				var book = new Book(new BookModel() { ObjectId = "123" }, _logger);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyRelevantErrors = logEntries.Any(x => x.Type == LogType.GetFontsError && x.Level == LogLevel.Error);
				Assert.That(anyRelevantErrors, Is.True, "The relevant error type was not found");
				Assert.That(book.Model.HarvestState, Is.EqualTo("Failed"), "HarvestState should be failed");

				// Just double-check that our stub did actually get called
				fakeFontChecker.ReceivedWithAnyArgs().GetMissingFonts(default, out _);

				// Validate that the code did in fact attempt to report an error
				_fakeIssueReporter.ReceivedWithAnyArgs().ReportError(default, default, default, default);
			}
		}

		[Test]
		public void ProcessOneBook_MissingFont_MissingFontErrorRecorded()
		{
			var options = GetHarvesterOptionsForProcessOneBookTests();

			// Stub setup
			var missingFonts = new List<string>();
			missingFonts.Add("madeUpFontName");
			var fakeFontChecker = Substitute.For<IFontChecker>();
			fakeFontChecker.Configure().GetMissingFonts(default, out bool success)
				.ReturnsForAnyArgs(args =>
					{
						args[1] = true;	// Report success
						return missingFonts;
					});

			using (var harvester = GetSubstituteHarvester(options, fontChecker: fakeFontChecker))
			{
				// Test Setup
				var book = new Book(new BookModel() { ObjectId = "123" }, _logger);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				var logEntries = book.Model.GetValidLogEntries();
				var anyRelevantErrors = logEntries.Any(x => x.Type == LogType.MissingFont && x.Level == LogLevel.Error);
				Assert.That(anyRelevantErrors, Is.True, "The relevant error type was not found");
				Assert.That(book.Model.HarvestState, Is.EqualTo("Failed"), "HarvestState should be failed");

				// Just double-check that our stub did actually get called
				fakeFontChecker.ReceivedWithAnyArgs().GetMissingFonts(default, out _);

				// Validate that the code did in fact attempt to report an error
				_fakeIssueReporter.Received().ReportMissingFont("madeUpFontName", "UnitTestHarvester", EnvironmentSetting.Test, book.Model);
			}
		}

		[Test]
		public void ProcessOneBook_NormalConditions_DirectoriesCleanedBeforeUpload()
		{
			// Mock Setup
			var options = new HarvesterOptions()
			{
				Mode = HarvestMode.Default,
				Environment = EnvironmentSetting.Local,
				SuppressLogs = true
			};

			using (var harvester = GetSubstituteHarvester(options))
			{
				// Test setup
				string baseUrl = "https://s3.amazonaws.com/FakeBucket/fakeUploader%40gmail.com%2fFakeGuid%2fFakeTitle%2f";
				var bookModel = new BookModel(baseUrl: baseUrl);
				var book = new Book(bookModel, _logger);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				_fakeS3UploadClient.DidNotReceive().DeleteDirectory("fakeUploader@gmail.com/FakeGuid");
				_fakeS3UploadClient.Received(1).DeleteDirectory("fakeUploader@gmail.com/FakeGuid/bloomdigital");
				_fakeS3UploadClient.Received(1).DeleteDirectory("fakeUploader@gmail.com/FakeGuid/epub");
				_fakeS3UploadClient.Received(1).DeleteDirectory("fakeUploader@gmail.com/FakeGuid/thumbnails");
			}
		}

		[Test]
		public void ProcessOneBook_SkipUploadVariousArtifacts_NoDirectoriesCleaned()
		{
			// Test Setup
			var options = new HarvesterOptions()
			{
				Mode = HarvestMode.Default,
				Environment = EnvironmentSetting.Local,
				SkipUploadBloomDigitalArtifacts = true,
				SkipUploadEPub = true,
				SkipUploadThumbnails = true,
			};

			using (var harvester = GetSubstituteHarvester(options))
			{
				string baseUrl = "https://s3.amazonaws.com/FakeBucket/fakeUploader%40gmail.com%2fFakeGuid%2fFakeTitle%2f";
				var bookModel = new BookModel(baseUrl: baseUrl);
				var book = new Book(bookModel, _logger);

				// System under test				
				harvester.ProcessOneBook(book);

				// Validate
				_fakeS3UploadClient.DidNotReceiveWithAnyArgs().DeleteDirectory(default);
			}
		}

		[Test]
		public void ProcessOneBook_DeletedBook_ParseUpdateFailureHandled()
		{
			// Mock Setup
			var options = new HarvesterOptions()
			{
				Mode = HarvestMode.Default,
				Environment = EnvironmentSetting.Local,
				SuppressLogs = true
			};
			var parseClient = Substitute.For<IParseClient>();
			parseClient.Configure().UpdateObject(default, default, default).ReturnsForAnyArgs(args =>
				throw new ParseException("Update failed.\r\nRequest.Json: {\"harvestState\":\"Done\",\"updateSource\":\"bloomHarvester\"}\r\nResponse.Code: NotFound\r\nResponse.Uri: https://bloom-parse-server-develop.azurewebsites.net/parse/classes/books/7Nfwo3hquq\r\nResponse.Description: Not Found\r\nResponse.Content: {\"code\":101,\"error\":\"Object not found.\"}"));
			var logger = new StringListLogger();
			using (var harvester = GetSubstituteHarvester(options, parseClient: parseClient,  logger: logger))
			{
				string baseUrl = "https://s3.amazonaws.com/FakeBucket/fakeUploader%40gmail.com%2fFakeGuid%2fFakeTitle%2f";
				var bookModel = new BookModel(baseUrl: baseUrl) {ObjectId = "123456789"};
				var book = new Book(bookModel, logger);

				// System under test
				harvester.ProcessOneBook(book);

				// Validate that the error was not logged to the model.
				var logEntries = book.Model.GetValidLogEntries();
				Assert.That(logEntries.Count, Is.EqualTo(0), "The error should not be logged to the model.");

				// Validate that the code did not in fact attempt to report an error to the issue tracker.
				_fakeIssueReporter.DidNotReceiveWithAnyArgs().ReportException(default, default, default, default);

				// Validate that we did at least log the error.
				Assert.That(logger.LogList.Count, Is.GreaterThan(2));
				var hadDeletionEvent = logger.LogList.Any(x => x.Contains("Event: Possible book deletion"));
				Assert.That(hadDeletionEvent, Is.True, "Book deletion event was not found");
				var hadDeletionWarning = logger.LogList.Any(x => x.Contains("Log Warn: ProcessOneBook - Exception caught, book") && x.Contains("may have been deleted."));
				Assert.That(hadDeletionWarning, Is.True, "Book deletion warning was not found");
			}
		}

		[Test]
		public void ProcessOneBook_DeletedBook_S3DownloadFailureHandled()
		{
			// Mock Setup
			var options = new HarvesterOptions()
			{
				Mode = HarvestMode.Default,
				Environment = EnvironmentSetting.Local,
				SuppressLogs = true
			};
			var transferClient = Substitute.For<IBookTransfer>();
			transferClient.Configure().HandleDownloadWithoutProgress(default, default).ReturnsForAnyArgs(args =>
				throw new DirectoryNotFoundException("The book we tried to download is no longer in the BloomLibrary"));
			var logger = new StringListLogger();
			using (var harvester = GetSubstituteHarvester(options, transferClient: transferClient,  logger: logger))
			{
				string baseUrl = "https://s3.amazonaws.com/FakeBucket/fakeUploader%40gmail.com%2fFakeGuid%2fFakeTitle%2f";
				var bookModel = new BookModel(baseUrl: baseUrl) {ObjectId = "123456789"};
				var book = new Book(bookModel, logger);

				// System under test
				harvester.ProcessOneBook(book);

				// Validate that the error was not logged to the model.
				var logEntries = book.Model.GetValidLogEntries();
				Assert.That(logEntries.Count, Is.EqualTo(0), "The error should not be logged to the model.");

				// Validate that the code did not in fact attempt to report an error to the issue tracker.
				_fakeIssueReporter.DidNotReceiveWithAnyArgs().ReportException(default, default, default, default);

				// Validate that we did at least log the error.
				Assert.That(logger.LogList.Count, Is.GreaterThan(2));
				var hadDeletionEvent = logger.LogList.Any(x => x.Contains("Event: Possible book deletion"));
				Assert.That(hadDeletionEvent, Is.True, "Book deletion event was not found");
				var hadDeletionWarning = logger.LogList.Any(x => x.Contains("Log Warn: ProcessOneBook - Exception caught, book") && x.Contains("may have been deleted."));
				Assert.That(hadDeletionWarning, Is.True, "Book deletion warning was not found");
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
