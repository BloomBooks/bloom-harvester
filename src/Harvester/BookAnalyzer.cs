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
using BloomHarvester.LogEntries;
using CoenM.ImageHash.HashAlgorithms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SIL.Xml;
using Bloom.Collection;
using SIL.IO;
using Bloom.SafeXml;

namespace BloomHarvester
{
	internal interface IBookAnalyzer
	{
		string Language1Code { get; }

		string WriteBloomCollection(string bookFolder);

		bool IsBloomReaderSuitable(List<LogEntry> harvestLogEntries);
		bool IsEpubSuitable(List<LogEntry> harvestLogEntries);

		int GetBookComputedLevel();
		bool BookHasRestrictiveLicense { get; }

		List<string> GetBestPHashImageSources();
		ulong ComputeImageHash(string path);

		string GetBookshelf();
	}

	/// <summary>
	/// Analyze a book and extract various information the harvester needs
	/// </summary>
	class BookAnalyzer : CollectionSettingsReconstructor, IBookAnalyzer
	{
		private readonly Version _bloomVersion;
		private readonly Version _version5_5 = new Version(5, 5);   // collection settings are uploaded starting with 5.5
		private readonly Version _version5_4 = new Version(5, 4);	// epub publishing changed in 5.4
		private readonly Version _version5_3 = new Version(5, 3);	// publishing-settings.json introduced in 5.3
		private readonly dynamic _publishSettings;

		public BookAnalyzer(string html, string meta, string bookDirectory = "")
		: base(html, meta, bookDirectory)
		{
			var metaObj = DynamicJson.Parse(meta);
			if (metaObj.IsDefined("license"))
			{
				var license = metaObj["license"] as string;
				BookHasRestrictiveLicense = license == "custom" || license == "ask";
			}
			// Extract the Bloom version that created/uploaded the book.
			_bloomVersion = _dom.GetGeneratorVersion();
			//var generatorNode = _dom.RawDom.SelectSingleNode("//head/meta[@name='Generator']") as SafeXmlElement;
			_publishSettings = null;
			var settingsPath = Path.Combine(_bookDirectory, "publish-settings.json");
			var needSave = false;
			if (SIL.IO.RobustFile.Exists(settingsPath))
			{
				// Note that Harvester runs a recent (>= 5.4) version of Bloom which defaults to "fixed"
				// if epub.mode has not been set in the publish-settings.json file.  The behavior before
				// version 5.4 was what is now called "flowable" and we want to preserve that for uploaders
				// using the older versions of Bloom since that is what they'll see in ePUBs they create.
				try
				{
					var settingsRawText = SIL.IO.RobustFile.ReadAllText(settingsPath);
					_publishSettings = DynamicJson.Parse(settingsRawText, Encoding.UTF8) as DynamicJson;
					if (_bloomVersion < _version5_3)
					{
						// publish-settings.json was introduced in Bloom 5.3.  If the upload version
						// of Bloom is prior to that, then what we have is a stale copy of the settings
						// file from a source book used to make a derivative.  We really want the default
						// behavior that occurs when the settings file does not exist.  See BL-14127.
						_publishSettings = null;
					}
					else if (_bloomVersion < _version5_4)
					{
						if (!_publishSettings.IsDefined("epub"))
						{
							_publishSettings.epub = DynamicJson.Parse("{\"mode\":\"flowable\"}");
							needSave = true;
						}
						else if (!_publishSettings.epub.IsDefined("mode"))
						{
							_publishSettings.epub.mode = "flowable";    // traditional behavior
							needSave = true;
						}
						if (_publishSettings.epub.mode == "fixed")
						{
							// Debug.Assert doesn't allow a dynamic argument to be used.
							System.Diagnostics.Debug.Assert(false, "_publishSettings.epub.mode == \"fixed\" should not happen before Bloom 5.4!");
						}
					}
				}
				catch
				{
					// Ignore exceptions reading or parsing the publish-settings.json file.
					_publishSettings = null;
				}
			}
			if (_publishSettings == null && _bloomVersion < _version5_4)
			{
				_publishSettings = DynamicJson.Parse("{\"epub\":{\"mode\":\"flowable\"}}");
				needSave = true;
			}
			if (needSave)
			{
				// We've set the epub.mode to flowable, so we need to let Bloom know about it when we
				// create the artifacts.  (This is written to a temporary folder.)
				// (Don't use DynamicJson.Serialize() -- it doesn't work like you might think.)
				SIL.IO.RobustFile.WriteAllText(settingsPath, _publishSettings.ToString());
			}
		}

