using BloomHarvester.IO;
using BloomHarvester.LogEntries;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using BloomHarvester.WebLibraryIntegration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace BloomHarvester
{
	public class FontAnalytics : IDisposable
	{
		public static void SendAnalytics(SendFontAnalyticsOptions options)
		{
			using (var fontAnalytics = new FontAnalytics(options))
			{
				fontAnalytics.SendFontAnalytics();
			}
		}

		private const int kSendAnalyticsTimeoutSecs = 600;	// 5 minutes may be overgenerous.

		private readonly IMonitorLogger _logger = new ConsoleLogger();
		private readonly SendFontAnalyticsOptions _options;
		private readonly IParseClient _parseClient;
		private readonly IS3Client _bloomS3Client;
		private readonly IFileIO _fileIO;
		private readonly Version Version;
		private readonly IDiskSpaceManager _diskSpaceManager;
		private readonly IBookDownload _downloadClient;
		private readonly IBloomCliInvoker _bloomCli;

		public FontAnalytics(SendFontAnalyticsOptions options)
		{
			this._options = options;
			var issueReporter = YouTrackIssueConnector.GetInstance(_options.Environment);
			issueReporter.Disabled = true;
			var parseClient = new ParseClient(_options.Environment, _logger);
			_fileIO = new FileIO();
			var assemblyVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 0);
			this.Version = new Version(assemblyVersion.Major, assemblyVersion.Minor);	// Only consider the major and minor version
			var driveInfo = Harvester.GetHarvesterDrive();
			_diskSpaceManager = new DiskSpaceManager(driveInfo, _logger, issueReporter);
			(string downloadBucketName, string uploadBucketName) = Harvester.GetS3BucketNames(_options.Environment);
			var s3DownloadClient = new HarvesterS3Client(downloadBucketName, _options.Environment, true);
			_downloadClient = new HarvesterBookDownload(parseClient,
				bloomS3Client: s3DownloadClient);
			_parseClient = parseClient;
			_bloomS3Client = s3DownloadClient;
			_bloomCli = new BloomCliInvoker(_logger);
		}

		private void SendFontAnalytics()
		{
			_logger.TrackEvent($"SendFontAnalytics Start");

			string queryWhereJson = _options.QueryWhere ?? "";
			var additionalWhereFilters = GetQueryWhereOptimizations();
			string combinedWhereJson = Harvester.InsertQueryWhereOptimizations(queryWhereJson, additionalWhereFilters);
			Console.Out.WriteLine("combinedWhereJson: " + combinedWhereJson);
			try
			{
				Console.Out.WriteLine();
				var methodStopwatch = new Stopwatch();
				methodStopwatch.Start();

				IEnumerable<BookModel> bookList = _parseClient.GetBooks(out bool didExitPrematurely, combinedWhereJson);
				if (didExitPrematurely)
				{
					// If GetBooks exited prematurely (i.e., partial results),
					// then we don't want to risk the user getting confused with only some of the intended books getting processed.
					// So we just abort it. All or nothing.
					_logger.LogError("SendFontAnalytics/GetBooks() encountered an error and did not return all results. Aborting.");
					return;
				}
				if (bookList == null)
				{
					_logger.LogInfo("SendFontAnalytics/GetBooks() did not return any books. Quitting.");
					return;
				}

				int numBooksProcessed = 0;
				var skippedBooks = new List<BookModel>();
				var failedBooks = new List<BookModel>();	// Only the list from the current iteration, not the total cumulative list

				foreach (var bookModel in bookList)
				{
					try
					{
						var book = new Book(bookModel, _logger, _fileIO);
						bool shouldBeProcessed = ShouldProcessBook(bookModel, HarvestMode.All, this.Version, out string reason);
						_logger.LogInfo($"{bookModel.ObjectId} - {reason}");
						if (!shouldBeProcessed)
						{
							skippedBooks.Add(bookModel);
							continue;
						}
						bool isSuccessful = ProcessOneBook(book);
						if (!isSuccessful)
						{
							failedBooks.Add(bookModel);
						}
						++numBooksProcessed;
					}
					catch (Exception e)
					{
						Console.WriteLine("Exception caught processing {0}: {1}", bookModel.BaseUrl, e);
						break;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception caught getting books to process: {0}", e);
			}
		}

		private bool ShouldProcessBook(BookModel book, HarvestMode harvestMode, Version version, out string reason)
		{
			Debug.Assert(book != null, "ShouldProcessBook(): Book was null");

			if (!Enum.TryParse(book.HarvestState, out HarvestState state))
			{
				throw new Exception($"Invalid book.HarvestState \"{book.HarvestState}\" for book.ObjectId=\"{book.ObjectId}\"");
			}

			if (state == HarvestState.FailedIndefinitely)
			{
				reason = "SKIP: Marked as failed indefinitely";
				return false;
			}

			// Skip books that are explicitly marked as out of circulation.
			// Note: Beware, IsInCirculation can also be null, and we DO want to process books where isInCirculation==null
			if (book.IsInCirculation == false)
			{
				reason = "SKIP: Not in circulation";
				return false;
			}

			switch (state)
			{
				case HarvestState.Requested:
				case HarvestState.New:
				case HarvestState.Updated:
				case HarvestState.Unknown:
				default:
					reason = "SKIP: Harvest not done.";
					return false;
				case HarvestState.Done:
					reason = "PROCESS: Harvest successfully done.";
					return HasPublicArtifacts(book, ref reason);
				case HarvestState.Aborted:
					reason = "SKIP: Harvest was aborted.";
					return false;
				case HarvestState.InProgress:
					reason = "SKIP: Harvest is in progress";
					return false;
				case HarvestState.Failed:
					reason = "SKIP: Harvest failed.";
					return false;
			}
		}

		private bool _epubVisible;
		private bool _pdfVisible;
		private bool _bloompubVisible;

		private bool HasPublicArtifacts(BookModel book, ref string reason)
		{
			var show = book.Show;
			if (show == null)
			{
				// If there's no information at all, silence means consent.
				return true;
			}
			_epubVisible = GetArtifactVisibility(show.epub);
			_pdfVisible = GetArtifactVisibility(show.pdf);
			_bloompubVisible = GetArtifactVisibility(show.bloomReader);
			if (_epubVisible || _pdfVisible || _bloompubVisible)
				return true;
			reason = "SKIP: Harvest successfully done, but no artifacts are visible";
			return false;
		}

		private static bool GetArtifactVisibility(dynamic artifact)
		{
			// This logic is the same as in BloomLibrary2 / ArtifactVisibilitySetttings.get decision()
			if (artifact != null)
			{
				var exists = artifact.exists;
				if (exists != null && exists.Value == false)
					return false;
				var user = artifact.user;
				if (user != null)
					return user.Value;
				var librarian = artifact.librarian;
				if (librarian != null)
					return librarian.Value;
				var harvester = artifact.harvester;
				if (harvester != null)
					return harvester.Value;
			}
			// If there's no information, silence means consent.
			return true;
		}

		private string _currentBookId;

		private bool _ePubSuitable;
		private bool _pdfExists;

		private bool ProcessOneBook(Book book)
		{
			bool isSuccessful = true;
			string collectionBookDir = null;
			try
			{
				var logEntries = new List<LogEntry>();

				string message = $"Processing: {book.Model.BaseUrl}";
				_logger.LogVerbose(message);
				_logger.TrackEvent("ProcessOneBook Start"); // After we check ShouldProcessBook

				_currentBookId = book.Model.ObjectId;

				// Download the book
				string decodedUrl = HttpUtility.UrlDecode(book.Model.BaseUrl);
				var originalBookModel  = (BookModel)book.Model.Clone();
				collectionBookDir = Harvester.DownloadBookAndCopyToCollectionFolder(book, decodedUrl, originalBookModel,
					_logger, _diskSpaceManager, _downloadClient, _options.Environment, _options.ForceDownload, false);

				// Process the book
				var warnings = book.FindBookWarnings();
				foreach (var entry in warnings)
					_logger.LogWarn(entry.ToString());

				var analyzer = GetAnalyzer(collectionBookDir);
				var collectionFilePath = analyzer.WriteBloomCollection(collectionBookDir);
				book.Analyzer = analyzer;
				_ePubSuitable = analyzer.IsEpubSuitable(logEntries);
				_pdfExists = Harvester.CheckIfPdfExists(decodedUrl, _options.Environment, _bloomS3Client);

				isSuccessful &= SendAnalyticsForBook(decodedUrl, collectionBookDir, collectionFilePath, book);

				_logger.TrackEvent("ProcessOneBook End - " + (isSuccessful ? "Success" : "Error"));
			}
			catch (Exception e)
			{
				isSuccessful = false;
				string bookId = book.Model?.ObjectId ?? "null";
				string bookUrl = book.Model?.BaseUrl ?? "null";
				string errorMessage = $"Unhandled exception \"{e.Message}\" thrown.";
				// On rare occasions, someone may delete a book just as we're processing it.
				var skipBugReport = bookId != "null" && bookUrl != "null" &&
				                    ((e is ParseException && errorMessage.Contains("Response.Code: NotFound")) ||
				                     (e is DirectoryNotFoundException && errorMessage.Contains("tried to download")));
				if (skipBugReport)
				{
					_logger.TrackEvent("Possible book deletion");
					var msgFormat =
						$"ProcessOneBook - Exception caught, book {bookId} ({bookUrl}) may have been deleted.{Environment.NewLine}{{0}}";
					_logger.LogWarn(msgFormat, e.Message);
					return isSuccessful;
				}
				else
				{
					_logger.LogError(errorMessage);
				}
			}
			finally
			{
				// clean up after ourselves: we only need to preserve the copy in the download cache folder.
				if (Directory.Exists(collectionBookDir))
					Directory.Delete(collectionBookDir, true);
			}

			return isSuccessful;
		}

		private bool SendAnalyticsForBook(string decodedUrl, string collectionBookDir, string collectionFilePath, Book book)
		{
			Debug.Assert(book != null, "SendAnalyticsForBook(): book expected to be non-null");

			var success = true;
			var argsBldr = new StringBuilder();
			argsBldr.Append($"sendFontAnalytics \"--collectionPath={collectionFilePath}\"");
			
			if (_options.Testing || _options.Environment != EnvironmentSetting.Prod)
				argsBldr.Append(" --testing");
			if (!_epubVisible || !_ePubSuitable)
				argsBldr.Append(" --skipEpubAnalytics");
			if (!_pdfVisible || !_pdfExists)
				argsBldr.Append(" --skipPdfAnalytics");
			argsBldr.Append($" \"{collectionBookDir}\"");
			var bloomCliStopwatch = new Stopwatch();
			bloomCliStopwatch.Start();
			var exitedNormally = _bloomCli.StartAndWaitForBloomCli(argsBldr.ToString(), kSendAnalyticsTimeoutSecs * 1000, out int bloomExitCode, out string bloomStdOut, out string bloomStdErr);
			bloomCliStopwatch.Stop();
			if (exitedNormally)
			{
				if (bloomExitCode == 0)
				{
					_logger.LogVerbose($"SendFontAnalytics finished successfully in {bloomCliStopwatch.Elapsed.TotalSeconds:0.0} seconds.");
				}
				else
				{
					success = false;
					var errorMessage = $"Bloom Command Line error: SentFontAnalytics failed with exit code: {bloomExitCode}.";
					_logger.LogError(errorMessage);
				}
			}
			else
			{
				success = false;
				var errorMessage = $"Bloom Command Line error: SentFontAnalytics terminated because it exceeded {kSendAnalyticsTimeoutSecs} seconds.";
				_logger.LogError(errorMessage);
			}
			if (!success)
			{
				var errorDetails = $"\n===StandardOut===\n{bloomStdOut ?? ""}\n";
				errorDetails += $"\n===StandardError===\n{bloomStdErr ?? ""}";
				_logger.LogError(errorDetails);
			}
			return success;
		}

		internal virtual IBookAnalyzer GetAnalyzer(string collectionBookDir)
		{
			return BookAnalyzer.FromFolder(collectionBookDir);
		}

		internal List<string> GetQueryWhereOptimizations()
		{
			var whereOptimizationConditions = new List<string>();
			var inCirculation = "\"$or\":[{\"inCirculation\":true},{\"inCirculation\":{\"$exists\":false}}]";
			whereOptimizationConditions.Add(inCirculation);
			return whereOptimizationConditions;
		}

		#region IDisposable Support
		private bool _isDisposed = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					_logger.Dispose();
				}
				_isDisposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}
		#endregion
	}
}
