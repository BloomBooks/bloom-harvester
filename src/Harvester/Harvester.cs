using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Bloom;
using Bloom.Book;
using Bloom.Collection;
using Bloom.Publish.Epub;
using Bloom.web;
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
		protected IMonitorLogger _logger;
		private ParseClient _parseClient;
		private BookTransfer _transfer;
		private HarvesterS3Client _s3UploadClient;  // Note that we upload books to a different bucket than we download them from, so we have a separate client.

		private ApplicationContainer _applicationContainer;
		private ProjectContext _projectContext;

		internal bool IsDebug { get; set; }

		public string Identifier { get; set; }

		public Harvester(HarvesterCommonOptions options)
		{
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

			_applicationContainer = new Bloom.ApplicationContainer();
			Bloom.Program.SetUpLocalization(_applicationContainer);
		}

		public void Dispose()
		{
			_parseClient.FlushBatchableOperations();
			_logger.Dispose();
			_applicationContainer.Dispose();
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
		private void HarvestAll(int maxBooksToProcess = -1, string queryWhereJson = "")
		{
			_logger.TrackEvent("HarvestAll Start");
			var methodStopwatch = new Stopwatch();
			methodStopwatch.Start();

			int numBooksProcessed = 0;

			IEnumerable<Book> bookList = _parseClient.GetBooks(queryWhereJson);
			// Various publishing steps use GeckoFx windows; this is required one-time initialization.
			Bloom.Browser.SetUpXulRunner();
			foreach (var book in bookList)
			{
				ProcessOneBook(book);
				++numBooksProcessed;

				if (maxBooksToProcess > 0 && numBooksProcessed >= maxBooksToProcess)
				{
					break;
				}
			}

			_parseClient.FlushBatchableOperations();
			methodStopwatch.Stop();
			Console.Out.WriteLine($"HarvestAll took {methodStopwatch.ElapsedMilliseconds / 1000.0} seconds.");

			_logger.TrackEvent("HarvestAll End - Success");
		}

		private void ProcessOneBook(Book book)
		{
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

				_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, initialUpdates.ToJson());

				// Download the book
				_logger.TrackEvent("Download Book");
				string decodedUrl = HttpUtility.UrlDecode(book.BaseUrl);
				string urlWithoutTitle = RemoveBookTitleFromBaseUrl(decodedUrl);
				string downloadRootDir = Path.Combine(Path.GetTempPath(), Path.Combine("BloomHarvester", this.Identifier));
				_logger.LogVerbose("Download Dir: {0}", downloadRootDir);
				string downloadBookDir = _transfer.HandleDownloadWithoutProgress(urlWithoutTitle, downloadRootDir);

				// Set up a project context
				var analyzer = BookAnalyzer.fromFolder(downloadBookDir);
				var collectionFilePath = analyzer.WriteBloomCollection(downloadBookDir);
				using (_projectContext = _applicationContainer.CreateProjectContext(collectionFilePath))
				{
					Bloom.Program.SetProjectContext(_projectContext);

					// Process the book
					var finalUpdates = new UpdateOperation();
					var warnings = FindBookWarnings(book);
					finalUpdates.UpdateField(Book.kWarningsField, Book.ToJson(warnings));

					// Make the .bloomd and /bloomdigital outputs
					UploadBloomD(decodedUrl, downloadBookDir);

					// And a default epub
					UploadEpub(decodedUrl, downloadBookDir);

					// Write the updates
					finalUpdates.UpdateField(Book.kHarvestStateField, Book.HarvestState.Done.ToString());
					_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, finalUpdates.ToJson());
				}

				_projectContext = null; // fail fast if we try to use it while we don't have one.

				_logger.TrackEvent("ProcessOneBook End - Success");
			}
			catch (Exception e)
			{
				YouTrackIssueConnector.SubmitToYouTrack(e, $"Unhandled exception thrown while processing book \"{book.BaseUrl}\"");

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
				throw;
			}
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
				warnings.Add("Missing baseUrl");
			}

			// ENHANCE: Add the real implementation one day, when we have a spec for what warnings we might actually want
			if (book.BaseUrl != null && book.BaseUrl.Contains("gmail"))
			{
				warnings.Add("Gmail user");
			}

			return warnings;
		}

		/// <summary>
		/// Converts a book to BloomD and uploads it for publishing to the bloomharvest bucket.
		/// </summary>
		/// <param name="downloadUrl">Precondition: The URL should not be encoded.</param>
		/// <param name="downloadBookDir"></param>
		private void UploadBloomD(string downloadUrl, string downloadBookDir)
		{
			var components = new S3UrlComponents(downloadUrl);

			Bloom.Program.RunningNonApplicationMode = true;

			Bloom.Book.BookServer bookServer = _projectContext.BookServer;
			using (var folderForUnzipped = new TemporaryFolder("BloomHarvesterStagingUnzipped"))
			{
				using (var folderForZipped = new TemporaryFolder("BloomHarvesterStagingZipped"))
				{
					string zippedBloomDOutputPath = Path.Combine(folderForZipped.FolderPath, $"{components.BookTitle}.bloomd");

					// Make the bloomd
					string unzippedPath = Bloom.Publish.Android.BloomReaderFileMaker.CreateBloomReaderBook(
						zippedBloomDOutputPath,
						downloadBookDir,
						bookServer,
						System.Drawing.Color.Azure,	// TODO: What should this be?
						new Bloom.web.NullWebSocketProgress(),
						folderForUnzipped);

					RenameFilesForHarvestUpload(unzippedPath);

					string s3FolderLocation = $"{components.Submitter}/{components.BookGuid}";

					_logger.TrackEvent("Upload .bloomd");
					_s3UploadClient.UploadFile(zippedBloomDOutputPath, s3FolderLocation);

					_logger.TrackEvent("Upload bloomdigital directory");
					_s3UploadClient.UploadDirectory(unzippedPath, $"{s3FolderLocation}/bloomdigital");
				}
			}
		}

		/// <summary>
		/// Converts a book to Epub and uploads it for publishing to the bloomharvest bucket.
		/// </summary>
		/// <param name="downloadUrl">Precondition: The URL should not be encoded.</param>
		/// <param name="downloadBookDir"></param>
		private void UploadEpub(string downloadUrl, string downloadBookDir)
		{
			var components = new S3UrlComponents(downloadUrl);

			Bloom.Program.RunningNonApplicationMode = true;

			Bloom.Book.BookServer bookServer = _projectContext.BookServer;
			BookThumbNailer thumbNailer = _projectContext.ThumbNailer;
			var maker = new EpubMaker(thumbNailer, bookServer);
			maker.Book = bookServer.GetBookFromBookInfo(new BookInfo(downloadBookDir, true));
			maker.Unpaginated = true; // so far they all are
			maker.OneAudioPerPage = true; // default used in EpubApi
			// Enhance: maybe we want book to have image descriptions on page? use reader font sizes?
			using (var folderForOutput = new TemporaryFolder("BloomHarvesterStagingEpub"))
			{
				string epubOutputPath = Path.Combine(folderForOutput.FolderPath, $"{components.BookTitle}.epub");

				// Make the epub
				maker.SaveEpub(epubOutputPath, new NullWebSocketProgress());

				string s3FolderLocation = $"{components.Submitter}/{components.BookGuid}/epub";

				_logger.TrackEvent("Upload .epub");
				_s3UploadClient.UploadFile(epubOutputPath, s3FolderLocation);
			}
		}

		// Consumers expect the file to be in index.htm name, not {title}.htm name.
		private static void RenameFilesForHarvestUpload(string bookDirectory)
		{
			string originalHtmFilePath = Bloom.Book.BookStorage.FindBookHtmlInFolder(bookDirectory);

			Debug.Assert(File.Exists(originalHtmFilePath), "Book HTM not found: " + originalHtmFilePath);
			if (File.Exists(originalHtmFilePath))
			{
				string newHtmFilePath = Path.Combine(bookDirectory, $"index.htm");
				File.Copy(originalHtmFilePath, newHtmFilePath);
				File.Delete(originalHtmFilePath);
			}
		}

		public static string GetBookTitleFromBookPath(string bookPath)
		{
			return Path.GetFileName(bookPath);
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