		/// <summary>
		/// This is called from the base (CollectionSettingsReconstructor) constructor.
		/// </summary>
		public override bool LoadFromUploadedSettings()
		{
			var uploadVersion = _dom.GetGeneratorVersion();
			if (uploadVersion < _version5_5)
				return false;	// even if it exists, the settings file is stale and useless

			var uploadedCollectionSettingsPath = Path.Combine(_bookDirectory, "collectionFiles", "book.uploadCollectionSettings");
			if (RobustFile.Exists(uploadedCollectionSettingsPath))
			{
				BloomCollection = RobustFile.ReadAllText(uploadedCollectionSettingsPath);
				// Set public properties from the uploaded Bloom Collection settings.
				var xdoc = new XmlDocument();
				xdoc.LoadXml(BloomCollection);
				Language1Code = xdoc.SelectSingleNode("/Collection/Language1Iso639Code")?.InnerText ?? "";
				Language2Code = xdoc.SelectSingleNode("/Collection/Language2Iso639Code")?.InnerText ?? "";
				Language3Code = xdoc.SelectSingleNode("/Collection/Language3Iso639Code")?.InnerText ?? "";
				SignLanguageCode = xdoc.SelectSingleNode("/Collection/SignLanguageIso639Code")?.InnerText ?? "";
				Country = xdoc.SelectSingleNode("/Collection/Country")?.InnerText ?? "";
				Province = xdoc.SelectSingleNode("/Collection/Province")?.InnerText ?? "";
				District = xdoc.SelectSingleNode("/Collection/District")?.InnerText ?? "";
				SubscriptionCode = GetSubscriptionCode(xdoc);
				_bookshelf = GetBookShelfFromTagsIfPossible(xdoc.SelectSingleNode("/Collection/DefaultBookTags")?.InnerText ?? "");
				return true;
			}
			else
			{
				return false;
			}
		}

		private string GetSubscriptionCode(XmlDocument xdoc)
		{
			// We expect to get SubscriptionCode starting with Bloom 6.1.
			// Older books will come with BrandingProjectName instead.
			var code = xdoc.SelectSingleNode("/Collection/SubscriptionCode")?.InnerText;
			if (string.IsNullOrEmpty(code))
				return xdoc.SelectSingleNode("/Collection/BrandingProjectName")?.InnerText ?? "";
			return code;
		}

		private string GetBookShelfFromTagsIfPossible(string defaultTagsString)
		{
			if (String.IsNullOrEmpty(defaultTagsString))
				return String.Empty;
			var defaultTags = defaultTagsString.Split(',');
			var defaultBookshelfTag = defaultTags.Where(t => t.StartsWith("bookshelf:")).FirstOrDefault();
			return defaultBookshelfTag ?? String.Empty;
		}

		public string GetBookshelf()
		{
			return _bookshelf;
		}

		public static BookAnalyzer FromFolder(string bookFolder)
		{
			var bookPath = BookStorage.FindBookHtmlInFolder(bookFolder);
			if (!File.Exists(bookPath))
				throw new Exception("Incomplete upload: missing book's HTML file");
			var metaPath = Path.Combine(bookFolder, "meta.json");
			if (!File.Exists(metaPath))
				throw new Exception("Incomplete upload: missing book's meta.json file");
			return new BookAnalyzer(File.ReadAllText(bookPath, Encoding.UTF8),
				File.ReadAllText(metaPath, Encoding.UTF8), bookFolder);
		}

