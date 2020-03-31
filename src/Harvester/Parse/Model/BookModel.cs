using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BloomHarvester.Parse.Model
{
	/// <summary>
	/// This class represents a Book row in the Parse database
	/// as well as functions that deal with the interaction with Parse
	///
	/// However, most of the logic for processing a book and updating its value
	/// should go in Harvester.Book class instead, which will wrap this class.
	/// </summary>
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public class BookModel : WriteableParseObject
	{
		public const string kHarvestStateField = "harvestState";
		public const string kShowField = "show";

		protected override HashSet<string> GetWriteableMembers()
		{
			return new HashSet<string>(new string[]
			{
				"HarvestState",
				"HarvesterId",
				"HarvesterMajorVersion",
				"HarvesterMinorVersion",
				"HarvestStartedAt",
				"HarvestLogEntries",
				"Features",
				"Tags",
				"Show"
			});
		}

		// Below follow properties from the book table, the ones that we've needed to this point.
		// There are many more properties from the book table that we could add when they are needed.
		// When adding a new property, you probably also need to add it to the list of keys selected in ParseClient.cs

		#region Harvester-specific properties
		[JsonProperty(kHarvestStateField)]
		public string HarvestState { get; set; }

		[JsonProperty("harvesterId")]
		public string HarvesterId { get; set; }

		/// <summary>
		/// Represents the major version number of the last Harvester instance that attempted to process this book.
		/// If the major version changes, then we will redo processing even of books that succeeded.
		/// </summary>
		[JsonProperty("harvesterMajorVersion")]
		public int HarvesterMajorVersion { get; set; }

		/// <summary>
		/// Represents the minor version number of the last Harvester instance that attempted to process this book.
		/// If the minor version is updated, then we will redo processing of books that failed
		/// </summary>
		[JsonProperty("harvesterMinorVersion")]
		public int HarvesterMinorVersion { get; set; }

		/// <summary>
		/// The timestamp of the last time Harvester started processing this book
		/// </summary>
		[JsonProperty("harvestStartedAt")]
		public Parse.Model.ParseDate HarvestStartedAt { get; set; }

		[JsonProperty("harvestLog")]
		public List<string> HarvestLogEntries { get; set; }
		#endregion


		#region Non-harvester related properties of the book
		[JsonProperty("baseUrl")]
		public readonly string BaseUrl;

		[JsonProperty("title")]
		public readonly string Title;

		[JsonProperty("inCirculation")]
		public readonly bool? IsInCirculation;

		[JsonProperty("langPointers")]
		public readonly Language[] Languages;

		[JsonProperty("uploader")]
		public readonly User Uploader;

		[JsonProperty("features")]
		public string[] Features { get; set; }

		[JsonProperty("tags")]
		public string[] Tags { get; set; }

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
		[JsonProperty(kShowField)]
		public dynamic Show { get; set; }
		#endregion


		public override WriteableParseObject Clone()
		{
			// Dynamic object Show is not easily cloned,
			// so easier for us to do this by serializing/deserializing JSON instead
			// Also makes sure we clone arrays properly (although there are other ways we could get this too)
			string jsonOfThis = JsonConvert.SerializeObject(this);
			var newBook = JsonConvert.DeserializeObject<BookModel>(jsonOfThis);
			return newBook;
		}

		// Returns the class name (like a table name) of the class on the Parse server that this object corresponds to
		internal override string GetParseClassName()
        {
            return GetStaticParseClassName();
        }

		internal static string GetStaticParseClassName()
		{
			return "books";
		}

		#region Batch Parse Update code
		// (This region of code is not in normal use as of 3-30-2020)
		internal static UpdateOperation GetNewBookUpdateOperation()
		{
			var updateOp = new UpdateOperation();
			AddUpdateSource(updateOp);
			return updateOp;
		}

		private static void AddUpdateSource(UpdateOperation updateOp) => updateOp.UpdateFieldWithString("updateSource", "bloomHarvester");

		internal override UpdateOperation GetPendingUpdates()
		{
			// We need the updateSource to be set
			var pendingUpdates = base.GetPendingUpdates();
			if (pendingUpdates._updatedFieldValues.Any())
			{
				AddUpdateSource(pendingUpdates);
			}
			return pendingUpdates;
		}
		#endregion
	}
}
