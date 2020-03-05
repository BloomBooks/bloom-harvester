using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BloomHarvester.Parse.Model
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public class Book : WriteableParseObject
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
			var newBook = JsonConvert.DeserializeObject<Book>(jsonOfThis);
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

		/// <summary>
		/// Set the harvester evaluation for the given artifact.
		/// Call this method only if harvester created and uploaded the given artifact.
		/// </summary>
	    internal void SetHarvesterEvaluation(string artifact, bool enabled)
	    {
			if (Show == null)
			{
				var jsonString = $"{{ \"{artifact}\": {{ \"harvester\": {enabled.ToString().ToLowerInvariant()} }} }}";
				Show = JsonConvert.DeserializeObject(jsonString);
				return;
			}
		    var setting = JsonConvert.DeserializeObject($"{{ \"harvester\": {enabled.ToString().ToLowerInvariant()} }}");
			switch (artifact)
			{
				case "epub":
					if (Show.epub == null)
						Show.epub = setting;
					else
						Show.epub.harvester = enabled;
					break;
				case "pdf":
					if (Show.pdf == null)
						Show.pdf = setting;
					else
						Show.pdf.harvester = enabled;
					break;
				case "bloomReader":
					if (Show.bloomReader == null)
						Show.bloomReader = setting;
					else
						Show.bloomReader.harvester = enabled;
					break;
				case "readOnline":
					if (Show.readOnline == null)
						Show.readOnline = setting;
					else
						Show.readOnline.harvester = enabled;
					break;
			}
		}

		// Prints out some diagnostic info about the book (for debugging a failed book)
		// environment should be the environment of the BOOK not the Harvester. (i.e., it should probably be _parseDbEnvironment)
		internal string GetBookDiagnosticInfo(EnvironmentSetting environment)
		{
			string diagnosticInfo =
				$"BookId: {this.ObjectId}\n" +
				$"URL: {this.GetDetailLink(environment) ?? "No URL"}\n" +
				$"Title: {this.Title}";

			return diagnosticInfo;
		}

		// Returns the link to the book detail page on Bloom Library
		// If the book's ObjectId is null/etc, this method returns null as well.
		public string GetDetailLink(EnvironmentSetting environment)
		{
			if (String.IsNullOrWhiteSpace(this.ObjectId))
			{
				return null;
			}

			string subdomain;
			switch (environment)
			{
				case EnvironmentSetting.Prod:
					subdomain = "";
					break;
				case EnvironmentSetting.Dev:
				default:
					subdomain = environment.ToString().ToLowerInvariant() + '.';
					break;
			}
			string anchorReference = $"https://{subdomain}bloomlibrary.org/browse/detail/{this.ObjectId}";
			return anchorReference;
		}

		/// <summary>
		/// Modifies the book with newer metadata
		/// </summary>
		/// <param name="bookFolderPath">The path to find the up-to-date version of the book (which contains the up-to-date meta.json)</param>
		internal void UpdateMetadataIfNeeded(string bookFolderPath)
		{
			var metaData = Bloom.Book.BookMetaData.FromFolder(bookFolderPath);
			UpdateMetadataIfNeeded(metaData);
		}

		/// <summary>
		/// Modifies the book with newer metadata
		/// </summary>
		/// <param name="metaData">The new metadata to update with</param>
		internal void UpdateMetadataIfNeeded(Bloom.Book.BookMetaData metaData)
		{
			if ((this.Features == null && metaData.Features != null)
				|| !this.Features.SequenceEqual(metaData.Features))
			{
				this.Features = metaData.Features;
			}
		}

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
	}
}