		public string WriteBloomCollection(string bookFolder)
		{
			var collectionFolder = Path.GetDirectoryName(bookFolder);
			var result = Path.Combine(collectionFolder, "temp.bloomCollection");
			File.WriteAllText(result, BloomCollection, Encoding.UTF8);
			return result;
		}

		public bool BookHasRestrictiveLicense { get; private set; }

		/// <summary>
		/// For now, we assume that generated Bloom Reader books are always suitable.
		/// </summary>
		public bool IsBloomReaderSuitable(List<LogEntry> harvestLogEntries)
		{
			return true;
		}

		/// <summary>
		/// Our simplistic check for ePUB suitability is that all of the content pages
		/// have 0 or 1 each of images, text boxes, and/or videos
		/// </summary>
		public bool IsEpubSuitable(List<LogEntry> harvestLogEntries)
		{
			int goodPages = 0;
			var mode = "";
			try
			{
				mode = _publishSettings?.epub?.mode;
			}
			catch (Exception e)
			{
				mode = "flowable";
			}
			// Bloom 5.4 sets a default value of "fixed" unless the user changes it.
			// Previous versions of Bloom should not even have a value for this setting,
			// but we set it to "flowable" earlier to preserve old behavior.
			if (mode == "fixed" && _bloomVersion >= _version5_4)
				return true;
			foreach (var div in GetNumberedPages().ToList())
			{
				var imageContainers = div.SafeSelectNodes("div[contains(@class,'marginBox')]//div[contains(@class,'bloom-imageContainer')]");
				if (imageContainers.Length > 1)
				{
					harvestLogEntries.Add(new LogEntry(LogLevel.Info, LogType.ArtifactSuitability, "Bad ePUB because some page(s) had multiple images"));
					return false;
				}
				// Count any translation group which is not an image description
				var translationGroups = GetTranslationGroupsFromPage(div, includeImageDescriptions: false);
				if (translationGroups.Length > 1)
				{
					harvestLogEntries.Add(new LogEntry(LogLevel.Info, LogType.ArtifactSuitability, "Bad ePUB because some page(s) had multiple text boxes"));
					return false;
				}
				var videos = div.SafeSelectNodes("following-sibling::div[contains(@class,'marginBox')]//video");
				if (videos.Length > 1)
				{
					harvestLogEntries.Add(new LogEntry(LogLevel.Info, LogType.ArtifactSuitability, "Bad ePUB because some page(s) had multiple videos"));
					return false;
				}
				++goodPages;
			}
			if (goodPages == 0)
				harvestLogEntries.Add(new LogEntry(LogLevel.Info, LogType.ArtifactSuitability, "Bad ePUB because there were no content pages"));
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

				IEnumerable<SafeXmlElement> editables = GetEditablesFromPage(pageElement, Language1Code, includeImageDescriptions: false, includeTextOverPicture: true);
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
			if (String.IsNullOrWhiteSpace(text))
				return 0;
			// FYI, GetWordsFromHtmlString() (which is a port from our JS code) returns an array containing the empty string
			// if the input to it is the empty string. So handle that...

			var words = GetWordsFromHtmlString(text);
			return words.Where(x => !String.IsNullOrEmpty(x)).Count();
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

		private IEnumerable<SafeXmlElement> GetNumberedPages() => _dom.SafeSelectNodes("//div[contains(concat(' ', @class, ' '),' numberedPage ')]").Cast<SafeXmlElement>();

		/// <remarks>This xpath assumes it is rooted at the level of the marginBox's parent (the page).</remarks>
		private static string GetTranslationGroupsXpath(bool includeImageDescriptions)
		{
			string imageDescFilter = includeImageDescriptions ? "" : " and not(contains(@class,'bloom-imageDescription'))";
			// We no longer (or ever did?) use box-header-off for anything, but some older books have it.
			// For our purposes (and really all purposes throughout the system), we don't want them to include them.
			string xPath = $"div[contains(@class,'marginBox')]//div[contains(@class,'bloom-translationGroup') and not(contains(@class, 'box-header-off')){imageDescFilter}]";
			return xPath;
		}

		/// <summary>
		/// Gets the translation groups for the current page that are not within the image container
		/// </summary>
		/// <param name="pageElement">The page containing the bloom-editables</param>
		private static SafeXmlNode[] GetTranslationGroupsFromPage(SafeXmlElement pageElement, bool includeImageDescriptions)
		{
			return pageElement.SafeSelectNodes(GetTranslationGroupsXpath(includeImageDescriptions));
		}

		/// <summary>
		/// Gets the bloom-editables for the current page that match the language and are not within the image container
		/// </summary>
		/// <param name="pageElement">The page containing the bloom-editables</param>
		/// <param name="lang">Only bloom-editables matching this ISO language code will be returned</param>
		private static IEnumerable<SafeXmlElement> GetEditablesFromPage(SafeXmlElement pageElement, string lang, bool includeImageDescriptions = true, bool includeTextOverPicture = true)
		{
			string translationGroupXPath = GetTranslationGroupsXpath(includeImageDescriptions);
			string langFilter = HtmlDom.IsLanguageValid(lang) ? $"[@lang='{lang}']" : "";

			string xPath = $"{translationGroupXPath}//div[contains(@class,'bloom-editable')]{langFilter}";
			var editables = pageElement.SafeSelectNodes(xPath).Cast<SafeXmlElement>();

			foreach (var editable in editables)
			{
				bool isOk = true;
				if (!includeTextOverPicture)
				{
					var textOverPictureMatch = GetClosestMatch(editable, (e) =>
					{
						return e.HasClass("bloom-textOverPicture");
					});

					isOk = textOverPictureMatch == null;
				}

				if (isOk)
					yield return editable;
			}
		}

		internal delegate bool ElementMatcher(SafeXmlElement element);

		/// <summary>
		/// Find the closest ancestor (or self) that matches the condition
		/// </summary>
		/// <param name="startElement"></param>
		/// <param name="matcher">A function that returns true if the element matches</param>
		/// <returns></returns>
		internal static SafeXmlElement GetClosestMatch(SafeXmlElement startElement, ElementMatcher matcher)
		{
			SafeXmlElement currentElement = startElement;
			while (currentElement != null)
			{
				if (matcher(currentElement))
				{
					return currentElement;
				}

				currentElement = currentElement.ParentNode as SafeXmlElement;
			}

			return null;
		}

		/// <summary>
		/// Compute the perceptual hash of the given image file.  We need to handle black and white PNG
		/// files which carry the image data in only the alpha channel.  Other image files are trivial
		/// to handle by comparison with the CoenM.ImageSharp.ImageHash functions.
		/// </summary>
		/// <remarks>
		/// We are using the CoenM.ImageSharp.ImageHash library to compute the perceptual hash.  This isn't
		/// the fastest hashing algorithm, but it isn't too bad for "perceptual" hashing.  We are using a
		/// perceptual hash because we want to be able to detect when two images are similar enough that
		/// they are likely to actually be the same except for some minor changes like DPI scaling.
		/// We're now computing up to 5 hashes for each book, so the performance of this method could slow
		/// things down by a few seconds per book.  That is still acceptable given the benefits of approximate
		/// matching and given that download time and artifact creation are likely to still be the bottlenecks
		/// for most books.
		/// We may decide someday that a faster hash with fewer false positives but more false negatives is
		/// better, but then we'd need to re-run the hashing for all existing books.
		/// </remarks>
		public ulong ComputeImageHash(string path)
		{
			using (var image = (Image<Rgba32>)Image.Load(path))
			{
				SanitizeImage(image);
				// check whether we have R=G=B=0 (ie, black) for all pixels, presumably with A varying.
				var allBlack = true;
				for (int x = 0; allBlack && x < image.Width; ++x)
				{
					for (int y = 0; allBlack && y < image.Height; ++y)
					{
						var pixel = image[x, y];
						if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
							allBlack = false;
					}
				}
				if (allBlack)
				{
					for (int x = 0; x < image.Width; ++x)
					{
						for (int y = 0; y < image.Height; ++y)
						{
							// If the pixels all end up the same because A never changes, we're no
							// worse off because the hash result will still be all zero bits.
							var pixel = image[x, y];
							pixel.R = pixel.A;
							pixel.G = pixel.A;
							pixel.B = pixel.A;
							image[x, y] = pixel;
						}
					}
				}
				var hashAlgorithm = new PerceptualHash();
				return hashAlgorithm.Hash(image);
			}
		}

		private static void SanitizeImage(Image<Rgba32> image)
		{
			// Corrupt Exif Metadata Orientation values can crash the phash implementation.
			// See https://issues.bloomlibrary.org/youtrack/issue/BH-5984 and other issues.
			if (image.Metadata != null && image.Metadata.ExifProfile != null &&
				image.Metadata.ExifProfile.TryGetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, out var orientObj))
			{
				uint orient;
				// Simply casting orientObj.Value to (uint) throws an exception if the underlying object is actually a ushort.
				// See https://issues.bloomlibrary.org/youtrack/issue/BH-6025.
				switch (orientObj.DataType)
				{
					case SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifDataType.Byte:
						var orientByte = (byte)orientObj.Value;
						orient = orientByte;
						break;
					case SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifDataType.Long:
						orient = (uint)orientObj.Value;
						break;
					case SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifDataType.Short:
						var orientUShort = (ushort)orientObj.Value;
						orient = orientUShort;
						break;
					case SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifDataType.SignedLong:
						var orientInt = (int)orientObj.Value;
						orient = (uint)orientInt;
						break;
					case SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifDataType.SignedShort:
						var orientShort = (short)orientObj.Value;
						orient = (uint)orientShort;
						break;
					default:
						// No idea of how to handle the rest of the cases, and most unlikely to be used.
						return;
				}
				// An exception is thrown if the orientation value is greater than 65545 (0xFFFF).
				// But we may as well ensure a valid value while we're at at.
				if (orient == 0 || orient > 0x9)
				{
					// Valid values of Exif Orientation are 1-9 according to https://jdhao.github.io/2019/07/31/image_rotation_exif_info/.
					orient = Math.Max(orient, 1);
					orient = Math.Min(orient, 9);
					image.Metadata.ExifProfile.SetValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, orient);
				}
			}
		}

