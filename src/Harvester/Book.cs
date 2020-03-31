using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BloomHarvester.LogEntries;
using BloomHarvester.Logger;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;

namespace BloomHarvester
{
	/// <summary>
	/// This class is a wrapper around a book in the Parse datbase
	/// This is where you can put methods for manipulating a book
	/// </summary>
	internal class Book
	{
		public Book(BookModel bookModel, IMonitorLogger logger)
		{
			Model = bookModel;
			_logger = logger;
		}

		internal BookModel Model { get; set; }
		protected IMonitorLogger _logger;

		internal IBookAnalyzer Analyzer { get; set; }

		/// <summary>
		/// Determines whether any warnings regarding a book should be displayed to the user on Bloom Library
		/// </summary>
		/// <param name="book">The book to check</param>
		/// <returns></returns>
		internal List<BaseLogEntry> FindBookWarnings()
		{
			var warnings = new List<BaseLogEntry>();

			if (this.Model == null)
			{
				return warnings;
			}

			if (String.IsNullOrWhiteSpace(this.Model.BaseUrl))
			{
				warnings.Add(new MissingBaseUrlWarning());
			}

			if (warnings.Any())
			{
				this._logger.LogWarn("Warnings: " + String.Join(";", warnings.Select(x => x.ToString())));
			}

			return warnings;
		}

		/// <summary>
		/// Determines and sets the tags for the book
		/// e.g., computing the computedLevel of the book
		/// Keeps any existing tags if they are not directly determined by this function
		/// </summary>
		internal bool SetTags()
		{
			Debug.Assert(this.Analyzer != null, "Analyzer must be set before calling SetTags");

			var tagMap = this.GetTagDictionary();

			var computedLevelValue = this.Analyzer.GetBookComputedLevel();
			if (computedLevelValue <= 0)
				return false;
			ReplaceTagWithScalar(tagMap, "computedLevel", computedLevelValue.ToString());

			this.SetTagsFromTagDictionary(tagMap);

			return true;
		}

		/// <summary>
		/// Turns the tag array into a Dictionary
		/// This enables you to find which keys are present and how many times they occur
		/// </summary>
		private Dictionary<string, List<string>> GetTagDictionary()
		{
			var map = new Dictionary<string, List<string>>();

			if (this.Model.Tags == null)
				return map;

			foreach (var tag in this.Model.Tags)
			{
				string[] fields = tag.Split(':');
				string key = null;
				string value = null;
				if (fields.Length == 0)
				{
					continue;
				}
				else if (fields.Length == 1)
				{
					key = fields[0];
				}
				else if (fields.Length >= 2)
				{
					key = fields[0];
					value = fields[1];
				}

				if (!map.TryGetValue(key, out List<string> valueList))
				{
					valueList = new List<string>();
					map.Add(key, valueList);
				}

				if (value != null)
				{
					valueList.Add(value);
				}
			}

			return map;
		}

		/// <summary>
		/// Given a tag dictionary, accordingly sets the Tags field in the model.
		/// </summary>
		private void SetTagsFromTagDictionary(Dictionary<string, List<string>> tagDictionary)
		{
			// Note: Assumes that we don't need to preserve order when writing back into it
			if (tagDictionary == null)
			{
				this.Model.Tags = new string[0];
				return;
			}

			var newTags = new List<string>();
			foreach (var kvp in tagDictionary)
			{
				string tagKey = kvp.Key;
				List<string> tagValueList = kvp.Value;

				foreach (var tagValue in tagValueList)
				{
					string newTagStr = $"{tagKey}:{tagValue}";
					newTags.Add(newTagStr);
				}
			}

			this.Model.Tags = newTags.ToArray();
		}

		/// <summary>
		/// Given a tag (with only one entry for that key), udpates the dictionary with the new value of that tag
		/// (Or adds the tag if it doesn't exist in the dictionary yet)
		/// </summary>
		/// <param name="tagDictionary">The dictionary structure representing all the tag key/value pairs</param>
		/// <param name="tagKey">The key of the tag</param>
		/// <param name="tagValue">The new value</param>
		private void ReplaceTagWithScalar(Dictionary<string, List<string>> tagDictionary, string tagKey, string tagValue)
		{
			var newValueList = new List<string>(1);
			newValueList.Add(tagValue);

			tagDictionary[tagKey] = newValueList;
		}

		/// <summary>
		/// Set the harvester evaluation for the given artifact.
		/// Call this method only if harvester created and uploaded the given artifact.
		/// </summary>
		internal void SetHarvesterEvaluation(string artifact, bool enabled)
		{
			if (Model.Show == null)
			{
				var jsonString = $"{{ \"{artifact}\": {{ \"harvester\": {enabled.ToString().ToLowerInvariant()} }} }}";
				Model.Show = JsonConvert.DeserializeObject(jsonString);
				return;
			}
			var setting = JsonConvert.DeserializeObject($"{{ \"harvester\": {enabled.ToString().ToLowerInvariant()} }}");
			switch (artifact)
			{
				case "epub":
					if (Model.Show.epub == null)
						Model.Show.epub = setting;
					else
						Model.Show.epub.harvester = enabled;
					break;
				case "pdf":
					if (Model.Show.pdf == null)
						Model.Show.pdf = setting;
					else
						Model.Show.pdf.harvester = enabled;
					break;
				case "bloomReader":
					if (Model.Show.bloomReader == null)
						Model.Show.bloomReader = setting;
					else
						Model.Show.bloomReader.harvester = enabled;
					break;
				case "readOnline":
					if (Model.Show.readOnline == null)
						Model.Show.readOnline = setting;
					else
						Model.Show.readOnline.harvester = enabled;
					break;
			}
		}

		// Prints out some diagnostic info about the book (for debugging a failed book)
		// environment should be the environment of the BOOK not the Harvester. (i.e., it should probably be _parseDbEnvironment)
		internal string GetBookDiagnosticInfo(EnvironmentSetting environment)
		{
			string diagnosticInfo =
				$"BookId: {Model.ObjectId}\n" +
				$"URL: {this.GetDetailLink(environment) ?? "No URL"}\n" +
				$"Title: {Model.Title}";

			return diagnosticInfo;
		}

		// Returns the link to the book detail page on Bloom Library
		// If the book's ObjectId is null/etc, this method returns null as well.
		public string GetDetailLink(EnvironmentSetting environment)
		{
			if (String.IsNullOrWhiteSpace(Model.ObjectId))
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
			string anchorReference = $"https://{subdomain}bloomlibrary.org/browse/detail/{Model.ObjectId}";
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
			if ((Model.Features == null && metaData.Features != null)
				|| !Model.Features.SequenceEqual(metaData.Features))
			{
				Model.Features = metaData.Features;
			}
		}
	}
}
