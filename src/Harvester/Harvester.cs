using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using Bloom.WebLibraryIntegration;
using BloomHarvester.LogEntries;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.WebLibraryIntegration;
using BloomTemp;
using Newtonsoft.Json.Linq;
using Book = BloomHarvester.Parse.Model.Book;

namespace BloomHarvester
{
	// This class is responsible for coordinating the running of the application logic
	public class Harvester : IDisposable
	{
		private const int kCreateArtifactsTimeoutSecs = 300;
		private const int kGetFontsTimeoutSecs = 20;
		private const int kDelayAfterEmptyRunSecs = 300;
		private const bool kEnableLoggingSkippedBooks = false;

		protected IMonitorLogger _logger;
		private ParseClient _parseClient;
		private EnvironmentSetting _parseDBEnvironment;
		private BookTransfer _transfer;
		private HarvesterS3Client _s3UploadClient;  // Note that we upload books to a different bucket than we download them from, so we have a separate client.
		private HarvesterOptions _options;
		private HashSet<string> _cumulativeFailedBookIdSet = new HashSet<string>();
		private HashSet<string> _missingFonts = new HashSet<string>();
		internal Version Version;
		private Random _rng = new Random();

		// These vars handle the application being exited while a book is still InProgress
		private string _currentBookId = null;   // The ID of the current book for as long as that book has the "InProgress" state set on it. Should be set back to null/empty when the state is no longer "InProgress"
		static ConsoleEventDelegate consoleExitHandler;
		private delegate bool ConsoleEventDelegate(int eventType);

		internal bool IsDebug { get; set; }
		public string Identifier { get; set; }


		public Harvester(HarvesterOptions options)
		{
			_options = options;
			var assemblyVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 0);
			this.Version = new Version(assemblyVersion.Major, assemblyVersion.Minor);	// Only consider the major and minor version

			// Note: If the same machine runs multiple BloomHarvester processes, then you need to add a suffix to this.
			this.Identifier = Environment.MachineName;

			if (options.SuppressLogs)
			{
				_logger = new ConsoleLogger();
			}
			else
			{
				EnvironmentSetting azureMonitorEnvironment = EnvironmentUtils.GetEnvOrFallback(options.LogEnvironment, options.Environment);
				_logger = new AzureMonitorLogger(azureMonitorEnvironment, this.Identifier);
			}

			// Setup Parse Client and S3 Clients
			_parseDBEnvironment = EnvironmentUtils.GetEnvOrFallback(options.ParseDBEnvironment, options.Environment);
			_parseClient = new ParseClient(_parseDBEnvironment);
			_parseClient.Logger = _logger;

			string downloadBucketName;
			string uploadBucketName;
			switch (_parseDBEnvironment)
			{
				case EnvironmentSetting.Prod:
					downloadBucketName = BloomS3Client.ProductionBucketName;
					uploadBucketName = HarvesterS3Client.HarvesterProductionBucketName;
					break;
				case EnvironmentSetting.Test:
					downloadBucketName = BloomS3Client.UnitTestBucketName;
					uploadBucketName = HarvesterS3Client.HarvesterUnitTestBucketName;
					break;
				case EnvironmentSetting.Dev:
				case EnvironmentSetting.Local:
				default:
					downloadBucketName = BloomS3Client.SandboxBucketName;
					uploadBucketName = HarvesterS3Client.HarvesterSandboxBucketName;
					break;
			}
			_transfer = new BookTransfer(_parseClient,
				bloomS3Client: new HarvesterS3Client(downloadBucketName, _parseDBEnvironment, true),
				htmlThumbnailer: null,
				bookDownloadStartingEvent: new Bloom.BookDownloadStartingEvent());

			_s3UploadClient = new HarvesterS3Client(uploadBucketName, _parseDBEnvironment, false);

			// Setup a handler that is called when the console is closed
			consoleExitHandler = new ConsoleEventDelegate(ConsoleEventCallback);
			SetConsoleCtrlHandler(consoleExitHandler, add: true);
		}

		public void Dispose()
		{
			_parseClient.FlushBatchableOperations();
			_logger.Dispose();
		}

		/// <summary>
		/// Handles control signals received by the process
		/// https://docs.microsoft.com/en-us/windows/console/handlerroutine
		/// </summary>
		/// <param name="eventType"></param>
		/// <returns>True if the event was handled by this function. False otherwise (i.e, let any subsequent handlers take a stab at it)</returns>
		bool ConsoleEventCallback(int eventType)
		{
			// See https://stackoverflow.com/a/4647168 for reference

			if (eventType == 2) // CTRL_CLOSE_EVENT - The console is being closed
			{
				// The console is being closed but it looks like we were in the middle of processing some book (which may or may not succeed if allowed to finish).
				// Before closing, try to update the state in the database so that it's not stuck in "InProgress"
				if (!String.IsNullOrEmpty(_currentBookId))
				{
					var updateOp = new BookUpdateOperation();
					updateOp.UpdateFieldWithString(Book.kHarvestStateField, Parse.Model.HarvestState.Aborted.ToString());
					_parseClient.UpdateObject(Book.GetStaticParseClassName(), _currentBookId, updateOp.ToJson());
					return true;
				}
			}

			return false;
		}

