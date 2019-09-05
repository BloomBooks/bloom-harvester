using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Parse.Model
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Book : ParseObject
    {
		// TODO: There are many more properties from the book table that we could add when they are needed.
		[JsonProperty("harvestState")]
		public string HarvestState;

		[JsonProperty("baseUrl")]
        public string BaseUrl;

        [JsonProperty("harvestLog")]
		public List<string> HarvestLogEntries;

		public const string kHarvestStateField = "harvestState";
        public const string kHarvesterIdField = "harvesterId";
        public const string kHarvestLogField = "harvestLog";

        // Returns the class name (like a table name) of the class on the Parse server that this object corresponds to
        internal override string GetParseClassName()
        {
            return "books";
        }
    }
}
