using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BloomHarvester.Logger;

namespace BloomHarvester
{
    // This class is responsible for coordinating the running of the application logic
    public class Harvester : IDisposable
    {
        protected IMonitorLogger logger;
        private ParseClient parseClient;

        public bool IsDebug { get; set; }

        public Harvester(bool isDebug)
        {
            this.IsDebug = isDebug;
            this.logger = new AzureMonitorLogger(isDebug);
            parseClient = new ParseClient(isDebug);
            parseClient.Logger = this.logger;
        }

        public void Dispose()
        {
            logger.Dispose();
        }

        public static void RunHarvestAll(HarvestAllOptions options)
        {
            using (Harvester harvester = new Harvester(options.IsDebug))
            {
                harvester.HarvestAll();
            }
        }

        // Public interface should use RunHarvestAll() function instead. (So that we can guarantee that the class instance is properly disposed).
        private void HarvestAll()
        {
            logger.TrackEvent("HarvestAll Start");
            IEnumerable<string> bookList = parseClient.GetBookUrlList();
            foreach (string bookBaseUrl in bookList)
            {
                ProcessOneBook(bookBaseUrl);
            }

            logger.TrackEvent("HarvestAll End - Success");
        }

        private void ProcessOneBook(string bookBaseUrl)
        {
            logger.TrackEvent("ProcessOneBook Start");
            string message = $"Processing: {bookBaseUrl}";
            Console.Out.WriteLine(message);
            logger.LogVerbose(message);

            logger.TrackEvent("ProcessOneBook End - Success");
        }
    }
}
