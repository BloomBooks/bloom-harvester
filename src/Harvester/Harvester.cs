using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		internal bool IsDebug { get; set; }

		public Harvester(HarvesterCommonOptions options)
		{
			if (options.SuppressLogs)
			{
				_logger = new ConsoleLogger();
			}
			else
			{
				EnvironmentSetting azureMonitorEnvironment = EnvironmentUtils.GetEnvOrFallback(options.LogEnvironment, options.Environment);
				_logger = new AzureMonitorLogger(azureMonitorEnvironment);
			}

			EnvironmentSetting parseDBEnvironment = EnvironmentUtils.GetEnvOrFallback(options.ParseDBEnvironment, options.Environment);
			_parseClient = new ParseClient(parseDBEnvironment);
			_parseClient.Logger = _logger;
		}

		public void Dispose()
		{
			_logger.Dispose();
		}

		public static void RunHarvestAll(HarvestAllOptions options)
		{
			Console.Out.WriteLine("Command Line Options: \n" + options.GetPrettyPrint());

			using (Harvester harvester = new Harvester(options))
			{
				harvester.HarvestAll(maxBooksToProcess: options.Count);
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

		// Public interface should use RunHarvestAll() function instead. (So that we can guarantee that the class instance is properly disposed).
		private void HarvestAll(int maxBooksToProcess = -1)
		{
			_logger.TrackEvent("HarvestAll Start");
			var methodStopwatch = new Stopwatch();
			methodStopwatch.Start();

			int numBooksProcessed = 0;

			IEnumerable<Book> bookList = _parseClient.GetBooks();
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

				var warnings = FindBookWarnings(book);

				string pBookId = _parseClient.GetPublishedBookByBookId(book.ObjectId);
				if (pBookId != null)
				{
					// REVIEW: DELETE vs. UPDATE?
					// The spec says to delete then create
					// Pros: This is easier to code up
					// Cons:
					//   The objectId will change
					//   * Not sure, but potentially that could make it more annoying to debug or communicate since we'll have to look up new objectIds all the time.
					//   * If some other table has a foreign key on publishedBooks.objectId, it will be really annoying because we need to update multiple places.
					//   *   (Quite possibly annoying enough that we would preclude the possibility of ever having a foreign key on this?)
					//   * Created Time is less informative

					// We're going to overwrite any existing entries, so delete it if necessary

					// ENHANCE: Maybe we should delete all rows that match the book? Even though it's not SUPPOSED to happen, the table could get messed up.
					_parseClient.RequestDeleteObject("publishedBooks", pBookId);
				}

				PublishedBook publishedBook = new PublishedBook()
				{
					BookValue = book,
					Warnings = warnings
				};

				string json = publishedBook.GetJson();
				_parseClient.RequestCreateObject("publishedBooks", json);

				_logger.TrackEvent("ProcessOneBook End - Success");
			}
			catch (Exception e)
			{
				YouTrackIssueConnector.SubmitToYouTrack(e, $"Unhandled exception thrown while processing book \"{book.BaseUrl}\"");
				throw;
			}
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
				warnings.Add("Invalid baseUrl");
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
			var publishedBooks = _parseClient.GetPublishedBooksWithWarnings();

			// ENHANCE: Currently this is just a dummy implementation to test that we can walk through it and check
			//   One day we might actually want to re-process these if there is a code minor version update or something
			foreach (var pBook in publishedBooks)
			{
				string message = $"{pBook.BookPointer?.ObjectId ?? ""}: {pBook.Warnings.Count} warnings.";
				Console.Out.WriteLine(message);
			}
		}
	}
}
