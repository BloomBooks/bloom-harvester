using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
		string WriteBloomCollection(string bookFolder);

		bool IsBloomReaderSuitable();
		bool IsEpubSuitable();

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
				var translationGroups = GetTranslationGroupsFromPage(div, includeImageDescriptions: false);
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

				IEnumerable<XmlElement> editables = GetEditablesFromPage(pageElement, Language1Code, includeImageDescriptions: false, includeTextOverPicture: false);
				foreach (var editable in editables)
				{
					wordCountForThisPage += GetWordCount(editable.InnerText);
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

		/// <summary>
		/// Returns the number of words in a piece of text
		/// </summary>
		internal static int GetWordCount(string text)
		{
			var words = GetWordsFromHtmlString(text);
			return words.Length;
		}

		private static readonly Regex kHtmlLinebreakRegex = new Regex("/<br><\\/br>|<br>|<br \\/>|<br\\/>|\r?\n/", RegexOptions.Compiled);
		/// <summary>
		/// Splits a piece of HTML text
		/// </summary>
		/// <param name="textHTML">The text to split</param>
		/// <param name="letters">Optional - Characters which Unicode defines as punctuation but which should be counted as letters instead</param>
		/// <returns>An array where each element represents a word</returns>
		private static string[] GetWordsFromHtmlString(string textHTML, string letters = null)
		{
			// This function is a port of the Javascript version in BloomDesktop's synphony_lib.js's getWordsFromHtmlString() function

			// Enhance: I guess it'd be ideal if we knew what the text's culture setting was, but I don't know how we can get that
			textHTML = textHTML.ToLower();

			// replace html break with space
			string s = kHtmlLinebreakRegex.Replace(textHTML, " ");

			var punct = "\\p{P}";

			if (!String.IsNullOrEmpty(letters))
			{
				// BL-1216 Use negative look-ahead to keep letters from being counted as punctuation
				// even if Unicode says something is a punctuation character when the user
				// has specified it as a letter (like single quote).
				punct = "(?![" + letters + "])" + punct;
			}
			/**************************************************************************
			 * Replace punctuation in a sentence with a space.
			 *
			 * Preserves punctuation marks within a word (ex. hyphen, or an apostrophe
			 * in a contraction)
			 **************************************************************************/
			var regex = new Regex(
				"(^" +
				punct +
				"+)" + // punctuation at the beginning of a string
				"|(" +
				punct +
				"+[\\s\\p{Z}\\p{C}]+" +
				punct +
				"+)" + // punctuation within a sentence, between 2 words (word" "word)
				"|([\\s\\p{Z}\\p{C}]+" +
				punct +
				"+)" + // punctuation within a sentence, before a word
				"|(" +
				punct +
				"+[\\s\\p{Z}\\p{C}]+)" + // punctuation within a sentence, after a word
					"|(" +
					punct +
					"+$)" // punctuation at the end of a string
			);
			s = regex.Replace(s, " ");

			// Split into words using Separator and SOME Control characters
			// Originally the code had p{C} (all Control characters), but this was too all-encompassing.
			const string whitespace = "\\p{Z}";
			const string controlChars = "\\p{Cc}"; // "real" Control characters
											// The following constants are Control(format) [p{Cf}] characters that should split words.
											// e.g. ZERO WIDTH SPACE is a Control(format) charactor
											// (See http://issues.bloomlibrary.org/youtrack/issue/BL-3933),
											// but so are ZERO WIDTH JOINER and NON JOINER (See https://issues.bloomlibrary.org/youtrack/issue/BL-7081).
											// See list at: https://www.compart.com/en/unicode/category/Cf
			const string zeroWidthSplitters = "\u200b"; // ZERO WIDTH SPACE
			const string ltrrtl = "\u200e\u200f"; // LEFT-TO-RIGHT MARK / RIGHT-TO-LEFT MARK
			const string directional = "\u202A-\u202E"; // more LTR/RTL/directional markers
			const string isolates = "\u2066-\u2069"; // directional "isolate" markers
											  // split on whitespace, Control(control) and some Control(format) characters
			regex = new Regex(
				"[" +
					whitespace +
					controlChars +
					zeroWidthSplitters +
					ltrrtl +
					directional +
					isolates +
					"]+"
			);
			return regex.Split(s.Trim());
		}

		private IEnumerable<XmlElement> GetNumberedPages() => _dom.SafeSelectNodes("//div[contains(concat(' ', @class, ' '),' numberedPage ')]").Cast<XmlElement>();

		private static string GetTranslationGroupsXpath(bool includeImageDescriptions)
		{
			string imageDescFilter = includeImageDescriptions ? "" : " and not(contains(@class,'bloom-imageDescription'))";
			string xPath = $"div[contains(@class,'marginBox')]//div[contains(@class,'bloom-translationGroup'){imageDescFilter}]";
			return xPath;
		}

		/// <summary>
		/// Gets the translation groups for the current page that are not within the image container
		/// </summary>
		/// <param name="pageElement">The page containing the bloom-editables</param>
		private static XmlNodeList GetTranslationGroupsFromPage(XmlElement pageElement, bool includeImageDescriptions)
		{
			return pageElement.SafeSelectNodes(GetTranslationGroupsXpath(includeImageDescriptions));
		}

		/// <summary>
		/// Gets the bloom-editables for the current page that match the language and are not within the image container
		/// </summary>
		/// <param name="pageElement">The page containing the bloom-editables</param>
		/// <param name="lang">Only bloom-editables matching this ISO language code will be returned</param>
		private static IEnumerable<XmlElement> GetEditablesFromPage(XmlElement pageElement, string lang, bool includeImageDescriptions = true, bool includeTextOverPicture = true)
		{
			string translationGroup = GetTranslationGroupsXpath(includeImageDescriptions);
			string langFilter = Bloom.Book.HtmlDom.IsLanguageValid(lang) ? $"[@lang='{lang}']" : "";

			string xPath = $"{translationGroup}//div[contains(@class,'bloom-editable')]{langFilter}";
			var editables = pageElement.SafeSelectNodes(xPath).Cast<XmlElement>();

			foreach (var editable in editables)
			{
				bool isOk = true;
				if (!includeTextOverPicture)
				{
					var textOverPictureMatch = GetClosestMatch(editable, (e) =>
					{
						return HtmlDom.HasClass(e, "bloom-textOverPicture");
					});

					isOk = textOverPictureMatch == null;
				}

				if (isOk)
					yield return editable;
			}
		}

		internal delegate bool ElementMatcher(XmlElement element);

		/// <summary>
		/// Find the closest ancestor (or self) that matches the condition
		/// </summary>
		/// <param name="startElement"></param>
		/// <param name="matcher">A function that returns true if the element matches</param>
		/// <returns></returns>
		internal static XmlElement GetClosestMatch(XmlElement startElement, ElementMatcher matcher)
		{
			XmlElement currentElement = startElement;
			while (currentElement != null)
			{
				if (matcher(currentElement))
				{
					return currentElement;
				}

				currentElement = currentElement.ParentNode as XmlElement;
			}

			return null;
		}
	}
}