		[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

		public static void RunHarvest(HarvesterOptions options)
		{
			Console.Out.WriteLine($"Command Line Options: \n" + options.GetPrettyPrint());
			Console.Out.WriteLine();

			using (Harvester harvester = new Harvester(options))
			{
				harvester.Harvest(maxBooksToProcess: options.Count, queryWhereJson: options.QueryWhere);
			}
		}

		/// <summary>
		/// Process all rows in the books table
		/// Public interface should use RunHarvest() function instead. (So that we can guarantee that the class instance is properly disposed).
		/// </summary>
		/// 
		/// <param name="maxBooksToProcess"></param>
		private void Harvest(int maxBooksToProcess = -1, string queryWhereJson = "")
		{
			_logger.TrackEvent($"Harvest{_options.Mode} Start");

			string additionalWhereFilters = GetQueryWhereOptimizations();
			string combinedWhereJson = Harvester.InsertQueryWhereOptimizations(queryWhereJson, additionalWhereFilters);
			Console.Out.WriteLine("combinedWhereJson: " + combinedWhereJson);

			do
			{
				try
				{
					Console.Out.WriteLine();

					var methodStopwatch = new Stopwatch();
					methodStopwatch.Start();

					IEnumerable<Book> bookList = _parseClient.GetBooks(combinedWhereJson);

					// Prioritize New books first.
					// It would be nice to push this into the Parse query, but the "order" parameter seems to only allow sorting by a field. Can't find any info about sorting by more complicated expressions.
					//
					// Current assumptions are that the program is normally run in Default Mode with count=1 and loop flag set.
					// Also that it does not take long to get the results of the Parse query or sort them.
					// In that scenario I think it makes sense to just do a single Parse query
					// But if assumptions change (like if usual call parameters change or it becomes expensive to retrieve all the columns from Parse), it could become worthwhile to split into multiple Parse queries.
					bookList = bookList.OrderByDescending(x => x.HarvestState == Parse.Model.HarvestState.New.ToString())   // TRUE (1) cases first, FALSE (0) cases second. i.e. State=New first, then everything else
						.ThenByDescending(x => x.HarvestState == Parse.Model.HarvestState.Updated.ToString())	// State=Updated first, then everything else.
						// Enhance: Could also add another level to check whether the book's updatedTime < start time of the program. It would help in situations like where mode=all, count=1, and loop=true to ensure that every book gets processed once first before probably re-doing some books. But this is not normally needed when mode=default
						.ThenBy(x => _rng.Next());   // Randomize within each section.

					int numBooksProcessed = 0;

					var skippedBooks = new List<Book>();
					var failedBooks = new List<Book>();	// Only the list from the current iteration, not the total cumulative list

					foreach (var book in bookList)
					{
						// Decide if we should process it.
						bool shouldBeProcessed = ShouldProcessBook(book, out string reason);
						if (shouldBeProcessed || kEnableLoggingSkippedBooks)
						{
							_logger.LogInfo($"{book.ObjectId} - {reason}");
						}

						if (!shouldBeProcessed)
						{
							skippedBooks.Add(book);

							if (book.HarvestState == Parse.Model.HarvestState.Done.ToString())
							{
								// Something else has marked this book as no longer failed
								_cumulativeFailedBookIdSet.Remove(book.ObjectId);
							}

							continue;
						}

						bool isSuccessful = ProcessOneBook(book);
						if (isSuccessful)
						{
							// We know this book is no longer failed
							_cumulativeFailedBookIdSet.Remove(book.ObjectId);
						}
						else
						{
							failedBooks.Add(book);
						}
						++numBooksProcessed;

						if (maxBooksToProcess > 0 && numBooksProcessed >= maxBooksToProcess)
						{
							break;
						}
					}

					_parseClient.FlushBatchableOperations();
					methodStopwatch.Stop();
					Console.Out.WriteLine($"Harvest{_options.Mode} took {(methodStopwatch.ElapsedMilliseconds / 1000.0):0.0} seconds.");

					if (kEnableLoggingSkippedBooks && skippedBooks.Any())
					{
						// There is a flag enabled for logging these because it can be useful to see in the development phase, but not likely to be useful when it's running normally.
						string warningMessage = "Skipped Book ObjectIds:\n\t" + String.Join("\t", skippedBooks.Select(x => x.ObjectId));
						_logger.LogVerbose(warningMessage);
					}

					if (_cumulativeFailedBookIdSet?.Any() == true)
					{
						var sample = _cumulativeFailedBookIdSet.Take(10);
						string errorMessage = $"Books with outstanding errors from previous iterations (sample of {sample.Count()}):\n\t" + String.Join("\n\t", sample.Select(id => $"ObjectId: {id}"));
						_logger.LogInfo(errorMessage);
					}

					if (failedBooks.Any())
					{
						string errorMessage = "Books with errors (this iteration only):\n\t" + String.Join("\n\t", failedBooks.Select(x => $"ObjectId: {x.ObjectId}.  URL: {x.BaseUrl}"));
						_logger.LogError(errorMessage);

						_cumulativeFailedBookIdSet?.UnionWith(failedBooks.Select(x => x.ObjectId));
					}


					int numBooksFailed = failedBooks.Count;
					int numBooksSkipped = skippedBooks.Count;
					int numBooksTotal = numBooksSkipped + numBooksProcessed;
					int numBooksSuccess = numBooksProcessed - numBooksFailed;
					_logger.LogInfo($"Success={numBooksSuccess}, Failed={numBooksFailed}, Skipped={numBooksSkipped}, Total={numBooksTotal}.");

					if (numBooksFailed > 0)
					{
						double percentFailed = ((double)numBooksFailed) / numBooksTotal * 100;
						if (percentFailed > 0 && percentFailed < 0.1)
						{
							percentFailed = 0.1;    // Don't round non-zero numbers down to 0. It might make the log misleading.
						}
						_logger.LogError($"Failures ({percentFailed:0.0}% failed)");
					}

					if (_options.Loop)
					{
						_logger.TrackEvent($"Harvest{_options.Mode} Loop Iteration completed.");

						if (numBooksProcessed == 0)
						{
							var estimatedResumeTime = DateTime.Now.AddSeconds(kDelayAfterEmptyRunSecs);
							Console.Out.WriteLine($"Waiting till: {estimatedResumeTime.ToString("h:mm:ss tt")}...");
							Thread.Sleep(kDelayAfterEmptyRunSecs * 1000);
						}
						else
						{
#if DEBUG
							Thread.Sleep(5);   // Just for debugging purposes to see what's going on
#endif
						}
					}
				}
				catch (Exception e)
				{
					try
					{
						YouTrackIssueConnector.ReportExceptionToYouTrack(e, $"Unhandled exception thrown while running Harvest() function.", _parseDBEnvironment, exitImmediately: false);
					}
					catch (Exception)
					{
						// There was an error in reporting the error...
						// That's unfortunate, but we don't want to propagate another exception higher up. Because that would suspend our loop
						Console.Error.WriteLine("Exception thrown while attempting to report exception to YouTrack");
					}
				}

			} while (_options.Loop);

			_logger.TrackEvent($"Harvest{_options.Mode} End");
		}

		internal string GetQueryWhereOptimizations()
		{
			// These filters should keep every book we need to process, but it's ok to include some books we don't need to process. We will still call ShouldProcessBook later to do more checking.
			string whereOptimizationConditions = "";

			string majorVersionFilter = "\"$or\": [{\"harvesterMajorVersion\" : { \"$lte\": " + this.Version.Major + "}}, {\"harvesterMajorVersion\": {\"$exists\": false}}]";
			switch (_options.Mode)
			{
				case HarvestMode.All:
					break;
				case HarvestMode.NewOrUpdatedOnly:
					whereOptimizationConditions = "\"harvestState\" : { \"$in\": [\"New\", \"Updated\", \"Unknown\"]}";
					break;
				case HarvestMode.RetryFailuresOnly:
					whereOptimizationConditions = "\"harvestState\": \"Failed\", " + majorVersionFilter;
					break;
				case HarvestMode.Default:
				default:
					whereOptimizationConditions = majorVersionFilter;
					break;
			}

			return whereOptimizationConditions;
		}

		internal static string InsertQueryWhereOptimizations(string userInputQueryWhere, string whereOptimization)
		{
			if (String.IsNullOrWhiteSpace(userInputQueryWhere) || userInputQueryWhere == "{}" || userInputQueryWhere == "{ }")
			{
				return "{" + whereOptimization + "}";
			}

			var userInputJson = JObject.Parse(userInputQueryWhere);
			var additionalJson  = JObject.Parse("{" + whereOptimization + "}");
			userInputJson.Merge(additionalJson, new JsonMergeSettings
			{
				MergeArrayHandling = MergeArrayHandling.Union
			});

			string jsonString = userInputJson.ToString();
			jsonString = jsonString.Replace("\r", "");
			jsonString = jsonString.Replace("\n", "");
			string previousString;
			do
			{
				previousString = jsonString;
				jsonString = jsonString.Replace("  ", " ");
			} while (previousString != jsonString);

			return jsonString;
		}
				
		private bool ProcessOneBook(Book book)
		{
			bool isSuccessful = true;
			try
			{
				string message = $"Processing: {book.BaseUrl}";
				_logger.LogVerbose(message);

				_logger.TrackEvent("ProcessOneBook Start");	// After we check ShouldProcessBook

				// Parse DB initial updates
				var initialUpdates = new BookUpdateOperation();
				initialUpdates.UpdateFieldWithString(Book.kHarvestStateField, Parse.Model.HarvestState.InProgress.ToString());
				initialUpdates.UpdateFieldWithString(Book.kHarvesterIdField, this.Identifier);
				initialUpdates.UpdateFieldWithNumber(Book.kHarvesterMajorVersionField, Version.Major.ToString());
				initialUpdates.UpdateFieldWithNumber(Book.kHarvesterMinorVersionField, Version.Minor.ToString());
				var startTime = new Parse.Model.Date(DateTime.UtcNow);
				initialUpdates.UpdateFieldWithJson("harvestStartedAt", startTime.ToJson());
				if (!_options.ReadOnly)
				{
					_currentBookId = book.ObjectId;
					_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, initialUpdates.ToJson());
				}

				// Download the book
				_logger.TrackEvent("Download Book");
				string decodedUrl = HttpUtility.UrlDecode(book.BaseUrl);
				string urlWithoutTitle = RemoveBookTitleFromBaseUrl(decodedUrl);
				string downloadRootDir = Path.Combine(Path.GetTempPath(), Path.Combine("BloomHarvester", this.Identifier));
				_logger.LogVerbose("Download Dir: {0}", downloadRootDir);
				Bloom.Program.RunningHarvesterMode = true;	// HandleDownloadWithoutProgress has a nested subcall to BloomS3Client.cs::AvoidThisFile() which looks at HarvesterMode
				string downloadBookDir = _transfer.HandleDownloadWithoutProgress(urlWithoutTitle, downloadRootDir);

				// Process the book
				var finalUpdates = new BookUpdateOperation();
				List<BaseLogEntry> harvestLogEntries = CheckForMissingFontErrors(downloadBookDir, book);
				bool anyFontsMissing = harvestLogEntries.Any();
				isSuccessful &= !anyFontsMissing;

				// More processing
				if (isSuccessful)
				{
					var warnings = FindBookWarnings(book);
					harvestLogEntries.AddRange(warnings);

					if (_options.ReadOnly)
						return true;

					var analyzer = BookAnalyzer.FromFolder(downloadBookDir);
					var collectionFilePath = analyzer.WriteBloomCollection(downloadBookDir);

					isSuccessful &= CreateArtifacts(decodedUrl, downloadBookDir, collectionFilePath, book.ObjectId);
				}

				// Finalize the state
				finalUpdates.UpdateFieldWithJson(Book.kHarvestLogField, Book.ToJson(harvestLogEntries.Select(x => x.ToString())));
				if (isSuccessful)
				{
					finalUpdates.UpdateFieldWithString(Book.kHarvestStateField, Parse.Model.HarvestState.Done.ToString());
				}
				else
				{
					finalUpdates.UpdateFieldWithString(Book.kHarvestStateField, Parse.Model.HarvestState.Failed.ToString());
				}

				// Write the updates
				if (!_options.ReadOnly)
				{
					_currentBookId = null;
					_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, finalUpdates.ToJson());
				}

				// Cleanup the download directory if everything was successful.
				// (If it failed, I guess it's fine to skip deleting it because having the download around makes debugging easier)
				SIL.IO.RobustIO.DeleteDirectoryAndContents(downloadBookDir);

				_logger.TrackEvent("ProcessOneBook End - " + (isSuccessful ? "Success" : "Error"));
			}
			catch (Exception e)
			{
				isSuccessful = false;
				string bookId = book?.ObjectId ?? "null";
				string bookUrl = book?.BaseUrl ?? "null";
				YouTrackIssueConnector.ReportExceptionToYouTrack(e, $"Unhandled exception thrown while processing book objectId={bookId}, baseUrl={bookUrl}", _parseDBEnvironment, exitImmediately: false);

				// Attempt to write to Parse that processing failed
				if (!String.IsNullOrEmpty(book?.ObjectId))
				{
					try
					{
						var onErrorUpdates = new BookUpdateOperation();
						onErrorUpdates.UpdateFieldWithString(Book.kHarvestStateField, Parse.Model.HarvestState.Failed.ToString());
						onErrorUpdates.UpdateFieldWithString(Book.kHarvesterIdField, this.Identifier);
						_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, onErrorUpdates.ToJson());
					}
					catch (Exception)
					{
						// If it fails, just let it be and report the first exception rather than the nested exception.
					}
				}
			}

			return isSuccessful;
		}

		private bool ShouldProcessBook(Book book, out string reason)
		{
			return ShouldProcessBook(book, _options.Mode, this.Version, out reason);
		}

		/// <summary>
		/// Determines whether or not a book should be processed by the current harvester
		/// </summary>
		/// <param name="book"></param>
		/// <param name="reason">If the method returns true, then reason must be assigned with an explanation of why the book was selected for processing</param>
		/// <returns>Returns true if the book should be processed</returns>
		public static bool ShouldProcessBook(Book book, HarvestMode harvestMode, Version currentVersion, out string reason)
		{
			Debug.Assert(book != null, "ShouldProcessBook(): Book was null");

			if (!Enum.TryParse(book.HarvestState, out Parse.Model.HarvestState state))
			{
				state = Parse.Model.HarvestState.Unknown;
			}
			bool isNewOrUpdatedState = (state == Parse.Model.HarvestState.New || state == Parse.Model.HarvestState.Updated);

			// This is an important exception-to-the-rule case for almost every scenario,
			// so let's get it out of the way first.
			bool isStaleState = false;
			if (state == Parse.Model.HarvestState.InProgress)
			{
				if (harvestMode == HarvestMode.ForceAll)
				{
					reason = "PROCESS: Mode = HarvestForceAll";
					return true;
				}

				// In general, we just skip and let whoever else is working on it do its thing to avoid any potential for getting into strange states.
				// But if it's been "InProgress" for a suspiciously long time, it seems like it might've crashed. In that case, consider processing it.
				TimeSpan timeDifference = DateTime.UtcNow - book.HarvestStartedAt.UtcTime;
				if (timeDifference.TotalDays < 2)
				{
					reason = "SKIP: Recently in progress";
					return false;
				}
				else
				{
					isStaleState = true;
				}
			}


			if (harvestMode == HarvestMode.All || harvestMode == HarvestMode.ForceAll)
			{
				// If settings say to process all books, this is easy. We always return true.
				reason = $"PROCESS: Mode = Harvest{harvestMode}";
				return true;
			}
			else if (harvestMode == HarvestMode.NewOrUpdatedOnly)
			{
				if (isNewOrUpdatedState)
				{
					reason = "PROCESS: New or Updated state";
					return true;
				}
				else
				{
					reason = "SKIP: Not new or updated.";
					return false;
				}
			}
			else if (harvestMode == HarvestMode.RetryFailuresOnly)
			{
				Version previouslyUsedVersion = new Version(book.HarvesterMajorVersion, book.HarvesterMinorVersion);
				if (previouslyUsedVersion > currentVersion)
				{
					// A newer version previously marked it as failed. Don't touch this book.
					reason = "SKIP: Previously processed by newer version.";
					return false;
				}
				else
				{
					reason = "PROCESS: Retrying failure.";
					return true;
				}
			}
			else if (harvestMode == HarvestMode.Default)
			{
				Version previouslyUsedVersion = new Version(book.HarvesterMajorVersion, book.HarvesterMinorVersion);
				switch (state)
				{
					case Parse.Model.HarvestState.New:
					case Parse.Model.HarvestState.Updated:
					case Parse.Model.HarvestState.Unknown:
					default:
						reason = "PROCESS: New or Updated state";
						return true;
					case Parse.Model.HarvestState.Done:
						if (currentVersion.Major > book.HarvesterMajorVersion)
						{
							reason = "PROCESS: Updated major version, so updating output";
							return true;
						}
						else
						{
							reason = "SKIP: Already processed successfully.";
							return false;
						}
					case Parse.Model.HarvestState.Aborted:
						if (currentVersion >= previouslyUsedVersion)
						{
							reason = "PROCESS: Re-starting book that was previously aborted";
							return true;
						}
						else
						{
							reason = "SKIP: Skipping aborted book that was previously touched by a newer version.";
							return false;
						}
					case Parse.Model.HarvestState.InProgress:
						if (!isStaleState)
						{
							reason = "SKIP: Recently in progress";
							return false;
						}
						else if (currentVersion > previouslyUsedVersion)
						{
							reason = "PROCESS: Retrying stuck book of older version.";
							return true;
						}
						else if (currentVersion == previouslyUsedVersion)
						{
							reason = "PROCESS: Retrying stuck book of current version.";
							return true;
						}
						else
						{
							reason = "SKIP: Skipping stuck book that was previously processed by a newer version.";
							return false;
						}
					case Parse.Model.HarvestState.Failed:
						if (currentVersion > previouslyUsedVersion)
						{
							// ENHANCE: Should we bother checking if the font is still missing?

							// Current is at least a minor version newer than what we had before
							// Default to true (re-try failures), unless we have reason to think that it's still pretty hopeless for the book to succeed.
							var logEntryList = GetValidBaseLogEntries(book.HarvestLogEntries);
							if (logEntryList != null)
							{
								var previouslyMissingFontNames = logEntryList.Where(x => x is MissingFontError).Select(x => (x as MissingFontError).FontName);

								if (previouslyMissingFontNames.Any())
								{
									var stillMissingFontNames = GetMissingFonts(previouslyMissingFontNames);

									if (stillMissingFontNames.Any())
									{
										reason = $"SKIP: Still missing font {stillMissingFontNames.First()}";
										return false;
									}
								}
							}

							reason = "PROCESS: Retry-ing failed book of older version.";
							return true;
						}
						else if (currentVersion == previouslyUsedVersion)
						{
							reason = "SKIP: Marked as failed by current version.";
							return false;
						}
						{
							reason = "SKIP: Marked as failed by newer version.";
							return false;
						}
				}
			}
			else
			{
				throw new ArgumentException("Unexpected mode: " + harvestMode);
			}
		}

		private static IEnumerable<BaseLogEntry> GetValidBaseLogEntries(List<string> logEntryStrList)
		{
			if (logEntryStrList == null)
			{
				return null;
			}

			return logEntryStrList.Select(str => BaseLogEntry.ParseFromLogEntry(str)).Where(x => x != null);
		}

		// Precondition: Assumes that baseUrl is not URL-encoded, and that it ends with the book title as a subfolder.
		public static string RemoveBookTitleFromBaseUrl(string baseUrl)
		{
			if (String.IsNullOrEmpty(baseUrl))
			{
				return baseUrl;
			}

			int length = baseUrl.Length;
			if (baseUrl.EndsWith("/"))
			{
				// Don't bother processing trailing slash
				--length;
			}

			int lastSlashIndex = baseUrl.LastIndexOf('/', length - 1);

			string urlWithoutTitle = baseUrl;
			if (lastSlashIndex >= 0)
			{
				urlWithoutTitle = baseUrl.Substring(0, lastSlashIndex);
			}

			return urlWithoutTitle;
		}

		/// <summary>
		/// Determines whether any warnings regarding a book should be displayed to the user on Bloom Library
		/// </summary>
		/// <param name="book">The book to check</param>
		/// <returns></returns>
		private List<BaseLogEntry> FindBookWarnings(Book book)
		{
			var warnings = new List<BaseLogEntry>();

			if (book == null)
			{
				return warnings;
			}

			if (String.IsNullOrWhiteSpace(book.BaseUrl))
			{
				warnings.Add(new MissingBaseUrlWarning());
			}

			if (warnings.Any())
			{
				_logger.LogWarn("Warnings: " + String.Join(";", warnings.Select(x => x.ToString())));
			}

			return warnings;
		}

		// Returns true if at least one font is missing
		private List<BaseLogEntry> CheckForMissingFontErrors(string bookPath, Book book)
		{
			var harvestLogEntries = new List<BaseLogEntry>();

			var missingFontsForCurrBook = GetMissingFonts(bookPath);
			bool areAnyFontsMissing = missingFontsForCurrBook != null && missingFontsForCurrBook.Any();
			if (areAnyFontsMissing)
			{
				_logger.LogWarn("Missing fonts: " + String.Join(",", missingFontsForCurrBook));

				foreach (var missingFont in missingFontsForCurrBook)
				{
					var logEntry = new LogEntries.MissingFontError(missingFont);
					harvestLogEntries.Add(logEntry);

					if (!_missingFonts.Contains(missingFont))
					{
						YouTrackIssueConnector.ReportMissingFontToYouTrack(missingFont, this.Identifier, _parseDBEnvironment, book);
						_missingFonts.Add(missingFont);
					}
					else
					{
						// We already know that this font is missing, which means we already reported an issue to YouTrack. No need to re-report it.
						Console.Out.WriteLine("Missing font, but no issue created because already known: " + missingFont);
					}
				}
			}

			return harvestLogEntries;
		}

		/// <summary>
		/// Gets the names of the fonts referenced in the book but not found on this machine.
		/// </summary>
		/// <param name="bookPath">The path to the book folder</param>
		private List<string> GetMissingFonts(string bookPath)
		{
			var missingFonts = new List<string>();

			using (var reportFile = SIL.IO.TempFile.CreateAndGetPathButDontMakeTheFile())
			{
				string bloomArguments = $"getfonts --bookpath \"{bookPath}\" --reportpath \"{reportFile.Path}\"";
				bool success = StartAndWaitForBloomCli(bloomArguments, kGetFontsTimeoutSecs, out int exitCode, out string stdOut, out string stdError);

				if (!success)
				{
					_logger.LogError("Error: Could not determine fonts from book located at " + bookPath);
					return missingFonts;
				}

				var bookFontNames = GetFontsFromReportFile(reportFile.Path);
				missingFonts = GetMissingFonts(bookFontNames);
			}

			return missingFonts;
		}

		private static List<string> GetMissingFonts(IEnumerable<string> bookFontNames)
		{			
			var computerFontNames = GetInstalledFontNames();

			var missingFonts = new List<string>();
			foreach (var bookFontName in bookFontNames)
			{
				if (!String.IsNullOrEmpty(bookFontName) && !computerFontNames.Contains(bookFontName))
				{
					missingFonts.Add(bookFontName);
				}
			}

			return missingFonts;
		}

		/// <summary>
		/// Gets the fonts referenced by a book baesd on a "getfonts" report file. 
		/// </summary>
		/// <param name="filePath">The path to the report file generated from Bloom's "getfonts" CLI command. Each line of the file should correspond to 1 font name.</param>
		/// <returns>A list of strings, one for each font referenced by the book.</returns>
		private List<string> GetFontsFromReportFile(string filePath)
		{
			var referencedFonts = new List<string>();

			string[] lines = File.ReadAllLines(filePath);   // Not expecting many lines in this file

			if (lines != null)
			{
				foreach (var fontName in lines)
				{
					referencedFonts.Add(fontName);
				}
			}

			return referencedFonts;
		}

		// Returns the names of each of the installed font families as a set of strings
		private static HashSet<string> GetInstalledFontNames()
		{
			var installedFontCollection = new System.Drawing.Text.InstalledFontCollection();

			var fontFamilyDict = new HashSet<string>(installedFontCollection.Families.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
			return fontFamilyDict;
		}

		private bool CreateArtifacts(string downloadUrl, string downloadBookDir, string collectionFilePath, string bookId)
		{
			bool success = true;

			using (var folderForUnzipped = new TemporaryFolder("BloomHarvesterStagingUnzipped"))
			{
				using (var folderForZipped = new TemporaryFolder("BloomHarvesterStagingZipped"))
				{
					var components = new S3UrlComponents(downloadUrl);
					string zippedBloomDOutputPath = Path.Combine(folderForZipped.FolderPath, $"{components.BookTitle}.bloomd");
					string epubOutputPath = Path.Combine(folderForZipped.FolderPath, $"{components.BookTitle}.epub");

					string bloomArguments = $"createArtifacts \"--bookPath={downloadBookDir}\" \"--collectionPath={collectionFilePath}\" \"--bloomdOutputPath={zippedBloomDOutputPath}\" \"--bloomDigitalOutputPath={folderForUnzipped.FolderPath}\" \"--epubOutputPath={epubOutputPath}\"";

					// Start a Bloom command line in a separate process
					_logger.LogVerbose("Starting Bloom CLI process");
					var bloomCliStopwatch = new Stopwatch();
					bloomCliStopwatch.Start();
					bool exitedNormally = StartAndWaitForBloomCli(bloomArguments, kCreateArtifactsTimeoutSecs * 1000, out int bloomExitCode, out string bloomStdOut, out string bloomStdErr);
					bloomCliStopwatch.Stop();

					string errorDetails = $"Book: {bookId ?? "null"}\n";
					if (exitedNormally)
					{
						if (bloomExitCode == 0)
						{
							_logger.LogVerbose($"CreateArtifacts finished successfully in {bloomCliStopwatch.Elapsed.TotalSeconds:0.0} seconds.");
						}
						else
						{
							success = false;
							errorDetails += $"Bloom Command Line error:\nCreateArtifacts failed with exit code: {bloomExitCode}.";
						}
					}
					else
					{
						success = false;
						errorDetails += $"Bloom Command Line error:\nCreateArtifacts terminated because it exceeded {kCreateArtifactsTimeoutSecs} seconds. Book Title: {components.BookTitle}.";
					}

					if (!success)
					{
						// Usually better just to report these right away. If BloomCLI didn't succeed, the subsequent Upload...() methods will probably throw an exception, except it'll be more confusing because it's not directly related to the root cause anymore.
						_logger.LogError(errorDetails);
						errorDetails += $"\n===StandardOut===\n{bloomStdOut ?? ""}\n";
						errorDetails += $"\n===StandardError===\n{bloomStdErr ?? ""}";
						YouTrackIssueConnector.ReportErrorToYouTrack("Harvester BloomCLI Error", errorDetails, _parseDBEnvironment);
					}
					else
					{
						string s3FolderLocation = $"{components.Submitter}/{components.BookGuid}";

						UploadBloomDigitalArtifacts(zippedBloomDOutputPath, folderForUnzipped.FolderPath, s3FolderLocation);
						UploadEPubArtifact(epubOutputPath, s3FolderLocation);
					}
				}
			}

			return success;
		}

		/// <summary>
		/// Starts a Bloom instance in a new process and waits up to the specified amount of time for it to finish.
		/// </summary>
		/// <param name="arguments">The arguments to pass to Bloom. (Don't include the name of the executable.)</param>
		/// <param name="timeoutMilliseconds">After this amount of time, the process will be killed if it's still running</param>
		/// <param name="exitCode">Out parameter. The exit code of the process.</param>
		/// <param name="standardOutput">Out parameter. The standard output of the process.</param>
		/// <param name="standardError">Out parameter. The standard error of the process.</param>
		/// <returns>Returns true if the process ended by itself without timeout. Returns false if the process was forcibly terminated.</returns>
		public static bool StartAndWaitForBloomCli(string arguments, int timeoutMilliseconds, out int exitCode, out string standardOutput, out string standardError)
		{
			var process = new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					FileName = "Bloom.exe",
					Arguments = arguments,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				}
			};

			StringBuilder processErrorBuffer = new StringBuilder();
			process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => { processErrorBuffer.Append(e.Data); });			

			process.Start();

			// These ReadToEnd() calls are filled with deadlock potential if you write them naively.
			// See this official documentation for details on proper usage:
			// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput?redirectedfrom=MSDN&view=netframework-4.8#System_Diagnostics_ProcessStartInfo_RedirectStandardOutput
			//
			// Highlights: You shouldn't have WaitForExit() followed by ReadToEnd(). It will deadlock if the new process writes enough to fill the buffer.
			//             You shouldn't have ReadToEnd() of both stdout and stderr. It will deadlock if the new process writes enough to fill the buffer.
			standardOutput = process.StandardOutput.ReadToEnd();
			process.BeginErrorReadLine();

			// Block and wait for it to finish
			bool hasExited = process.WaitForExit(timeoutMilliseconds);
			standardError = processErrorBuffer.ToString();

			bool isExitedNormally = true;
			if (!hasExited)
			{
				try
				{
					process.Kill();
					isExitedNormally = false;
				}
				catch
				{
					// Just make a best effort to kill the process, but no need to throw exception if it didn't work
				}
			}

			exitCode = process.ExitCode;

			if (!String.IsNullOrWhiteSpace(standardOutput))
				Console.Out.WriteLine("Standard out: " + standardOutput);
			if (!String.IsNullOrWhiteSpace(standardError))
				Console.Out.WriteLine("Standard error: " + standardError);

			return isExitedNormally;
		}

		/// <summary>
		/// Uploads the .bloomd and the bloomdigital folders to S3
		/// </summary>
		/// <param name="zippedBloomDPath">The .bloomd file (zipped) path on this machine</param>
		/// <param name="unzippedFolderPath">The bloomdigital folder (unzipped) path on this machine</param>
		/// <param name="s3FolderLocation">The S3 path to upload to</param>
		private void UploadBloomDigitalArtifacts(string zippedBloomDPath, string unzippedFolderPath, string s3FolderLocation)
		{
			_logger.TrackEvent("Upload .bloomd");
			_s3UploadClient.UploadFile(zippedBloomDPath, s3FolderLocation);

			_logger.TrackEvent("Upload bloomdigital directory");
			_s3UploadClient.UploadDirectory(unzippedFolderPath,
				$"{s3FolderLocation}/bloomdigital");
		}

		// This function doesn't wrap much, but I made so that when studying the stack trace of exceptions, we could distinguish errors uploading .bloomd vs .epub files.
		/// <summary>
		/// Uploads an EPub to S3
		/// </summary>
		/// <param name="epubPath">The current location of an ePub on this machine</param>
		/// <param name="s3FolderLocation">The S3 path to upload to</param>
		private void UploadEPubArtifact(string epubPath, string s3FolderLocation)
		{
			_logger.TrackEvent("Upload .epub");
			_s3UploadClient.UploadFile(epubPath, $"{s3FolderLocation}/epub");
		}

		/// <summary>
		/// This function is here to allow setting the harvesterState to specific values to aid in setting up specific ad-hoc testing states.
		/// In the Parse database, there are some rules that automatically set the harvestState to "Updated" when the book is republished.
		/// Unfortunately, this rule also kicks in when a book is modified in the Parse dashboard or via the API Console (if no updateSource is set) :(
		/// 
		/// But executing this function allows you to set it to a value other than "Updated"
		/// </summary>
		internal static void UpdateHarvesterState(EnvironmentSetting parseDbEnvironment, string objectId, Parse.Model.HarvestState newState)
		{
			var updateOp = new BookUpdateOperation();
			updateOp.UpdateFieldWithString(Book.kHarvestStateField, newState.ToString());

			EnvironmentSetting environment = EnvironmentUtils.GetEnvOrFallback(parseDbEnvironment, EnvironmentSetting.Default);
			var parseClient = new ParseClient(environment);
			parseClient.UpdateObject(Book.GetStaticParseClassName(), objectId, updateOp.ToJson());
			parseClient.FlushBatchableOperations();

			Console.Out.WriteLine($"Evnironment={parseDbEnvironment}: Sent request to update object \"{objectId}\" with harvestState={newState}");
		}
	}
}
