using BloomHarvester.LogEntries;
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

		/// <summary>
		/// The normal constructor
		/// </summary>
		public BookModel()
		{
			// As of March 2024, we have set the defaultValue to true in the _Schema table for books.inCirculation
			IsInCirculation = true;
		}

		/// <summary>
		/// A constructor that allows setting readonly fields
		/// </summary>
		internal BookModel(string baseUrl, string title, bool isInCirculation = true, ParseDate lastUploaded = null)
		{
			this.BaseUrl = baseUrl;
			this.Title = title;
			this.IsInCirculation = isInCirculation;
			this.LastUploaded = lastUploaded;

			// Enhance: add more readonly fields as needed
		}

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
				"Show",
				"SubscriptionDescriptor",
				"PHashOfFirstContentImage",
				"BloomPUBVersion"
			});
		}

		// Below follow properties from the book table, the ones that we've needed to this point.
		// There are many more properties from the book table that we could add when they are needed.
		// When adding a new property, it is added to the list of keys selected in ParseClient.cs
		// by the method GetParseKeys() below.

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
		public ParseDate HarvestStartedAt { get; set; }

		[JsonProperty("harvestLog")]
		public List<string> HarvestLogEntries { get; set; }
		#endregion


		#region Non-harvester related properties of the book
		[JsonProperty("baseUrl")]
		public readonly string BaseUrl;

		[JsonProperty("features")]
		public string[] Features { get; set; }

		[JsonProperty("inCirculation")]
		public readonly bool IsInCirculation;

		[JsonProperty("langPointers")]
		public readonly Language[] Languages;

		[JsonProperty("lastUploaded")]
		public readonly ParseDate LastUploaded;

		[JsonProperty("phashOfFirstContentImage")]	// Should be phash, not pHash
		public string PHashOfFirstContentImage { get; set; }

		[JsonProperty("bookHashFromImages")]
		public string BookHashFromImages { get; set; }


		[JsonProperty("brandingProjectName")] // Can't change to subscriptionDescriptor. It must match the field in the parse database.
		public string SubscriptionDescriptor { get; set; }

		[JsonProperty("draft")]
		public readonly bool IsDraft;

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
		
		[JsonProperty("tags")]
		public string[] Tags { get; set; }

		[JsonProperty("title")]
		public readonly string Title;

		[JsonProperty("uploader")]
		public readonly User Uploader;

		[JsonProperty("bloomPUBVersion")]
		public int? BloomPUBVersion { get; set; }
		#endregion

		public static List<string> GetParseKeys()
		{
			var type = typeof(BookModel);

			var serializedMembers = 
				// First collect all the fields and properties
				type.GetFields().Cast<MemberInfo>()
				.Concat(type.GetProperties().Cast<MemberInfo>())
				// Only include the ones which are serialized to JSON
				.Where(member => member.CustomAttributes.Any(attr => attr.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute"));

			var parseKeyNames = serializedMembers.Select(GetMemberJsonName).ToList();

			return parseKeyNames;
		}

		private static string GetMemberJsonName(MemberInfo memberInfo)
		{
			// Precondition: memberInfo must have an attribute of type JsonPropertyAttribute
			return
				// First find the JsonPropertyAttribute
				memberInfo.CustomAttributes.Where(attr => attr.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute")
				.Select(attr =>
					// Prefer to use the name assigned in JsonPropertyAttribute first, if available
					(attr.ConstructorArguments.FirstOrDefault().Value as string)
					// If not, fallback to the member's C# name
					?? memberInfo.Name)
				.First();
		}

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
		internal IEnumerable<LogEntry> GetValidLogEntries()
		{
			if (this.HarvestLogEntries == null)
				return Enumerable.Empty<LogEntry>();

			return this.HarvestLogEntries.Select(str => LogEntry.Parse(str)).Where(x => x != null);
		}

		internal IEnumerable<string> GetMissingFonts()
		{
			var previouslyMissingFontNames = this.GetValidLogEntries().Where(x => x.Type == LogType.MissingFont).Select(x => x.Message);
			return previouslyMissingFontNames;
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
		/// Update the Tags array if necessary for the bookshelfTag value, preserving at most one tag that
		/// starts with "bookshelf:".  Remove any such tag if the bookshelfTag value is empty or null.
		/// </summary>
		internal void UpdateBookshelfTag(string bookshelfTag)
		{
			System.Diagnostics.Debug.Assert(String.IsNullOrEmpty(bookshelfTag) || bookshelfTag.StartsWith("bookshelf:"));
			int countOfCurrentBookshelves = 0;
			string currentBookshelf = String.Empty;
			if (Tags == null || Tags.Length == 0)
			{
				if (!String.IsNullOrEmpty(bookshelfTag))
					Tags = new string[] { bookshelfTag };
			}
			else
			{
				foreach (var tag in Tags)
				{
					if (tag.StartsWith("bookshelf:"))
					{
						++countOfCurrentBookshelves;
						currentBookshelf = tag;
					}
				}
				if (countOfCurrentBookshelves == 0 && String.IsNullOrEmpty(bookshelfTag))
					return;
				if (countOfCurrentBookshelves == 1 && bookshelfTag == currentBookshelf)
					return;
				var newTags = new List<string>();
				foreach (var tag in Tags)
				{
					if (!tag.StartsWith("bookshelf:"))
						newTags.Add(tag);
				}
				// Note that we need to be able to delete bookshelves as well as add or change them.
				if (!String.IsNullOrEmpty(bookshelfTag))
					newTags.Add(bookshelfTag);
				Tags = newTags.ToArray();
			}
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
