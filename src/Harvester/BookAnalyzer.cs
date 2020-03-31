using System;
using System.Collections.Generic;
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
	internal interface IBookAnalyzer
	{
		int GetBookComputedLevel();
	}

	/// <summary>
	/// Analyze a book and extract various information the harvester needs
	/// </summary>
	class BookAnalyzer : IBookAnalyzer
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

			string pageNumberStyle = null;
			if (metaObj.IsDefined("page-number-style"))
			{
				pageNumberStyle = metaObj["page-number-style"];
			}

			bool isRtl = false;
			if (metaObj.IsDefined("isRtl"))
			{
				isRtl = metaObj["isRtl"];
			}

			var bloomCollectionElement =
				new XElement("Collection",
					new XElement("Language1Iso639Code", new XText(Language1Code)),
					new XElement("Language2Iso639Code", new XText(Language2Code)),
					new XElement("Language3Iso639Code", new XText(Language3Code)),
					new XElement("Language1Name", new XText(GetLanguageDisplayNameOrEmpty(metaObj, Language1Code))),
					new XElement("Language2Name", new XText(GetLanguageDisplayNameOrEmpty(metaObj, Language2Code))),
					new XElement("Language3Name", new XText(GetLanguageDisplayNameOrEmpty(metaObj, Language3Code))),
					new XElement("BrandingProjectName", new XText(Branding ?? "")),
					new XElement("PageNumberStyle", new XText(pageNumberStyle ?? "")),
					new XElement("IsLanguage1Rtl"), new XText(isRtl.ToString().ToLowerInvariant())
					);
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
			foreach (var div in GetNumberedPages().ToList())
			{
				var imageContainers = div.SafeSelectNodes("div[contains(@class,'marginBox')]//div[contains(@class,'bloom-imageContainer')]");
				if (imageContainers.Count > 1)
					return false;

				// Count any translation group which is not an image description
				var translationGroups = GetNonImageTranslationGroupsFromPage(div);
				if (translationGroups.Count > 1)
					return false;

				var videos = div.SafeSelectNodes("following-sibling::div[contains(@class,'marginBox')]//video");
				if (videos.Count > 1)
					return false;
				++goodPages;
			}
			return goodPages > 0;
		}

		/// <summary>
		/// Computes an estimate of the level of the book
		/// </summary>
		/// <returns>An int representing the level of the book.
		/// 1: "First words", 2: "First sentences", 3: "First paragraphs", 4: "Longer paragraphs"
		/// -1: Error
		/// </returns>
		public int GetBookComputedLevel()
		{
			var numberedPages = GetNumberedPages();

			int pageCount = 0;
			int maxWordsPerPage = 0;
			foreach (var pageElement in numberedPages)
			{
				++pageCount;
				int wordCountForThisPage = 0;

				XmlNodeList editables = GetNonImageEditablesFromPage(pageElement, Language1Code);
				foreach (XmlNode editable in editables)
				{
					string text = editable.InnerText;
					string[] words = text.Split(new char[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
					int wordCount = words.Length;
					wordCountForThisPage += wordCount;
				}

				maxWordsPerPage = Math.Max(maxWordsPerPage, wordCountForThisPage);
			}

			// This algorithm is to maintain consistentcy with African Storybook Project word count definitions
			// (Note: There are also guidelines about sentence count and paragraph count, which we could && in to here in the future).
			if (maxWordsPerPage <= 10)
				return 1;
			else if (maxWordsPerPage <= 25)
				return 2;
			else if (maxWordsPerPage <= 50)
				return 3;
			else
				return 4;
		}

		private IEnumerable<XmlElement> GetNumberedPages() => _dom.SafeSelectNodes("//div[contains(concat(' ', @class, ' '),' numberedPage ')]").Cast<XmlElement>();

		private string GetNonImageTranslationGroupsXpath() => "div[contains(@class,'marginBox')]//div[contains(@class,'bloom-translationGroup') and not(contains(@class,'bloom-imageDescription'))]";

		/// <summary>
		/// Gets the translation groups for the current page that are not within the image container
		/// </summary>
		/// <param name="pageElement">The page containing the bloom-editables</param>
		private XmlNodeList GetNonImageTranslationGroupsFromPage(XmlElement pageElement)
		{
			var translationGroups = pageElement.SafeSelectNodes(GetNonImageTranslationGroupsXpath());
			return translationGroups;
		}

		/// <summary>
		/// Gets the bloom-editables for the current page that match the language and are not within the image container
		/// </summary>
		/// <param name="pageElement">The page containing the bloom-editables</param>
		/// <param name="lang">Only bloom-editables matching this ISO language code will be returned</param>
		private XmlNodeList GetNonImageEditablesFromPage(XmlElement pageElement, string lang)
		{
			string translationGroup = GetNonImageTranslationGroupsXpath();
			string langFilter = Bloom.Book.HtmlDom.IsLanguageValid(lang) ? $"[@lang='{lang}']" : "";

			string xPath = $"{translationGroup}//div[contains(@class,'bloom-editable')]{langFilter}";
			var translationGroups = pageElement.SafeSelectNodes(xPath);
			return translationGroups;
		}
	}
}
