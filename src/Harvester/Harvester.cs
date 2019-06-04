using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Bloom;
using Bloom.WebLibraryIntegration;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using BloomHarvester.WebLibraryIntegration;

namespace BloomHarvester
{
	// This class is responsible for coordinating the running of the application logic
	public class Harvester : IDisposable
	{
		protected IMonitorLogger _logger;
		private ParseClient _parseClient;
		private BookTransfer _transfer;

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
			_transfer = new BookTransfer(_parseClient,
				bloomS3Client: new HarvesterS3Client(BloomS3Client.SandboxBucketName),
				htmlThumbnailer: null,
				bookDownloadStartingEvent: new BookDownloadStartingEvent());
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
		private void HarvestAll(int maxBooksToProcess = -1, string queryWhereJson = "")
		{
			_logger.TrackEvent("HarvestAll Start");
			var methodStopwatch = new Stopwatch();
			methodStopwatch.Start();

			int numBooksProcessed = 0;

			IEnumerable<Book> bookList = _parseClient.GetBooks(queryWhereJson);
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
				string urlWithoutTitle = GetBookIdFromBaseUrl(book.BaseUrl);
				string downloadDir = Path.Combine(Path.GetTempPath(), Path.Combine("BloomHarvester", this.Identifier));
				_logger.LogVerbose("Download Dir: {0}", downloadDir);
				_transfer.HandleDownloadWithoutProgress(urlWithoutTitle, downloadDir);

				// Process the book
				var finalUpdates = new UpdateOperation();
				var warnings = FindBookWarnings(book);
				finalUpdates.UpdateField(Book.kWarningsField, Book.ToJson(warnings));

				// ENHANCE: Do more processing here

				// Write the updates
				finalUpdates.UpdateField(Book.kHarvestStateField, Book.HarvestState.Done.ToString());
				_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, finalUpdates.ToJson());

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

		// Precondition: Assumes that baseUrl is URL-encoded and ends with the book title as a subfolder.
		public static string GetBookIdFromBaseUrl(string baseUrl)
		{
			if (String.IsNullOrEmpty(baseUrl))
			{
				return baseUrl;
			}

			string decodedUrl = HttpUtility.UrlDecode(baseUrl);

			int length = decodedUrl.Length;
			if (decodedUrl.EndsWith("/"))
			{
				// Don't bother processing trailing slash
				--length;
			}

			int lastSlashIndex = decodedUrl.LastIndexOf('/', length - 1);

			string urlWithoutTitle = decodedUrl;
			if (lastSlashIndex >= 0)
			{
				urlWithoutTitle = decodedUrl.Substring(0, lastSlashIndex);
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
