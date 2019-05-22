using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace BloomHarvester.WebLibraryIntegration
{
	// Enhance: In the future, may be worth it to subclass the BloomDesktop one and take a dependency on it.
	// We could get the URLs and ApplicationID from the BlookDesktop base class. We would add our new Harvester-specific functions in our derived class.
	//
	// Right now, there's not enough common functionality to make it worthwhile. Maybe revisit this topic when the code becomes more stable.
	public class ParseClient
	{
		// Constructors
		public ParseClient(EnvironmentSetting environment)
		{
			string url;
			string environmentVariableName;

			switch (environment)
			{
				case EnvironmentSetting.Prod:
					url = "https://parse.bloomlibrary.org/";
					environmentVariableName = "BloomHarvesterParseAppIdProd";
					break;
				case EnvironmentSetting.Test:
					url = "https://bloom-parse-server-unittest.azurewebsites.net/parse";
					environmentVariableName = "BloomHarvesterParseAppIdTest";
					break;
				case EnvironmentSetting.Dev:
				default:
					//url = "https://dev-parse.bloomlibrary.org/";	// Theoretically, this link should work too and is preferred, but it doesn't work in the program for some reason even though it redirects correctly in the browser.
					url = "https://bloom-parse-server-develop.azurewebsites.net/parse";
					environmentVariableName = "BloomHarvesterParseAppIdDev";
					break;
			}

			_client = new RestClient(url);
			_applicationId = Environment.GetEnvironmentVariable(environmentVariableName);
			Debug.Assert(!String.IsNullOrWhiteSpace(_applicationId), "Parse Application ID is invalid. Retrieving books from Parse probably won't work. Consider checking your environment variables.");
		}

		// Fields and properties
		private RestClient _client;
		private string _applicationId;

		// Careful! Very well might be null
		public Logger.IMonitorLogger Logger { get; set; }


		// Methods
		private void SetCommonHeaders(RestRequest request)
		{
			request.AddHeader("X-Parse-Application-Id", _applicationId);
		}

		public IEnumerable<string> GetBookUrlList()
		{
			Logger?.TrackEvent("ParseClient::GetBookUrlList Start");

			int numProcessed = 0;
			int totalCount = 0;
			do
			{
				var request = new RestRequest("classes/books", Method.GET);
				SetCommonHeaders(request);
				request.AddParameter("count", "1");
				request.AddParameter("limit", "1000");   // The limit should probably be on the higher side. The fewer DB calls, the better, probably.
				request.AddParameter("skip", numProcessed.ToString());
				request.AddParameter("order", "updatedAt");
				request.AddParameter("keys", "object_id,baseUrl");

				Logger?.TrackEvent("ParseClient::GetBookUrlList Request Sent");
				var restResponse = _client.Execute(request);
				string responseJson = restResponse.Content;

				var response = JsonConvert.DeserializeObject<dynamic>(responseJson);
				totalCount = response.count;

				if (response.results.Count <= 0)
				{
					break;
				}

				for (int i = 0; i < response.results.Count; ++i)
				{
					yield return response.results[i].baseUrl;
				}

				numProcessed += response.results.Count;

				string message = $"GetBookUrlList Progress: {numProcessed} out of {totalCount}.";
				Console.Out.WriteLine(message);
				Logger?.LogVerbose(message);
			}
			while (numProcessed < totalCount);

			Logger?.TrackEvent("ParseClient::GetBookUrlList End - Success");
		}
	}
}
