using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Parse.Model
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class Book : ParseObject
    {
		public const string kHarvestStateField = "harvestState";
		public const string kHarvesterIdField = "harvesterId";
		public const string kHarvesterVersionField = "harvesterVersion";
		public const string kHarvestLogField = "harvestLog";


		// There are many more properties from the book table that we could add when they are needed.
		// When adding a new property, you probably also need to add it to the list of keys selected in ParseClient.cs

		#region Harvester-specific properties
		[JsonProperty("harvestState")]
		public string HarvestState;

		/// <summary>
		/// Represents the version of the last Harvester instance that attempted to process this book.
		/// The assembly version should be updated at least every time something changes in how books are processed. Or, once every check-in would work too.
		/// </summary>
		[JsonProperty("harvesterVersion")]
		public string HarvesterVersion;

		/// <summary>
		/// The timestamp of the last time Harvester started processing this book
		/// </summary>
		[JsonProperty("harvestStartedAt")]
		public Date HarvestStartedAt;

		[JsonProperty("harvestLog")]
		public List<string> HarvestLogEntries;
		#endregion


		#region Non-harvester related properties of the book
		[JsonProperty("baseUrl")]
		public string BaseUrl;

		#endregion


		// Returns the class name (like a table name) of the class on the Parse server that this object corresponds to
		internal override string GetParseClassName()
        {
            return GetStaticParseClassName();
        }

		internal static string GetStaticParseClassName()
		{
			return "books";
		}

	}
}
