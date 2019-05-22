using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BloomHarvester.Logger;
using BloomHarvester.WebLibraryIntegration;

namespace BloomHarvester
{
	// This class is responsible for coordinating the running of the application logic
	public class Harvester : IDisposable
	{
		protected IMonitorLogger _logger;
		private ParseClient _parseClient;

		public bool IsDebug { get; set; }

		public Harvester(HarvestAllOptions options)
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

		// Public interface should use RunHarvestAll() function instead. (So that we can guarantee that the class instance is properly disposed).
		private void HarvestAll(int maxBooksToProcess = -1)
		{
			_logger.TrackEvent("HarvestAll Start");

			int numBooksProcessed = 0;

			IEnumerable<string> bookList = _parseClient.GetBookUrlList();
			foreach (string bookBaseUrl in bookList)
			{
				ProcessOneBook(bookBaseUrl);
				++numBooksProcessed;

				if (maxBooksToProcess > 0 && numBooksProcessed >= maxBooksToProcess)
				{
					break;
				}
			}

			_logger.TrackEvent("HarvestAll End - Success");
		}

		private void ProcessOneBook(string bookBaseUrl)
		{
			try
			{
				_logger.TrackEvent("ProcessOneBook Start");
				string message = $"Processing: {bookBaseUrl}";
				Console.Out.WriteLine(message);
				_logger.LogVerbose(message);

				_logger.TrackEvent("ProcessOneBook End - Success");
			}
			catch (Exception e)
			{
				YouTrackIssueConnector.SubmitToYouTrack(e, $"Unhandled exception thrown while processing book \"{bookBaseUrl}\"");
				throw;
			}
		}
	}
}
