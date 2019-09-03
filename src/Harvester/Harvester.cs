using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Bloom;
using Bloom.Collection;
using Bloom.WebLibraryIntegration;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.WebLibraryIntegration;
using BloomTemp;
using Book = BloomHarvester.Parse.Model.Book;

namespace BloomHarvester
{
	// This class is responsible for coordinating the running of the application logic
	public class Harvester : IDisposable
	{
		private const int kCreateArtifactsTimeoutSecs = 120;    // TODO: Maybe bump it up to 5 min (300 secs) after development stabilized
		private const int kGetFontsTimeoutSecs = 20;

		protected IMonitorLogger _logger;
		private ParseClient _parseClient;
		private BookTransfer _transfer;
		private HarvesterS3Client _s3UploadClient;  // Note that we upload books to a different bucket than we download them from, so we have a separate client.
		private HarvesterCommonOptions _options;
		private List<Book> _failedBooks = new List<Book>();
		private HashSet<string> _missingFonts = new HashSet<string>();

		internal bool IsDebug { get; set; }
		public string Identifier { get; set; }


		public Harvester(HarvesterCommonOptions options)
		{
			_options = options;
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

			EnvironmentSetting parseDBEnvironment = EnvironmentUtils.GetEnvOrFallback(options.ParseDBEnvironment, options.Environment);
			_parseClient = new ParseClient(parseDBEnvironment);
			_parseClient.Logger = _logger;

			string downloadBucketName;
			string uploadBucketName;
			switch (parseDBEnvironment)
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
				bloomS3Client: new HarvesterS3Client(downloadBucketName, parseDBEnvironment, true),
				htmlThumbnailer: null,
				bookDownloadStartingEvent: new BookDownloadStartingEvent());

			_s3UploadClient = new HarvesterS3Client(uploadBucketName, parseDBEnvironment, false);
		}

		public void Dispose()
		{
			_parseClient.FlushBatchableOperations();
			_logger.Dispose();
		}

		public static void RunHarvestAll(HarvestAllOptions options)
		{
			Console.Out.WriteLine("Command Line Options: \n" + options.GetPrettyPrint());

			using (Harvester harvester = new Harvester(options))
			{
				harvester.HarvestAll(maxBooksToProcess: options.Count, queryWhereJson: options.QueryWhere);
			}
		}

		public static void RunHarvestWarnings(HarvestWarningsOptions options)
		{
			Console.Out.WriteLine("Command Line Options: \n" + options.GetPrettyPrint());

			using (Harvester harvester = new Harvester(options))
			{
				harvester.HarvestWarnings();
			}
		}

		/// <summary>
		/// Process all rows in the books table
		/// Public interface should use RunHarvestAll() function instead. (So that we can guarantee that the class instance is properly disposed).
		/// </summary>
		/// 
		/// <param name="maxBooksToProcess"></param>
		private bool HarvestAll(int maxBooksToProcess = -1, string queryWhereJson = "")
		{
			_logger.TrackEvent("HarvestAll Start");
			var methodStopwatch = new Stopwatch();
			methodStopwatch.Start();

			int numBooksProcessed = 0;
			int numBooksFailed = 0;

			IEnumerable<Book> bookList = _parseClient.GetBooks(queryWhereJson);

			CollectionSettings.HarvesterMode = true;
			foreach (var book in bookList)
			{
				bool success = ProcessOneBook(book);
				if (!success)
				{
					++numBooksFailed;
					_failedBooks.Add(book);
				}

				++numBooksProcessed;

				if (maxBooksToProcess > 0 && numBooksProcessed >= maxBooksToProcess)
				{
					break;
				}
			}

			_parseClient.FlushBatchableOperations();
			methodStopwatch.Stop();
			Console.Out.WriteLine($"HarvestAll took {(methodStopwatch.ElapsedMilliseconds / 1000.0):0.0} seconds.");

			bool isSuccess = true;
			if (_failedBooks != null && _failedBooks.Any())
			{
				isSuccess = false;
				double percentFailed = ((double)numBooksFailed) / numBooksProcessed * 100;
				_logger.LogError($"Errors encounted: {numBooksFailed} books failed out of {numBooksProcessed} total ({percentFailed:0.0}% failed)");
				
				string errorMessage = "Books with errors:\n\t" + String.Join("\n\t", _failedBooks.Select(x => $"ObjectId: {x.ObjectId}.  URL: {x.BaseUrl}"));
				_logger.LogError(errorMessage);
			}

			_logger.TrackEvent("HarvestAll End");

			return isSuccess;
		}
				
