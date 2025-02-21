using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BloomHarvester.IO;
using BloomHarvester.LogEntries;
using BloomHarvester.Logger;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BloomHarvester
{
	/// <summary>
	/// This class is a wrapper around a book in the Parse database
	/// This is where you can put methods for manipulating a book
	/// </summary>
	internal class Book
	{
		public Book(BookModel bookModel, IMonitorLogger logger, IFileIO fileIO)
		{
			Model = bookModel;
			_logger = logger;
			_fileIO = fileIO;
		}

		internal BookModel Model { get; set; }
		internal int BloomPubVersion;

		protected IMonitorLogger _logger;
		protected readonly IFileIO _fileIO;

		internal IBookAnalyzer Analyzer { get; set; }

		/// <summary>
		/// Determines whether any warnings regarding a book should be displayed to the user on Bloom Library
		/// </summary>
		/// <param name="book">The book to check</param>
		/// <returns></returns>
		internal List<LogEntry> FindBookWarnings()
		{
			var warnings = new List<LogEntry>();

			if (this.Model == null)
			{
				return warnings;
			}

			if (String.IsNullOrWhiteSpace(this.Model.BaseUrl))
			{
				warnings.Add(new LogEntry(LogLevel.Warn, LogType.MissingBaseUrl, ""));
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
		/// <returns>True if successful, false otherwise</returns>
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
				if (fields.Length < 2)
				{
					Debug.Assert(false, $"Could not parse tag: \"{tag}\", objectId:{this.Model.ObjectId}");
					continue;
				}
				else
				{
					key = fields[0];
					value = fields[1].Trim();
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

				if (!tagValueList.Any())
				{
					newTags.Add(tagKey);
				}
				else
				{
					foreach (var tagValue in tagValueList)
					{
						string newTagStr = $"{tagKey}:{tagValue}";
						newTags.Add(newTagStr);
					}
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
				case "bloomSource":
					if (Model.Show.bloomSource == null)
						Model.Show.bloomSource = setting;
					else
						Model.Show.bloomSource.harvester = enabled;
					break;
				case "jsonTexts":
					if (Model.Show.jsonTexts == null)
						Model.Show.jsonTexts = setting;
					else
						Model.Show.jsonTexts.harvester = enabled;
					break;
				case "readOnline":
					if (Model.Show.readOnline == null)
						Model.Show.readOnline = setting;
					else
						Model.Show.readOnline.harvester = enabled;
					break;
				case "social":
					if (Model.Show.social == null)
						Model.Show.social = setting;
					else
						Model.Show.social.harvester = enabled;
					break;
				case "shellbook":
					if (Model.Show.shellbook == null)
						Model.Show.shellbook = setting;
					else
						Model.Show.shellbook.harvester = enabled;
					break;
				default:
					throw new ArgumentException($"SetHarvesterEvaluation(): Unrecognized artifact type \"{artifact}\"");
			}
		}

		public void SetValueForShowTypeKey(string type, string key, string value)
		{
			if (Model != null)
			{
				if (!string.IsNullOrEmpty(value))
				{
					if (Model.Show == null)
						Model.Show = JsonConvert.DeserializeObject<JToken>($"{{ \"{type}\": {{ \"{key}\": \"{value}\" }} }}");
					else if (Model.Show[type] == null)
						Model.Show[type] = JsonConvert.DeserializeObject<JToken>($"{{ \"{key}\": \"{value}\" }}");
					else
						Model.Show[type][key] = value;
				}
				else if (Model?.Show?[type]?[key] != null)
					Model.Show[type][key].Parent.Remove();
			}
		}

		public void SetValueForShowTypeKey(string type, string key, bool value)
		{
			if (Model != null)
			{
				var valueStr = value ? "true" : "false";
				if (Model.Show == null)
					Model.Show = JsonConvert.DeserializeObject<JToken>($"{{ \"{type}\": {{ \"{key}\": {valueStr} }} }}");
				else if (Model.Show[type] == null)
					Model.Show[type] = JsonConvert.DeserializeObject<JToken>($"{{ \"{key}\": {valueStr} }}");
				else
					Model.Show[type][key] = value;
			}
		}

		internal void UpdatePerceptualHash(string infoPath)
		{
			if (!_fileIO.Exists(infoPath))
				return;

			string pHashText = _fileIO.ReadAllText(infoPath).Trim();

			// The BloomCLI passes these back by writing to a file, so it can't return the null value easily... writes a literal string "null" instead.
			// convert that back to a proper null value (not a string)
			// Also deal with pHashes that are all 0's... this can happen and indicates something funny happened in the pHash calculation.
			// Doesn't make sense to assume two images that both return 0x000... are actually the same.
			// So change them into null, because null == null evaluates to false.
			if (pHashText == "null" || pHashText == "0x00000000000000000000000000000000000000000000000000000000000000000000000000000000")
				pHashText = null;

			this.Model.PHashOfFirstContentImage = pHashText;
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
			if (metaData == null)
				// Don't bother trying to change anything if the new metadata object is null
				return;

			if ((Model.Features == null && metaData.Features != null)
				|| !Model.Features.SequenceEqual(metaData.Features))
			{
				Model.Features = metaData.Features;
			}

			if (metaData.BrandingProjectName != Model.BrandingProjectName)
			{
				Model.BrandingProjectName = metaData.BrandingProjectName;
			}

			if (BloomPubVersion > 0)
				Model.BloomPUBVersion = BloomPubVersion;
		}
	}
}
