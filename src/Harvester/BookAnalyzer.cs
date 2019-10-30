﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Bloom;
using Bloom.Api;
using Bloom.Book;
using SIL.Xml;

namespace BloomHarvester
{
	/// <summary>
	/// Analyze a book and extract various information the harvester needs
	/// </summary>
	public class BookAnalyzer
	{
		private HtmlDom _dom;
		public BookAnalyzer(string html, string meta)
		{
			_dom = new HtmlDom(XmlHtmlConverter.GetXmlDomFromHtml(html, false));
			Language1Code = GetBestLangCode(1) ?? "";
			Language2Code = GetBestLangCode(2) ?? "en";
			Language3Code = GetBestLangCode(3) ?? "";

			var metaObj = DynamicJson.Parse(meta);
			if (metaObj.IsDefined("brandingProjectName"))
			{
				this.Branding = metaObj.brandingProjectName;
			}

			var bloomCollectionElement =
				new XElement("Collection",
					new XElement("Language1Iso639Code", new XText(Language1Code)),
					new XElement("Language2Iso639Code", new XText(Language2Code)),
					new XElement("Language3Iso639Code", new XText(Language3Code)),
					new XElement("Language1Name", new XText(GetLanguageDisplayNameOrEmpty(metaObj, Language1Code))),
					new XElement("Language2Name", new XText(GetLanguageDisplayNameOrEmpty(metaObj, Language2Code))),
					new XElement("Language3Name", new XText(GetLanguageDisplayNameOrEmpty(metaObj, Language3Code))),
					new XElement("BrandingProjectName", new XText(Branding ?? "")));
			var sb = new StringBuilder();
			using (var writer = XmlWriter.Create(sb))
				bloomCollectionElement.WriteTo(writer);
			BloomCollection = sb.ToString();
		}

		private string GetLanguageDisplayNameOrEmpty(dynamic metadata, string isoCode)
		{
			if (string.IsNullOrEmpty(isoCode))
				return "";

			if (metadata.IsDefined("language-display-names") && metadata["language-display-names"].IsDefined(isoCode))
				return metadata["language-display-names"][isoCode];

			return "";
		}

		/// <summary>
		/// Gets the language code for the specified language number
		/// </summary>
		/// <param name="x">The language number</param>
		/// <returns>The language code for the specified language, as determined from the bloomDataDiv. Returns null if not found.</returns>
		private string GetBestLangCode(int x)
		{
			string xpathString = $"//*[@id='bloomDataDiv']/*[@data-book='contentLanguage{x}']";
			var matchingNodes = _dom.SafeSelectNodes(xpathString);
			if (matchingNodes.Count == 0)
			{
				// contentLanguage2 and contentLanguage3 are only present in bilingual or trilingual books,
				// so we fall back to getting lang 2 and 3 from the html if needed.
				// We should never be missing contentLanguage1 (but having the fallback here is basically free).
				return GetLanguageCodeFromHtml(x);
			}
			var matchedNode = matchingNodes.Item(0);
			string langCode = matchedNode.InnerText.Trim();
			return langCode;
		}

		private string GetLanguageCodeFromHtml(int languageNumber)
		{
			string classToLookFor;
			switch (languageNumber)
			{
				case 1:
					classToLookFor = "bloom-content1";
					break;
				case 2:
					classToLookFor = "bloom-contentNational1";
					break;
				case 3:
					classToLookFor = "bloom-contentNational2";
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(languageNumber), "Must be 1, 2, or 3");
			}
			// We make the assumption that the bookTitle is always present and always has any relevant language
			string xpathString = $"//div[contains(@class, '{classToLookFor}') and @data-book='bookTitle' and @lang]";
			return _dom.SelectSingleNode(xpathString)?.Attributes["lang"]?.Value;
		}

		public static BookAnalyzer FromFolder(string bookFolder)
		{
			var filename = Path.GetFileName(bookFolder);
			var bookPath = Bloom.Book.BookStorage.FindBookHtmlInFolder(bookFolder);						 
			var metaPath = Path.Combine(bookFolder, "meta.json");
			return new BookAnalyzer(File.ReadAllText(bookPath, Encoding.UTF8),
				File.ReadAllText(metaPath, Encoding.UTF8));
		}

		public string WriteBloomCollection(string bookFolder)
		{
			var collectionFolder = Path.GetDirectoryName(bookFolder);
			var result = Path.Combine(collectionFolder, "temp.bloomCollection");
			File.WriteAllText(result, BloomCollection, Encoding.UTF8);
			return result;
		}

		public string Language1Code { get;}
		public string Language2Code { get; }
		public string Language3Code { get; set; }
		public string Branding { get; }

		/// <summary>
		/// The content appropriate to a skeleton BookCollection file for this book.
		/// </summary>
		public string BloomCollection { get; set; }

		/// <summary>
		/// For now, we assume that generated Bloom Reader books are always suitable.
		/// </summary>
		public bool IsBloomReaderSuitable()
		{
			return true;
		}

		/// <summary>
		/// Our simplistic check for ePUB suitability is that all of the content pages
		/// have 0 or 1 each of images, text boxes, and/or videos
		/// </summary>
		public bool IsEpubSuitable()
		{
			int goodPages = 0;
			foreach (var div in _dom.SafeSelectNodes("//div[contains(concat(' ', @class, ' '),' numberedPage ')]").Cast<XmlElement> ().ToList ())
			{
				var imageContainers = div.SafeSelectNodes("div[contains(@class,'marginBox')]//div[contains(@class,'bloom-imageContainer')]");
				if (imageContainers.Count > 1)
					return false;

				// Count any translation group which is not an image description
				var translationGroups = div.SafeSelectNodes("div[contains(@class,'marginBox')]//div[contains(@class,'bloom-translationGroup') and not(contains(@class,'bloom-imageDescription'))]");
				if (translationGroups.Count > 1)
					return false;

				var videos = div.SafeSelectNodes("following-sibling::div[contains(@class,'marginBox')]//video");
				if (videos.Count > 1)
					return false;
				++goodPages;
			}
			return goodPages > 0;
		}
	}
}