		private bool ProcessOneBook(Book book)
		{
			bool isSuccessful = true;
			try
			{
				_logger.TrackEvent("ProcessOneBook Start");
				string message = $"Processing: {book.BaseUrl}";
				Console.Out.WriteLine(message);
				_logger.LogVerbose(message);

				var initialUpdates = new UpdateOperation();
				initialUpdates.UpdateField(Book.kHarvestStateField, Book.HarvestState.InProgress.ToString());
				initialUpdates.UpdateField(Book.kHarvesterIdField, this.Identifier);

				var startTime = new Parse.Model.Date(DateTime.UtcNow);
				initialUpdates.UpdateField("harvestStartedAt", startTime.ToJson());

				if (!_options.ReadOnly)
				{
					_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, initialUpdates.ToJson());
				}

				// Download the book
				_logger.TrackEvent("Download Book");
				string decodedUrl = HttpUtility.UrlDecode(book.BaseUrl);
				string urlWithoutTitle = RemoveBookTitleFromBaseUrl(decodedUrl);
				string downloadRootDir = Path.Combine(Path.GetTempPath(), Path.Combine("BloomHarvester", this.Identifier));
				_logger.LogVerbose("Download Dir: {0}", downloadRootDir);
				string downloadBookDir = _transfer.HandleDownloadWithoutProgress(urlWithoutTitle, downloadRootDir);

				// Process the book
				var finalUpdates = new UpdateOperation();
				List<string> harvestLogEntries = CheckForMissingFontErrors(downloadBookDir, book);
				bool anyFontsMissing = harvestLogEntries.Any();
				isSuccessful &= !anyFontsMissing;
				
				if (isSuccessful)
				{
					var warnings = FindBookWarnings(book);
					harvestLogEntries.AddRange(warnings);
					
					if (_options.ReadOnly)
						return isSuccessful;

					var analyzer = BookAnalyzer.FromFolder(downloadBookDir);
					var collectionFilePath = analyzer.WriteBloomCollection(downloadBookDir);

					isSuccessful &= CreateArtifacts(decodedUrl, downloadBookDir, collectionFilePath);
				}

				finalUpdates.UpdateField(Book.kHarvestLogField, Book.ToJson(harvestLogEntries));
				if (isSuccessful)
				{
					finalUpdates.UpdateField(Book.kHarvestStateField, Book.HarvestState.Done.ToString());
				}
				else
				{
					finalUpdates.UpdateField(Book.kHarvestStateField, Book.HarvestState.Failed.ToString());
				}

				// Write the updates
				if (!_options.ReadOnly)
				{
					_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, finalUpdates.ToJson());
				}
				_logger.TrackEvent("ProcessOneBook End - " + (isSuccessful ? "Success" : "Error"));
			}
			catch (Exception e)
			{
				isSuccessful = false;
				YouTrackIssueConnector.ReportExceptionToYouTrack(e, $"Unhandled exception thrown while processing book \"{book.BaseUrl}\"", exitImmediately: false);

				// Attempt to write to Parse that processing failed
				if (!String.IsNullOrEmpty(book?.ObjectId))
				{
					try
					{
						var onErrorUpdates = new UpdateOperation();
						onErrorUpdates.UpdateField(Book.kHarvestStateField, $"\"{Book.HarvestState.Failed.ToString()}\"");
						onErrorUpdates.UpdateField(Book.kHarvesterIdField, this.Identifier);
						_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, onErrorUpdates.ToJson());
					}
					catch (Exception)
					{
						// If it fails, just let it be and throw the first exception rather than the nested exception.
					}
				}
			}

			return isSuccessful;
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
		private List<string> FindBookWarnings(Book book)
		{
			var warnings = new List<string>();

			if (book == null)
			{
				return warnings;
			}

			if (String.IsNullOrWhiteSpace(book.BaseUrl))
			{
				warnings.Add("WARN: Missing baseUrl");
			}

			if (warnings.Any())
			{
				_logger.LogWarn("Warnings: " + String.Join(";", warnings));
			}

			return warnings;
		}

		// Returns true if at least one font is missing
		private List<string> CheckForMissingFontErrors(string bookPath, Book book)
		{
			var harvestLogEntries = new List<string>();

			var missingFontsForCurrBook = GetMissingFonts(bookPath);
			bool areAnyFontsMissing = missingFontsForCurrBook != null && missingFontsForCurrBook.Any();
			if (areAnyFontsMissing)
			{
				_logger.LogWarn("Missing fonts: " + String.Join(",", missingFontsForCurrBook));
				_missingFonts.UnionWith(missingFontsForCurrBook);

				foreach (var missingFont in missingFontsForCurrBook)
				{
					harvestLogEntries.Add("Error: Missing font " + missingFont);
					YouTrackIssueConnector.ReportMissingFontToYouTrack(missingFont, this.Identifier, book);
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
					_logger.LogError("Error: Could not determine fonts from book locatedd at " + bookPath);
					return missingFonts;
				}

				var bookFontNames = GetFontsFromReportFile(reportFile.Path);
				var computerFontNames = GetInstalledFontNames();

				foreach (var bookFontName in bookFontNames)
				{
					if (!computerFontNames.Contains(bookFontName))
					{
						missingFonts.Add(bookFontName);
					}
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
		private HashSet<string> GetInstalledFontNames()
		{
			var installedFontCollection = new System.Drawing.Text.InstalledFontCollection();

			var fontFamilyDict = new HashSet<string>(installedFontCollection.Families.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
			return fontFamilyDict;
		}

		private bool CreateArtifacts(string downloadUrl, string downloadBookDir, string collectionFilePath)
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

					if (exitedNormally)
					{
						string logMessage = $"CreateArtifacts finished in {bloomCliStopwatch.Elapsed.TotalSeconds:0.0} seconds.";
						if (bloomExitCode == 0)
						{
							_logger.LogVerbose(logMessage);
						}
						else
						{
							success = false;
							logMessage += $"\nAbnormal exit code: {bloomExitCode}.";
							_logger.LogError(logMessage);
						}
					}
					else
					{
						success = false;
						_logger.LogError($"CreateArtifacts terminated because it exceeded {kCreateArtifactsTimeoutSecs} seconds. Book Title: {components.BookTitle}.");
					}

					string s3FolderLocation = $"{components.Submitter}/{components.BookGuid}";

					UploadBloomDigitalArtifacts(zippedBloomDOutputPath, folderForUnzipped.FolderPath, s3FolderLocation);
					UploadEPubArtifact(epubOutputPath, s3FolderLocation);
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

		private void HarvestWarnings()
		{
			var booksWithWarnings = _parseClient.GetBooksWithWarnings();

			// ENHANCE: Currently this is just a dummy implementation to test that we can walk through it and check
			//   One day we might actually want to re-process these if there is a code minor version update or something
			foreach (var book in booksWithWarnings)
			{
				string message = $"{book.ObjectId ?? ""} ({book.BaseUrl}): {book.Warnings.Count} warnings.";
				Console.Out.WriteLine(message);
			}
		}
	}
}