		// Note that newer books (produced by Bloom 6.2 and later) store primary img elements under a
		// div.bloom-canvas, but overlay images under a div.bloom-imageContainer.
		// Older books store all img elements under a div.bloom-imageContainer.
		const string contentPagePath =
			"//div[contains(@class,'bloom-page') and contains(@class,'numberedPage') and not(@data-activity) and not(@data-tool-id='game')]";
		const string bloomCanvasPath =
			"//div[contains(@class,'bloom-canvas')]";
		const string imageContainerPath =
			"//div[contains(@class,'bloom-imageContainer')]";
		const string frontCoverPath =
			"//div[contains(@class,'bloom-page') and @data-xmatter-page='frontCover']";

		/// <summary>
		/// Finds the images to use when computing the perceptual hash for the book.
		/// </summary>
		/// <remarks>
		/// Precondition: Assumes that pages were written to the HTML in the order of their page number
		/// </remarks>
		public List<string> GetBestPHashImageSources()
		{
			List<string> imagePaths = new List<string>();
			// Find the pictures on content pages other than games.  This may include overlay images as
			// well as background images.
			var allContentImages = _dom.SafeSelectNodes($"{contentPagePath}{bloomCanvasPath}/img|{contentPagePath}{imageContainerPath}/img");
			for (int i = 0; i < allContentImages.Length; ++i)
			{
				var src = allContentImages[i].GetAttribute("src");
				if (!String.IsNullOrEmpty(src) && src != "placeHolder.png")
					imagePaths.Add(src);
			}
			if (imagePaths.Count > 0)
				return imagePaths;

			var fallbackImgWrappers = _dom.SafeSelectNodes($"{contentPagePath}{bloomCanvasPath}");
			if (fallbackImgWrappers.Length == 0)
				fallbackImgWrappers = _dom.SafeSelectNodes($"{contentPagePath}{imageContainerPath}");
			for (int i = 0; i < fallbackImgWrappers.Length; ++i)
			{
				var fallbackUrl = GetImageElementUrl(fallbackImgWrappers[i] as SafeXmlElement)?.UrlEncoded;
				if (!String.IsNullOrEmpty(fallbackUrl) && fallbackUrl != "placeHolder.png")
					imagePaths.Add(fallbackUrl);
			}
			if (imagePaths.Count > 0)
				return imagePaths;

			// No content page images found.  Try the cover page
			var coverImages = _dom.SafeSelectNodes($"{frontCoverPath}{bloomCanvasPath}/img");
			if (coverImages.Length == 0)
				coverImages = _dom.SafeSelectNodes($"{frontCoverPath}{imageContainerPath}/img");
			for (int i = 0; i < coverImages.Length; ++i)
			{
				var src = coverImages[i].GetAttribute("src");
				if (!String.IsNullOrEmpty(src) && src != "placeHolder.png")
					imagePaths.Add(src);
			}
			if (imagePaths.Count > 0)
				return imagePaths;

			var coverFallbackImgWrappers = _dom.SafeSelectNodes($"{frontCoverPath}{bloomCanvasPath}");
			if (coverFallbackImgWrappers.Length == 0)
				coverFallbackImgWrappers = _dom.SafeSelectNodes($"{frontCoverPath}{imageContainerPath}");
			for (int i = 0; i < coverFallbackImgWrappers.Length; ++i)
			{
				var fallbackUrl = GetImageElementUrl(coverFallbackImgWrappers[i] as SafeXmlElement)?.UrlEncoded;
				if (!String.IsNullOrEmpty(fallbackUrl) && fallbackUrl != "placeHolder.png")
					imagePaths.Add(fallbackUrl);
			}
			// If nothing on the cover page either, give up.
			return imagePaths;
		}

		/// <summary>
		/// Gets the url for the image, either from an img element or any other element that has
		/// an inline style with background-image set.
		/// </summary>
		/// <remarks>
		/// This method is adapted (largely copied) from Bloom Desktop, so consider that if you
		/// need to modify this method.  The method is Bloom Desktop is not used because it would
		/// require adding a reference to Geckofx which is neither needed nor wanted here.
		/// </remarks>
		private UrlPathString GetImageElementUrl(SafeXmlElement imgOrDivWithBackgroundImage)
		{
			if (imgOrDivWithBackgroundImage.Name.ToLower() == "img")
			{
				var src = imgOrDivWithBackgroundImage.GetAttribute("src");
				return UrlPathString.CreateFromUrlEncodedString(src);
			}
			var styleRule = imgOrDivWithBackgroundImage.GetAttribute("style") ?? "";
			var regex = new Regex("background-image\\s*:\\s*url\\((.*)\\)", RegexOptions.IgnoreCase);
			var match = regex.Match(styleRule);
			if (match.Groups.Count == 2)
			{
				return UrlPathString.CreateFromUrlEncodedString(match.Groups[1].Value.Trim(new[] {'\'', '"'}));
			}
			return null;
		}
	}
}
