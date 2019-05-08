using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace BloomHarvester
{
    // Enhance: In the future, may be worth it to subclass the BloomDesktop one and take a dependency on it.
    // We could get the URLs and ApplicationID from the BlookDesktop base class. We would add our new Harvester-specific functions in our derived class.
    //
    // Right now, there's not enough common functionality to make it worthwhile. Maybe revisit this topic when the code becomes more stable.
    public class ParseClient
    {
        // Constructors
        public ParseClient(bool isDebug)
        {
            string url = (isDebug) ? "https://bloom-parse-server-develop.azurewebsites.net/parse/" : "https://bloom-parse-server.azurewebsites.net/parse/";
            client = new RestClient(url);
        }

        // Fields and properties
        private RestClient client;
        private string applicationId = Environment.GetEnvironmentVariable("BloomHarvesterParseAppId");

        // Careful! Very well might be null
        public Logger.IMonitorLogger Logger { get; set; }


        // Methods
        private void SetCommonHeaders(RestRequest request)
        {
            request.AddHeader("X-Parse-Application-Id", applicationId);
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
                request.AddParameter("limit", "200");   // TODO: Increase limit, IMO. The fewer DB calls, the better, probably.
                request.AddParameter("skip", numProcessed.ToString());
                request.AddParameter("order", "updatedAt");
                request.AddParameter("keys", "object_id,baseUrl");

                Logger?.TrackEvent("ParseClient::GetBookUrlList Request Sent");
                var response = this.client.Execute(request);

                var dy = JsonConvert.DeserializeObject<dynamic>(response.Content);
                totalCount = dy.count;

                if (dy.results.Count <= 0)
                {
                    break;
                }

                for (int i = 0; i < dy.results.Count; ++i)
                {
                    yield return dy.results[i].baseUrl;
                }

                numProcessed += dy.results.Count;

                string message = $"GetBookUrlList Progress: {numProcessed} out of {totalCount}.";
                Console.Out.WriteLine(message);
                Logger?.LogVerbose(message);
            }
            while (numProcessed < totalCount);

            Logger?.TrackEvent("ParseClient::GetBookUrlList End - Success");
        }
    }
}