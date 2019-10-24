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
		public const string kHarvesterMajorVersionField = "harvesterMajorVersion";
		public const string kHarvesterMinorVersionField = "harvesterMinorVersion";
		public const string kHarvestLogField = "harvestLog";
	    public const string kShow = "show";


		// There are many more properties from the book table that we could add when they are needed.
		// When adding a new property, you probably also need to add it to the list of keys selected in ParseClient.cs

		#region Harvester-specific properties
		[JsonProperty("harvestState")]
		public string HarvestState;

		/// <summary>
		/// Represents the major version number of the last Harvester instance that attempted to process this book.
		/// If the major version changes, then we will redo processing even of books that succeeded.
		/// </summary>
		[JsonProperty("harvesterMajorVersion")]
		public int HarvesterMajorVersion;

		/// <summary>
		/// Represents the minor version number of the last Harvester instance that attempted to process this book.
		/// If the minor version is updated, then we will redo processing of books that failed
		/// </summary>
		[JsonProperty("harvesterMinorVersion")]
		public int HarvesterMinorVersion;

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

		/// <summary>
	    /// A json object used to limit what the Library shows the user for each book.
	    /// For example:
	    /// "show": {
	    ///   "epub": {
	    ///     "harvester": true,
		///     "user": true,
		///     "librarian": true,
	    ///   },
		///   "pdf": {
		///     "harvester": true,
		///     "user": false,
		///   },
		///   "bloomReader": {
		///     "harvester": true,
		///     "librarian": false,
		///   },
		///   "readOnline": {
		///     "harvester": true,
		///   }
		/// }
	    /// </summary>
	    /// <remarks>
	    /// Only the harvester values should be changed by this code!
	    /// </remarks>
	    [JsonProperty("show")]
	    public dynamic Show { get; set; }

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

	    internal void SetHarvesterEvaluation(string name, bool enabled)
	    {
			if (Show == null)
			{
				var jsonString = $"{{ \"{name}\": {{ \"harvester\": {enabled.ToString().ToLowerInvariant()} }} }}";
				Show = JsonConvert.DeserializeObject(jsonString);
				return;
			}
		    var setting = $"{{ \"harvester\": {enabled.ToString().ToLowerInvariant()} }}";
			switch (name)
			{
				case "epub":
					if (Show.epub == null)
						Show.epub = JsonConvert.DeserializeObject(setting);
					else
						Show.epub.harvester = enabled;
					break;
				case "pdf":
					if (Show.pdf == null)
						Show.pdf = JsonConvert.DeserializeObject(setting);
					else
						Show.pdf.harvester = enabled;
					break;
				case "bloomReader":
					if (Show.bloomReader == null)
						Show.bloomReader = JsonConvert.DeserializeObject(setting);
					else
						Show.bloomReader.harvester = enabled;
					break;
				case "readOnline":
					if (Show.readOnline == null)
						Show.readOnline = JsonConvert.DeserializeObject(setting);
					else
						Show.readOnline.harvester = enabled;
					break;
			}

		}
	}
}
