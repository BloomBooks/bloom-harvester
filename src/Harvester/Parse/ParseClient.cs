using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace BloomHarvester.Parse
{
	// Enhance: In the future, may be worth it to subclass the BloomDesktop one and take a dependency on it.
	// We could get the URLs and ApplicationID from the BlookDesktop base class. We would add our new Harvester-specific functions in our derived class.
	//
	// Right now, there's not enough common functionality to make it worthwhile. Maybe revisit this topic when the code becomes more stable.
	internal class ParseClient : Bloom.WebLibraryIntegration.BloomParseClient
	{
		// Constructors
		internal ParseClient(EnvironmentSetting environment)
			:base(CreateRestClient(environment))
		{
			_environmentSetting = environment;
			this.ApplicationId = GetApplicationId(environment);
			Debug.Assert(!String.IsNullOrWhiteSpace(ApplicationId), "Parse Application ID is invalid. Retrieving books from Parse probably won't work. Consider checking your environment variables.");
		}

		private static RestClient CreateRestClient(EnvironmentSetting environment)
		{
			string url;
			switch (environment)
			{
				case EnvironmentSetting.Prod:
					// This URL gets redirected. It seems to work for reading, but not for writing. For that, we need the direct one.
					//url = "https://parse.bloomlibrary.org/";
					url = "https://bloom-parse-server-production.azurewebsites.net/parse";
					break;
				case EnvironmentSetting.Test:
					url = "https://bloom-parse-server-unittest.azurewebsites.net/parse";
					break;
				case EnvironmentSetting.Dev:
				default:
					//url = "https://dev-parse.bloomlibrary.org/";	// Theoretically, this link should work too and is preferred, but it doesn't work in the program for some reason even though it redirects correctly in the browser.
					url = "https://bloom-parse-server-develop.azurewebsites.net/parse";
					break;
				case EnvironmentSetting.Local:
					url = "http://localhost:1337/parse";
					break;
			}

			return new RestClient(url);
		}

		private static string GetApplicationId(EnvironmentSetting environment)
		{
			string appIdEnvVarKey;

			switch (environment)
			{
				case EnvironmentSetting.Prod:
					appIdEnvVarKey = "BloomHarvesterParseAppIdProd";
					break;
				case EnvironmentSetting.Test:
					appIdEnvVarKey = "BloomHarvesterParseAppIdTest";
					break;
				case EnvironmentSetting.Dev:
				default:
					appIdEnvVarKey = "BloomHarvesterParseAppIdDev";
					break;
				case EnvironmentSetting.Local:
					appIdEnvVarKey = null;
					break;
			}

			string applicationId = "myAppId";
			if (environment != EnvironmentSetting.Local)
			{
				applicationId = Environment.GetEnvironmentVariable(appIdEnvVarKey);
			}

			return applicationId;
		}

		// Fields and properties
		private EnvironmentSetting _environmentSetting;

		private const int kMaxBatchOpsToSend = 50;
		private List<BatchableOperation> _batchableOperations = new List<BatchableOperation>(kMaxBatchOpsToSend);

		// Careful! Very well might be null
		internal Logger.IMonitorLogger Logger { get; set; }


		// Methods
		/// <summary>
		/// Logs in, if necessary
		/// </summary>
		private void EnsureLogIn()
		{
			if (String.IsNullOrEmpty(_sessionToken))
			{
				LogIn();
			}
		}

		/// <summary>
		/// Sends a request to log in
		/// </summary>
		private void LogIn()
		{
			_sessionToken = String.Empty;

			// The officially recommended way to pass the password is via a URL parameter of a GET request
			// (https://docs.parseplatform.org/rest/guide/#logging-in)
			// If you connect via HTTPS, it should get a secure TCP connection first, then send the HTTP request over SSL...
			// Which means that having the password in plaintext in the URL (which is at the HTTP layer) isn't immediately terrible.
			// Other than the fact that the server could very well want to log every URL it processes into a log... or print it out in an exception...
			// Or that you don't need https for localhost..
			var request = MakeRequest("login", Method.GET);
			request.AddParameter("username", Environment.GetEnvironmentVariable($"BloomHarvesterUserName").ToLowerInvariant());
			request.AddParameter("password", Environment.GetEnvironmentVariable($"BloomHarvesterUserPassword{_environmentSetting}"));

			var response = this.Client.Execute(request);
			CheckForResponseError(response, "Failed to log in");

			var dy = JsonConvert.DeserializeObject<dynamic>(response.Content);
			_sessionToken = dy.sessionToken; //there's also an "error" in there if it fails, but a null sessionToken tells us all we need to know
		}

		/// <summary>
		/// Makes a request of the specified type, and sets common headers and authentication tokens
		/// </summary>
		/// <param name="path">The Parse relative path, e.g. "classes/{className}"</param>
		/// <param name="requestType">e.g., GET, POST, etc.</param>
		/// <returns></returns>
		private RestRequest MakeRequest(string path, Method requestType)
		{
			var request = new RestRequest(path, requestType);
			SetCommonHeaders(request);
			return request;
		}

		private void SetCommonHeaders(RestRequest request)
		{
			request.AddHeader("X-Parse-Application-Id", ApplicationId);
			if (!string.IsNullOrEmpty(_sessionToken))
			{
				request.AddHeader("X-Parse-Session-Token", _sessionToken);
			}
		}

		private void AddJsonToRequest(RestRequest request, string json)
		{
			request.AddParameter("application/json", json, ParameterType.RequestBody);
		}

		/// <summary>
		/// Creates an object in a Parse class (table)
		/// This method may take about half a second to complete.
		/// </summary>
		/// <param name="className">The name of the class (table). Do not prefix it with "classes/"</param>
		/// <param name="json">The JSON of the object to write</param>
		/// <exception>Throws an application exception if the request fails</exception>
		/// <returns>The response after executing the request</returns>
		internal IRestResponse CreateObject(string className, string json)
		{
			EnsureLogIn();
			var request = MakeRequest($"classes/{className}", Method.POST);
			AddJsonToRequest(request, json);

			var response = this.Client.Execute(request);
			CheckForResponseError(response, "Create failed.\nRequest.Json: {0}", json);

			return response;
		}

		/// <summary>
		/// Updates an object in a Parse class (table)
		/// This method may take about half a second to complete.
		/// </summary>
		/// <param name="className">The name of the class (table). Do not prefix it with "classes/"</param>
		/// <param name="json">The JSON of the object to update. It doesn't need to be the full object, just of the fields to update</param>
		/// <exception>Throws an application exception if the request fails</exception>
		/// <returns>The response after executing the request</returns>
		internal IRestResponse UpdateObject(string className, string objectId, string updateJson)
		{
			EnsureLogIn();
			var request = MakeRequest($"classes/{className}/{objectId}", Method.PUT);
			AddJsonToRequest(request, updateJson);

			var response = this.Client.Execute(request);
			CheckForResponseError(response, "Update failed.\nRequest.Json: {0}", updateJson);

			return response;
		}

		/// <summary>
		/// Deletes an object in a Parse class (table)
		/// This method may take about half a second to complete.
		/// </summary>
		/// <param name="className">The name of the class (table). Do not prefix it with "classes/"</param>
		/// <param name="objectId">The objectId of the object to delte</param>
		/// <exception>Throws an application exception if the request fails</exception>
		/// <returns>The response after executing the request</returns>
		internal IRestResponse DeleteObject(string className, string objectId)
		{
			EnsureLogIn();
			var request = MakeRequest($"classes/{className}/{objectId}", Method.DELETE);

			var response = this.Client.Execute(request);
			CheckForResponseError(response, "Delete of object {0} failed.", objectId);

			return response;
		}

		private void CheckForResponseError(IRestResponse response, string exceptionInfoFormat, params object[] args)
		{
			if (!IsResponseCodeSuccess(response.StatusCode))
			{
				throw new ParseException(response, exceptionInfoFormat + "\n", args);
			}
		}

		private static bool IsResponseCodeSuccess(HttpStatusCode statusCode)
		{
			return ((int)statusCode >= 200) && ((int)statusCode <= 299);
		}

		private static string GetResponseSummary(IRestResponse response, bool includeUri = true)
		{
			string summary =
				$"Response.Code: {response.StatusCode}\n" +
				(includeUri ? $"Response.Uri: {response.ResponseUri}\n" : "") +
				$"Response.Description: {response.StatusDescription}\n" +
				$"Response.Content: {response.Content}\n";
			return summary;
		}

		/// <summary>
		/// Schedules an object to be added the next time a batch of operations is sent.
		/// Remember to call FlushBatchableOperations at the end.
		/// A batch will also be sent automatically when the batch queue is full.
		/// </summary>
		/// <param name="className">The class to add to</param>
		/// <param name="objectId">The JSON of the object to create</param>
		internal void RequestCreateObject(string className, string json)
		{
			var requestedOperation = new BatchableOperation(RestSharp.Method.POST, $"classes/{className}", json);
			AddBatchableOperation(requestedOperation);
		}

		/// <summary>
		/// Schedules an object to be deleted the next time a batch of operations is sent.
		/// Remember to call FlushBatchableOperations at the end.
		/// A batch will also be sent automatically when the batch queue is full.
		/// </summary>
		/// <param name="className">The class to delete from</param>
		/// <param name="objectId">The object ID to delete</param>
		internal void RequestDeleteObject(string className, string objectId)
		{
			var requestedOperation = new BatchableOperation(RestSharp.Method.DELETE, $"classes/{className}/{objectId}", "{}");
			AddBatchableOperation(requestedOperation);
		}

		private void AddBatchableOperation(BatchableOperation requestedOperation)
		{
			// Note: it can take 0.5 seconds for each single Delete or Create operation,
			// so try to batch them up so it doesn't take as long
			//
			// One at a time, it took 493.84 seconds to process 439 records.
			// In batch, it took 101.93 seconds to process the same 439 records.
			// That's 493.84 / 101.93 = 4.8x speedup

			_batchableOperations.Add(requestedOperation);
			if (_batchableOperations.Count >= 50)
			{
				FlushBatchableOperations();
			}
		}

		internal void FlushBatchableOperations()
		{
			if (_batchableOperations?.Count > 0)
			{
				Console.Out.WriteLine($"Flushing {_batchableOperations.Count} operations to Parse.");

				int numProcessed = 0;
				do
				{
					// Parse can only do 50 at a time.
					// Get the JSON for the first 50 to send.
					var opsToSend = _batchableOperations.Skip(numProcessed).Take(kMaxBatchOpsToSend);
					string batchJson = GetBatchJson(opsToSend);

					// Prepare request
					EnsureLogIn();
					var request = MakeRequest("batch", Method.POST);
					AddJsonToRequest(request, batchJson);

					// Send the request
					Logger?.TrackEvent("ParseClient::FlushBatchableOperations Batch Request Sent");
					var response = this.Client.Execute(request);

					// Check for a complete request error
					CheckForResponseError(response, "FlushBatchableOperations failed. JSON={0}", batchJson);

					// Check if some (or all) of the batch operations errored out
					string responseJson = response.Content;
					JArray responseObjArray = JArray.Parse(responseJson);
					if (responseObjArray.Any())
					{
						for (int i = 0; i < responseObjArray.Count; ++i)
						{
							JToken responseToken = responseObjArray[i];
							var errorToken = responseToken.SelectToken("error");
							if (errorToken != null)
							{
								int errorCode = errorToken.Value<int>("code");
								string errorMessage = errorToken.Value<string>("error");

								throw new ApplicationException($"Operation at index {i} returned error code {errorCode}: {errorMessage}");
							}
						}
					}

					// At this point, we know no errors.
					numProcessed += opsToSend.Count();
				} while (numProcessed < _batchableOperations.Count);

				_batchableOperations.Clear();
			}
		}

		internal static string GetBatchJson(IEnumerable<BatchableOperation> batchOps)
		{
			var batchOpsJsons = batchOps.Select(op => op.GetJson());
			string requestsValueJson = String.Join(",", batchOpsJsons);
			string batchJson = "{\"requests\": [" + requestsValueJson + "] }";
			return batchJson;
		}

		/// <summary>
		/// Gets all rows from the Parse "books" class/table
		/// </summary>
		internal IEnumerable<Book> GetBooks(string whereCondition = "")
		{
			var request = new RestRequest("classes/books", Method.GET);
			SetCommonHeaders(request);
			request.AddParameter("keys", "object_id,baseUrl,harvestState,harvestLog");
			
			if (!String.IsNullOrEmpty(whereCondition))
			{
				request.AddParameter("where", whereCondition);
			}

			IEnumerable<Book> results = GetAllResults<Book>(request);
			return results;
		}
				
		/// <summary>
		/// Lazily gets all the results from a Parse database in chunks
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="request">The request should not include count, limit, skip, or order fields. This method will populate those in order to provide the functionality</param>
		/// <returns>Yields the results through an IEnumerable as needed</returns>
		private IEnumerable<T> GetAllResults<T>(IRestRequest request)
		{
			int numProcessed = 0;
			int totalCount = 0;
			do
			{
				request.AddParameter("count", "1");
				request.AddParameter("limit", "1000");   // The limit should probably be on the higher side. The fewer DB calls, the better, probably.
				request.AddParameter("skip", numProcessed.ToString());
				request.AddParameter("order", "createdAt");

				Logger?.TrackEvent("ParseClient::GetAllResults Request Sent");
				var restResponse = this.Client.Execute(request);
				string responseJson = restResponse.Content;

				var response = JsonConvert.DeserializeObject<Parse.ParseResponse<T>>(responseJson);

				totalCount = response.Count;
				if (totalCount == 0)
				{
					Console.Out.WriteLine("Query returned no results.");
					break;
				}

				var currentResultCount = response.Results.Length;				
				if (currentResultCount <= 0)
				{
					break;
				}

				for (int i = 0; i < currentResultCount; ++i)
				{
					yield return response.Results[i];
				}

				numProcessed += currentResultCount;

				string message = $"GetAllResults Progress: {numProcessed} out of {totalCount}.";
				Console.Out.WriteLine(message);
				Logger?.LogVerbose(message);
			}
			while (numProcessed < totalCount);
		}
	}
}
