using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using BloomHarvester;
using BloomHarvester.LogEntries;
using BloomTemp;
using NUnit.Framework;

namespace BloomHarvesterTests
{
	[TestFixture]
	public class BookAnalyzerTests
	{
		private BookAnalyzer _trilingualAnalyzer;
		private BookAnalyzer _bilingualAnalyzer;
		private BookAnalyzer _monolingualBookInBilingualCollectionAnalyzer;
		private BookAnalyzer _monolingualBookInTrilingualCollectionAnalyzer;
		private BookAnalyzer _bilingualBookInTrilingualCollectionAnalyzer;
		private BookAnalyzer _emptySubscriptionAnalyzer;
		private BookAnalyzer _silleadSubscriptionAnalyzer;
		private XElement _twoLanguageCollection;
		private XElement _threeLanguageCollection;
		private XElement _threeLanguageCollectionUsingTwo;
		private XElement _silleadCollection;
		private BookAnalyzer _epubCheckAnalyzer;
		private BookAnalyzer _epubCheckAnalyzer2;

		private string GetBookHtml(string generatorVersion = null)

		{
			var generatorMeta = "";
			if (generatorVersion != null)
			{
				generatorMeta = $@"<meta name='Generator' content='Bloom Version {generatorVersion} (apparent build date: 02-Apr-2025)'></meta>";
			}
			return @"
<html>
	<head>
		" + generatorMeta + @"
	</head>

	<body>
		<div id='bloomDataDiv'>
			{0}
		</div>

	    <div class='bloom-page cover coverColor bloom-frontMatter frontCover outsideFrontCover side-right A5Portrait' data-page='required singleton' data-export='front-matter-cover' data-xmatter-page='frontCover' id='923f450f-87dc-4fe8-829a-bf9cfe98ac6f' data-page-number=''>
			<div class='pageLabel' lang='en' data-i18n='TemplateBooks.PageLabel.Front Cover'>
				Front Cover
			</div>
			<div class='pageDescription' lang='en'></div>

            <div class='bloom-translationGroup bookTitle' data-default-languages='V,N1'>
				<div class='bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow' lang='z' contenteditable='true' data-book='bookTitle'></div>

				<div class='bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow bloom-content1 bloom-visibility-code-on' lang='xk' contenteditable='true' data-book='bookTitle'>
					<p>My Title</p>
				</div>

				<div class='bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow bloom-content2 bloom-contentNational1 bloom-visibility-code-on' lang='fr' contenteditable='true' data-book='bookTitle'>
					<p>My Title in the National Language</p>
				</div>
				<div class='bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow {1}' lang='de' contenteditable='true' data-book='bookTitle'></div>
			</div>
	    </div>
	</body>
</html>";
		}

		private string kHtml2 = @"
<html>
	<head></head>
	<body>
		<div id='bloomDataDiv'>
			<div data-book=""contentLanguage1"" lang=""*"">cmo-KH</div>
			<div data-book=""contentLanguage2"" lang=""*"">km</div>
		</div>
		<div class=""bloom-page cover coverColor bloom-frontMatter frontCover outsideFrontCover side-right Device16x9Portrait"" data-page=""required singleton"" data-export=""front-matter-cover"" data-xmatter-page=""frontCover"" id=""43b20336-73be-4d49-8dac-e15ac7f74f10"" lang=""en"" data-page-number="""">
			<div class=""pageLabel"" lang=""en"" data-i18n=""TemplateBooks.PageLabel.Front Cover"">
				Front Cover
			</div>
			<div class=""pageDescription"" lang=""en""></div>
			<div class=""marginBox"">
				<div data-book=""cover-branding-top-html"" lang=""*""></div>
				<div class=""bloom-translationGroup bookTitle"" data-default-languages=""V,N1"">
					<label class=""bubble"">Book title in {lang}</label>
					<div class=""bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow"" lang=""z"" contenteditable=""true"" data-book=""bookTitle""></div>
					<div class=""bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow bloom-content1 bloom-visibility-code-on"" lang=""cmo-KH"" contenteditable=""true"" data-book=""bookTitle"" style=""padding-bottom: 0px;"">
						<p>០៥ ងក៝ច​នាវ វាក្រាក់</p>
					</div>
					<div class=""bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow bloom-contentNational1 bloom-visibility-code-on"" lang=""en"" contenteditable=""true"" data-book=""bookTitle"" style=""padding-bottom: 0px;"">
						<p>The story of Uncle Krak</p>
					</div>
				</div>
			</div>
		</div>
		<div class=""bloom-page numberedPage customPage bloom-combinedPage side-right Device16x9Portrait bloom-bilingual"" data-page="""" id=""57d6a153-9a1b-4ce7-bd89-e4ef26a58336"" data-pagelineage=""adcd48df-e9ab-4a07-afd4-6a24d0398382"" data-page-number=""1"" lang="""">
			<div class=""pageLabel"" data-i18n=""TemplateBooks.PageLabel.Basic Text"" lang=""en"">
				Basic Text
			</div>
			<div class=""pageDescription"" lang=""en""></div>
			<div class=""marginBox"">
				<div class=""bloom-translationGroup bloom-trailingElement bloom-vertical-align-center"" data-default-languages=""auto"" data-hasqtip=""true"">
					<div class=""bloom-editable BigText-style bloom-content1 bloom-visibility-code-on"" style=""min-height: 43.2px;"" tabindex=""0"" spellcheck=""true"" role=""textbox"" aria-label=""false"" data-languagetipcontent=""Bunong"" lang=""cmo-KH"" contenteditable=""true"">
						<p>ពាង់​រាញា​វាក្រាក់។</p>
					</div>
					<div class=""bloom-editable normal-style bloom-content2 bloom-contentNational2 bloom-visibility-code-on"" style=""min-height: 33.6px;"" tabindex=""0"" spellcheck=""true"" role=""textbox"" aria-label=""false"" data-languagetipcontent=""Khmer"" lang=""km"" contenteditable=""true"">
						<p>គាត់ឈ្មោះ​អ៊ុំក្រាក់។​</p>
					</div>
				</div>
			</div>
		</div>
	</body>
</html>";

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			// Insert the appropriate contentLanguageX/bloom-content*X information for the different types of books
			_trilingualAnalyzer = new BookAnalyzer(GetTriLingualHtml(), GetMetaData());

			_bilingualAnalyzer = new BookAnalyzer(GetBiLingualHtml(), GetMetaData());

			var monoLingualHtml = GetMonoLingualHtml();
			_emptySubscriptionAnalyzer = new BookAnalyzer(monoLingualHtml, GetMetaData(@"""brandingProjectName"":"""","));
			_silleadSubscriptionAnalyzer = new BookAnalyzer(monoLingualHtml, GetMetaData(@"""brandingProjectName"":""SIL-LEAD"","));
			_monolingualBookInTrilingualCollectionAnalyzer = new BookAnalyzer(monoLingualHtml, GetMetaData());

			var monoLingualBookInBilingualCollectionHtml = String.Format(GetBookHtml(), kContentLanguage1Xml, "").Replace("bloom-content2 ", "");
			_monolingualBookInBilingualCollectionAnalyzer = new BookAnalyzer(monoLingualBookInBilingualCollectionHtml, GetMetaData());

			_bilingualBookInTrilingualCollectionAnalyzer = new BookAnalyzer(kHtml2, GetMetaData2());

			_twoLanguageCollection = XElement.Parse(_monolingualBookInBilingualCollectionAnalyzer.BloomCollection);
			_threeLanguageCollection = XElement.Parse(_monolingualBookInTrilingualCollectionAnalyzer.BloomCollection);
			_threeLanguageCollectionUsingTwo = XElement.Parse(_bilingualBookInTrilingualCollectionAnalyzer.BloomCollection);
			_silleadCollection = XElement.Parse(_silleadSubscriptionAnalyzer.BloomCollection);

			_epubCheckAnalyzer = new BookAnalyzer(kHtmlUnmodifiedPages, GetMetaData());
			_epubCheckAnalyzer2 = new BookAnalyzer(kHtmlModifiedPage, GetMetaData());
		}

		private const string kContentClassForLanguage3 = "bloom-contentNational2";
		private const string kContentLanguage1Xml = "<div data-book='contentLanguage1' lang='*'>xk</div>";
		private const string kContentLanguage2Xml = "<div data-book='contentLanguage2' lang='*'>fr</div>";
		private const string kContentLanguage3Xml = "<div data-book='contentLanguage3' lang='*'>de</div>";
		private string GetTriLingualHtml()
		{
			return String.Format(GetBookHtml(), kContentLanguage1Xml + kContentLanguage2Xml + kContentLanguage3Xml, kContentClassForLanguage3);
		}

		private string GetBiLingualHtml()
		{
			return String.Format(GetBookHtml(), kContentLanguage1Xml + kContentLanguage2Xml, kContentClassForLanguage3);
		}

		private string GetMonoLingualHtml(string generatorVersion = null)
		{
			return String.Format(GetBookHtml(generatorVersion), kContentLanguage1Xml, kContentClassForLanguage3).Replace("bloom-content2 ", "");
		}

		private string GetMetaData(string brandingJson = "")
		{
			const string meta = @"{""a11y_NoEssentialInfoByColor"":false,""a11y_NoTextIncludedInAnyImages"":false,""epub_HowToPublishImageDescriptions"":0,""epub_RemoveFontStyles"":false,""bookInstanceId"":""11c2c600-35af-488b-a8d6-3479edcb9217"",""suitableForMakingShells"":false,""suitableForMakingTemplates"":false,""suitableForVernacularLibrary"":true,""bloomdVersion"":0,""experimental"":false,{0}""folio"":false,""isRtl"":false,""title"":""Aeneas"",""allTitles"":""{\""en\"":\""Aeneas\"",\""es\"":\""Spanish title\""}"",""baseUrl"":null,""bookOrder"":null,""isbn"":"""",""bookLineage"":""056B6F11-4A6C-4942-B2BC-8861E62B03B3"",""downloadSource"":""ken@example.com/11c2c600-35af-488b-a8d6-3479edcb9217"",""license"":""cc-by"",""formatVersion"":""2.0"",""licenseNotes"":""Please be nice to John"",""copyright"":""Copyright © 2018, JohnT"",""credits"":"""",""tags"":[],""pageCount"":10,""languages"":[],""langPointers"":[{""__type"":""Pointer"",""className"":""language"",""objectId"":""2cy807OQoe""},{""__type"":""Pointer"",""className"":""language"",""objectId"":""VUiYTJhOyJ""},{""__type"":""Pointer"",""className"":""language"",""objectId"":""jwP3nu7XGY""}],""summary"":null,""allowUploadingToBloomLibrary"":true,""bookletMakingIsAppropriate"":true,""LeveledReaderTool"":null,""LeveledReaderLevel"":0,""country"":"""",""province"":"""",""district"":"""",""xmatterName"":null,""uploader"":{""__type"":""Pointer"",""className"":""_User"",""objectId"":""TWGrqk7NaR""},""tools"":[],""currentTool"":""talkingBookTool"",""toolboxIsOpen"":true,""author"":null,""subjects"":null,""hazards"":null,""a11yFeatures"":null,""a11yLevel"":null,""a11yCertifier"":null,""readingLevelDescription"":null,""typicalAgeRange"":null,""features"":[""blind"",""signLanguage"", ""signLanguage:aen""],""language-display-names"":{""xk"":""Vernacular"",""fr"":""French"",""pt"":""Portuguese"",""de"":""German"",""aen"":""Custom SL Name""}}";
			// can't use string.format here, because the metadata has braces as part of the json.
			return meta.Replace("{0}", brandingJson);
		}
		private string GetMetaData2(string brandingJson = "")
		{
			const string meta = @"{""a11y_NoEssentialInfoByColor"":false,""a11y_NoTextIncludedInAnyImages"":false,""epub_HowToPublishImageDescriptions"":0,""epub_RemoveFontStyles"":false,
	""bookInstanceId"":""11c2c600-35af-488b-a8d6-3479edcb9217"",""suitableForMakingShells"":false,""suitableForMakingTemplates"":false,""suitableForVernacularLibrary"":true,
	""bloomdVersion"":0,""experimental"":false,{0}""folio"":false,""isRtl"":false,""title"":""Aeneas"",
	""allTitles"":""{\""en\"":\""Aeneas\"",\""es\"":\""Spanish title\""}"",""baseUrl"":null,""bookOrder"":null,""isbn"":"""",""bookLineage"":""056B6F11-4A6C-4942-B2BC-8861E62B03B3"",
	""downloadSource"":""ken@example.com/11c2c600-35af-488b-a8d6-3479edcb9217"",""license"":""cc-by"",""formatVersion"":""2.0"",""licenseNotes"":""Please be nice to John"",
	""copyright"":""Copyright © 2018, JohnT"",""credits"":"""",""tags"":[],""pageCount"":10,
	""languages"":[],""langPointers"":[{""__type"":""Pointer"",""className"":""language"",""objectId"":""2cy807OQoe""},{""__type"":""Pointer"",""className"":""language"",""objectId"":
	""VUiYTJhOyJ""},{""__type"":""Pointer"",""className"":""language"",""objectId"":""jwP3nu7XGY""}],
	""summary"":null,""allowUploadingToBloomLibrary"":true,""bookletMakingIsAppropriate"":true,""LeveledReaderTool"":null,""LeveledReaderLevel"":0,
	""country"":"""",""province"":"""",""district"":"""",""xmatterName"":null,""uploader"":{""__type"":""Pointer"",""className"":""_User"",""objectId"":""TWGrqk7NaR""},""tools"":[],
	""currentTool"":""talkingBookTool"",""toolboxIsOpen"":true,""author"":null,""subjects"":null,""hazards"":null,""a11yFeatures"":null,""a11yLevel"":null,""a11yCertifier"":null,
	""readingLevelDescription"":null,""typicalAgeRange"":null,""features"":[],
	""language-display-names"":{""cmo-KH"":""Bunong"",""km"":""Khmer"",""en"":""English"",""de"":""German"",}}";
			// can't use string.format here, because the metadata has braces as part of the json.
			return meta.Replace("{0}", brandingJson);
		}

		[Test]
		public void Language1Code_InTrilingualBook()
		{
			Assert.That(_trilingualAnalyzer.Language1Code, Is.EqualTo("xk"));
		}

		[Test]
		public void Language1Code_InBilingualBook()
		{
			Assert.That(_bilingualAnalyzer.Language1Code, Is.EqualTo("xk"));
			Assert.That(_bilingualBookInTrilingualCollectionAnalyzer.Language1Code, Is.EqualTo("cmo-KH"));
		}
		[Test]
		public void Language1Code_InMonolingualBook()
		{
			Assert.That(_monolingualBookInBilingualCollectionAnalyzer.Language1Code, Is.EqualTo("xk"));
			Assert.That(_monolingualBookInTrilingualCollectionAnalyzer.Language1Code, Is.EqualTo("xk"));
		}

		[Test]
		public void Language2Code_InTrilingualBook()
		{
			Assert.That(_trilingualAnalyzer.Language2Code, Is.EqualTo("fr"));
		}

		[Test]
		public void Language2Code_InBilingualBook()
		{
			Assert.That(_bilingualAnalyzer.Language2Code, Is.EqualTo("fr"));
			Assert.That(_bilingualBookInTrilingualCollectionAnalyzer.Language2Code, Is.EqualTo("en"));
		}

		[Test]
		public void Language2Code_InMonolingualBook()
		{
			Assert.That(_monolingualBookInBilingualCollectionAnalyzer.Language2Code, Is.EqualTo("fr"));
			Assert.That(_monolingualBookInTrilingualCollectionAnalyzer.Language2Code, Is.EqualTo("fr"));
		}

		[Test]
		public void Language3Code_InTrilingualBook()
		{
			Assert.That(_trilingualAnalyzer.Language3Code, Is.EqualTo("de"));
		}

		[Test]
		public void Language3Code_InBilingualBook()
		{
			Assert.That(_bilingualAnalyzer.Language3Code, Is.EqualTo("de"));
			Assert.That(_bilingualBookInTrilingualCollectionAnalyzer.Language3Code, Is.EqualTo("km"));
		}

		[Test]
		public void Language3Code_InMonolingualBook()
		{
			Assert.That(_monolingualBookInBilingualCollectionAnalyzer.Language3Code, Is.Empty);
			Assert.That(_monolingualBookInTrilingualCollectionAnalyzer.Language3Code, Is.EqualTo("de"));
		}

		[Test]
		public void SignLanguageCode_InMonolingualBook()
		{
			Assert.That(_monolingualBookInTrilingualCollectionAnalyzer.SignLanguageCode, Is.EqualTo("aen"));
		}

		[Test]
		public void SignLanguageCode_InBilingualBook()
		{
			Assert.That(_bilingualAnalyzer.SignLanguageCode, Is.EqualTo("aen"));
			Assert.That(_bilingualBookInTrilingualCollectionAnalyzer.SignLanguageCode, Is.EqualTo(""));
		}

		[TestCase("Kyrgyzstan2020-XMatter.css", "Kyrgyzstan2020")]
		[TestCase("Device-XMatter.css", "Device")]
		public void GetBestXMatter_DirectoryHasXMatterCSS_PopulatesCollectionXMatterSetting(string filename, string expectedXMatterName)
		{
			using (var tempFolder = new TemporaryFolder("BookAnalyzerTests"))
			{
				var filePath = Path.Combine(tempFolder.FolderPath, filename);
				using (File.Create(filePath))	// TemporaryFolder takes a long time to dispose if you don't dispose this first.
				{
					// System under test
					var analyzer = new BookAnalyzer(GetMonoLingualHtml(), GetMetaData(), tempFolder.FolderPath);

					// Verification
					string expected = $"<XMatterPack>{expectedXMatterName}</XMatterPack>";
					Assert.That(analyzer.BloomCollection.Contains(expected), Is.True, "BloomCollection did not contain expected XMatter Pack. XML: " + analyzer.BloomCollection);
				}
			}
		}

		[TestCase]
		public void GetBestXMatter_DirectoryMissingXMatterCSS_PopulatesWithDefault()
		{
			using (var tempFolder = new TemporaryFolder("BookAnalyzerTests"))
			{
				// System under test
				var analyzer = new BookAnalyzer(GetMonoLingualHtml(), GetMetaData(), tempFolder.FolderPath);

				// Verification
				string expectedDefault = $"<XMatterPack>Device</XMatterPack>";
				Assert.That(analyzer.BloomCollection.Contains(expectedDefault), Is.True, "BloomCollection did not contain default XMatter Pack. XML: " + analyzer.BloomCollection);
			}
		}

		[Test]
		public void SubscriptionCode_Specified()
		{
			Assert.That(_silleadSubscriptionAnalyzer.SubscriptionCode, Is.EqualTo("SIL-LEAD-***-***"));
		}

		[Test]
		public void SubscriptionCode_Empty_IsNull()
		{
			Assert.That(_emptySubscriptionAnalyzer.SubscriptionCode, Is.Null);
		}

		[Test]
		public void SubscriptionCode_Missing_IsNull()
		{
			Assert.That(_monolingualBookInBilingualCollectionAnalyzer.SubscriptionCode, Is.Null);
		}

		[Test]
		public void BookCollection_HasLanguage1Code()
		{
			Assert.That(_twoLanguageCollection.Element("Language1Iso639Code")?.Value, Is.EqualTo("xk"));
			Assert.That(_threeLanguageCollection.Element("Language1Iso639Code")?.Value, Is.EqualTo("xk"));
			Assert.That(_threeLanguageCollectionUsingTwo.Element("Language1Iso639Code")?.Value, Is.EqualTo("cmo-KH"));
		}

		[Test]
		public void BookCollection_HasLanguage2Code()
		{
			Assert.That(_twoLanguageCollection.Element("Language2Iso639Code")?.Value, Is.EqualTo("fr"));
			Assert.That(_threeLanguageCollection.Element("Language2Iso639Code")?.Value, Is.EqualTo("fr"));
			Assert.That(_threeLanguageCollectionUsingTwo.Element("Language2Iso639Code")?.Value, Is.EqualTo("en"));
		}

		[Test]
		public void BookCollection_HasLanguage3Code()
		{
			Assert.That(_threeLanguageCollection.Element("Language3Iso639Code")?.Value, Is.EqualTo("de"));
			Assert.That(_threeLanguageCollectionUsingTwo.Element("Language3Iso639Code")?.Value, Is.EqualTo("km"));
		}


		[Test]
		public void BookCollection_HasSignLanguageCode()
		{
			Assert.That(_threeLanguageCollection.Element("SignLanguageIso639Code")?.Value, Is.EqualTo("aen"));
		}

		[Test]
		public void BookCollection_HasSignLanguageName()
		{
			Assert.That(_threeLanguageCollection.Element("SignLanguageName")?.Value, Is.EqualTo("Custom SL Name"));
		}

		[Test]
		public void BookCollection_HasSubscriptionCode()
		{
			Assert.That(_silleadCollection.Element("SubscriptionCode")?.Value, Is.EqualTo("SIL-LEAD-***-***"));
		}

		[Test]
		public void BookCollection_HasCorrectLanguageNames()
		{
			Assert.That(_threeLanguageCollection.Element("Language1Name")?.Value, Is.EqualTo("Vernacular"));
			Assert.That(_threeLanguageCollection.Element("Language2Name")?.Value, Is.EqualTo("French"));
			Assert.That(_threeLanguageCollection.Element("Language3Name")?.Value, Is.EqualTo("German"));
			Assert.That(_threeLanguageCollectionUsingTwo.Element("Language1Name")?.Value, Is.EqualTo("Bunong"));
			Assert.That(_threeLanguageCollectionUsingTwo.Element("Language2Name")?.Value, Is.EqualTo("English"));
			Assert.That(_threeLanguageCollectionUsingTwo.Element("Language3Name")?.Value, Is.EqualTo("Khmer"));
		}

		[Test]
		public void IsEpubSuitable_Works()
		{
			var list = new List<LogEntry>();
			Assert.That(_epubCheckAnalyzer.IsEpubSuitable(list), Is.True, "Unmodified Basic Text & Picture pages should be suitable for Epub");
			Assert.That(list.Count, Is.EqualTo(0), "Suitable epub should not add to the harvester log");
			Assert.That(_epubCheckAnalyzer2.IsEpubSuitable(list), Is.False, "Modified Basic Text & Picture page should not be suitable for Epub");
			Assert.That(list.Count, Is.EqualTo(1), "Unsuitable epub should add one entry to the harvester log");
		}

		private string GetHtmlForGetBookLevelTests(string page1Text, string page1TextOverPictureHtml)
		{
			string html =
$@"<html>
  <head><meta charset='UTF-8' /></head>
  <body>
	<div id='bloomDataDiv'>
	  <div data-book='contentLanguage1' lang='*'>en</div>
	</div>
    <div class='bloom-page cover coverColor bloom-frontMatter frontCover outsideFrontCover side-right A5Portrait' data-page='required singleton' data-export='front-matter-cover' data-xmatter-page='frontCover' id='89a32796-8cf9-4b0f-a694-43a3d705f620' data-page-number=''>
      <div class='pageLabel' lang='en' data-i18n='TemplateBooks.PageLabel.Front Cover'>Front Cover</div>
		<div class='marginBox'>
          <div class='bottomBlock'>
			<div class='bottomTextContent'>
			  <div class='creditsRow' data-hint='You may use this space for author/illustrator, or anything else.'>
                <div class='bloom-translationGroup' data-default-languages='V'>
                  <div class='bloom-editable smallCoverCredits Cover-Default-style' lang='en' contenteditable='true' data-book='smallCoverCredits'>
					This is a bunch of really long text that is way too many words for a Level 1 book, but since it's not on a numbered page, shouldn't matter.
				  </div>
                </div>
              </div>
            </div>
		  </div>
		</div>
	</div>
    <div class='bloom-page numberedPage customPage bloom-combinedPage side-right A5Portrait bloom-monolingual' data-page='' id='002627bd-4853-487d-986a-88ea67e0f31c' data-pagelineage='adcd48df-e9ab-4a07-afd4-6a24d0398382' data-page-number='1' lang=''>
      <div class='pageLabel' data-i18n='TemplateBooks.PageLabel.Basic Text &amp; Picture' lang='en'>Basic Text &amp; Picture</div>
      <div class='marginBox'>
        <div style='min-height: 42px;' class='split-pane horizontal-percent'>
          <div class='split-pane-component position-top' style='bottom: 50%'>
            <div class='split-pane-component-inner'>
              <div title='placeHolder.png 6.58 KB 341 x 335 81 DPI (should be 300-600) Bit Depth: 32' class='bloom-imageContainer bloom-leadingElement'>
                <img src='placeHolder.png' alt='place holder'></img>
                <div class='bloom-translationGroup bloom-imageDescription bloom-trailingElement'>
				  <div class='bloom-editable' lang='en'>This is a bunch of really really long text that would bump it above Level 1, but it's in an imageDescription, which is also ignored.
				  </div>
				</div>
				{page1TextOverPictureHtml}
              </div>
            </div>
          </div>
          <div class='split-pane-divider horizontal-divider' style='bottom: 50%' />
          <div class='split-pane-component position-bottom' style='height: 50%'>
            <div class='split-pane-component-inner'>
              <div class='bloom-translationGroup bloom-trailingElement' data-default-languages='auto'>
				<div class='bloom-editable' lang='en'><p>{page1Text}</p></div>
				<div class='bloom-editable' lang='es'><p>Este texto va a ser ignored porque no es en la lingua prima del libro. Uno dos tres cuatro cinco seis siete ocho nueve diaz.</p></div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </body>
</html>
";

			return html;
		}

		/// <summary>
		/// We have a bunch of tests for the computedLevel
		/// This function drives the test from its most direct input, the html
		/// We have a bunch of other public functions which generate the html for various test scenarios that can call this
		/// to run the core test for them
		/// </summary>
		/// <param name="html"></param>
		/// <param name="expectedLevel"></param>
		private void TestBookHtmlReturnsExpectedLevel(string html, int expectedLevel)
		{
			// Final Setup
			var analyzer = new BookAnalyzer(html, GetMetaData());

			// System under test
			var computedLevel = analyzer.GetBookComputedLevel();

			// Verification
			Assert.That(computedLevel, Is.EqualTo(expectedLevel));
		}

		[TestCase("One.Two.Three.Four.Five.Six.Seven.Eight.Nine.Ten.Eleven", 1, Description = "Punctuation test")]
		public void GetBookComputedLevel_BasicBook_ReturnsExpectedLevel(string input, int expectedLevel)
		{
			string html = GetHtmlForGetBookLevelTests(input, "");
			TestBookHtmlReturnsExpectedLevel(html, expectedLevel);
		}

		[TestCase(0, 1)]
		[TestCase(1, 1)]
		[TestCase(2, 1)]
		[TestCase(10, 1)]
		[TestCase(11, 2)]
		[TestCase(25, 2)]
		[TestCase(26, 3)]
		[TestCase(50, 3)]
		[TestCase(51, 4)]
		public void GetBookComputedLevel_BasicBookWithNWords_ReturnsExpectedLevel(int numWords, int expectedLevel)
		{
			string input = MakeStringOfXwords(numWords);
			GetBookComputedLevel_BasicBook_ReturnsExpectedLevel(input, expectedLevel);
		}

		private static string MakeStringOfXwords(int numWords)
		{
			var list = new List<string>();
			for (int i = 0; i < numWords; ++i)
			{
				list.Add((i).ToString());
			}
			return String.Join(" ", list);
		}

		// Make sure that comic books aren't returning level 1 all the time, which they used to, at one point
		[TestCase(0, 1)]
		[TestCase(1, 1)]
		[TestCase(2, 1)]
		[TestCase(10, 1)]
		[TestCase(11, 2)]
		[TestCase(25, 2)]
		[TestCase(26, 3)]
		[TestCase(50, 3)]
		[TestCase(51, 4)]
		public void GetBookComputedLevel_ComicBooksWithNWords_ReturnsExpectedLevel(int numWords, int expectedLevel)
		{
			string input = MakeStringOfXwords(numWords);
			string textOverPictureHtml =
				@"<svg class='comical-generated' viewBox='0,0,405,334' height='334' width='405' xmlns:xlink='http://www.w3.org/1999/xlink' xmlns='http://www.w3.org/2000/svg' version='1.1'><g style='mix-blend-mode: normal' text-anchor='start' font-size='12' font-weight='normal' font-family='sans-serif' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='1' stroke='none' fill-rule='nonzero' fill='none'></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='#000000' fill-rule='nonzero' fill='none'><path id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1' d='M197,79.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path d='M247.66671,124.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='none' fill-rule='nonzero' fill='#ffffff'><path stroke='#ffffff' stroke-opacity='0' id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1 1' d='M197,79.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path stroke='#ffffff' stroke-opacity='0.01' d='M247.66671,124.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='#000000' fill-rule='nonzero' fill='none'><path id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1' d='M164,207.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path d='M214.66671,252.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='none' fill-rule='nonzero' fill='#ffffff'><path stroke='#ffffff' stroke-opacity='0' id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1 1' d='M164,207.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path stroke='#ffffff' stroke-opacity='0.01' d='M214.66671,252.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='start' font-size='12' font-weight='normal' font-family='sans-serif' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='1' stroke='none' fill-rule='nonzero' fill='none'></g></svg>
                <div data-bubble='{`version`:`1.0`,`style`:`speech`,`tails`:[{`tipX`:337,`tipY`:139,`midpointX`:296,`midpointY`:133,`autoCurve`:true}],`level`:1}' style='left: 31.1477%; top: 26.6435%; width: 34.4672%; height: 8.95077%;' class='bloom-textOverPicture ui-resizable ui-draggable'>
                    <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V'>
                        <div data-languagetipcontent='English' aria-label='false' role='textbox' spellcheck='true' tabindex='0' class='bloom-editable Bubble-style bloom-content1 bloom-contentNational1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                            <p>" + input + @"</p>
                        </div>
                    </div>
                </div>";

			string html = GetHtmlForGetBookLevelTests("", textOverPictureHtml);

			TestBookHtmlReturnsExpectedLevel(html, expectedLevel);
		}

		// Multiple bubbles - all the words on the page should be summed up
		[TestCase(0, 0, 1)]
		// Adds to 1
		[TestCase(1, 0, 1)]
		[TestCase(0, 1, 1)]
		// Adds to 2
		[TestCase(2, 0, 1)]
		[TestCase(0, 2, 1)]
		[TestCase(1, 1, 1)]
		// Adds to 10 (L1 Max)
		[TestCase(10, 0, 1)]
		[TestCase(0, 10, 1)]
		[TestCase(5, 5, 1)]
		// Adds to 11 (L2 Min)
		[TestCase(11, 0, 2)]
		[TestCase(0, 11, 2)]
		[TestCase(6, 5, 2)]
		// Adds to 25 (L2 Max)
		[TestCase(25, 0, 2)]
		[TestCase(0, 25, 2)]
		[TestCase(13, 12, 2)]
		// Adds to 26 (L3 Min)
		[TestCase(26, 0, 3)]
		[TestCase(0, 26, 3)]
		[TestCase(13, 13, 3)]
		// Adds to 50 (L3 Max)
		[TestCase(50, 0, 3)]
		[TestCase(0, 50, 3)]
		[TestCase(25, 25, 3)]
		// Adds to 51 (L4 Min)
		[TestCase(51, 0, 4)]
		[TestCase(0, 51, 4)]
		[TestCase(26, 25, 4)]
		public void GetBookComputedLevel_ComicBooksWithTwoBubbles_ReturnsExpectedLevel(int numWords1, int numWords2, int expectedLevel)
		{
			string bubble1Text = MakeStringOfXwords(numWords1);
			string bubble2Text = MakeStringOfXwords(numWords2);

			string textOverPictureHtml =
				@"<div data-bubble='{`version`:`1.0`,`style`:`speech`,`tails`:[{`tipX`:304,`tipY`:267,`midpointX`:263,`midpointY`:261,`autoCurve`:true}],`level`:2}' style='left: 23.0233%; top: 64.8334%; width: 34.4672%; height: 8.95077%;' class='bloom-textOverPicture ui-resizable ui-draggable'>
                    <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V'>
                        <div aria-label='false' role='textbox' spellcheck='true' tabindex='0' data-languagetipcontent='English' class='bloom-editable Bubble-style bloom-content1 bloom-contentNational1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                            <p>" + bubble1Text + @"</p>
                        </div>
                    </div>
                </div><svg class='comical-generated' viewBox='0,0,405,334' height='334' width='405' xmlns:xlink='http://www.w3.org/1999/xlink' xmlns='http://www.w3.org/2000/svg' version='1.1'><g style='mix-blend-mode: normal' text-anchor='start' font-size='12' font-weight='normal' font-family='sans-serif' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='1' stroke='none' fill-rule='nonzero' fill='none'></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='#000000' fill-rule='nonzero' fill='none'><path id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1' d='M197,79.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path d='M247.66671,124.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='none' fill-rule='nonzero' fill='#ffffff'><path stroke='#ffffff' stroke-opacity='0' id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1 1' d='M197,79.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path stroke='#ffffff' stroke-opacity='0.01' d='M247.66671,124.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='#000000' fill-rule='nonzero' fill='none'><path id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1' d='M164,207.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path d='M214.66671,252.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='none' fill-rule='nonzero' fill='#ffffff'><path stroke='#ffffff' stroke-opacity='0' id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1 1' d='M164,207.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path stroke='#ffffff' stroke-opacity='0.01' d='M214.66671,252.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='start' font-size='12' font-weight='normal' font-family='sans-serif' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='1' stroke='none' fill-rule='nonzero' fill='none'></g></svg>

                <div data-bubble='{`version`:`1.0`,`style`:`speech`,`tails`:[{`tipX`:337,`tipY`:139,`midpointX`:296,`midpointY`:133,`autoCurve`:true}],`level`:1}' style='left: 31.1477%; top: 26.6435%; width: 34.4672%; height: 8.95077%;' class='bloom-textOverPicture ui-resizable ui-draggable'>
                    <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V'>
                        <div data-languagetipcontent='English' aria-label='false' role='textbox' spellcheck='true' tabindex='0' class='bloom-editable Bubble-style bloom-content1 bloom-contentNational1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                            <p>" + bubble2Text + @"</p>
                        </div>
                    </div>
                </div>";

			string html = GetHtmlForGetBookLevelTests("", textOverPictureHtml);
			TestBookHtmlReturnsExpectedLevel(html, expectedLevel);
		}

		// Mix of normal text boxes and comic speech bubbles - they should all be added up
		[TestCase(0, 0, 1)]
		// Adds to 1
		[TestCase(1, 0, 1)]
		[TestCase(0, 1, 1)]
		// Adds to 2
		[TestCase(2, 0, 1)]
		[TestCase(0, 2, 1)]
		[TestCase(1, 1, 1)]
		// Adds to 10 (L1 Max)
		[TestCase(10, 0, 1)]
		[TestCase(0, 10, 1)]
		[TestCase(5, 5, 1)]
		// Adds to 11 (L2 Min)
		[TestCase(11, 0, 2)]
		[TestCase(0, 11, 2)]
		[TestCase(6, 5, 2)]
		// Adds to 25 (L2 Max)
		[TestCase(25, 0, 2)]
		[TestCase(0, 25, 2)]
		[TestCase(13, 12, 2)]
		// Adds to 26 (L3 Min)
		[TestCase(26, 0, 3)]
		[TestCase(0, 26, 3)]
		[TestCase(13, 13, 3)]
		// Adds to 50 (L3 Max)
		[TestCase(50, 0, 3)]
		[TestCase(0, 50, 3)]
		[TestCase(25, 25, 3)]
		// Adds to 51 (L4 Min)
		[TestCase(51, 0, 4)]
		[TestCase(0, 51, 4)]
		[TestCase(26, 25, 4)]
		public void GetBookComputedLevel_TextBoxAndComicBubbleWithNWords_ReturnsExpectedLevel(int numWords1, int numWords2, int expectedLevel)
		{
			string textBoxText = MakeStringOfXwords(numWords1);
			string bubbleText = MakeStringOfXwords(numWords2);

			string textOverPictureHtml =
				@"<svg class='comical-generated' viewBox='0,0,405,334' height='334' width='405' xmlns:xlink='http://www.w3.org/1999/xlink' xmlns='http://www.w3.org/2000/svg' version='1.1'><g style='mix-blend-mode: normal' text-anchor='start' font-size='12' font-weight='normal' font-family='sans-serif' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='1' stroke='none' fill-rule='nonzero' fill='none'></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='#000000' fill-rule='nonzero' fill='none'><path id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1' d='M197,79.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path d='M247.66671,124.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='none' fill-rule='nonzero' fill='#ffffff'><path stroke='#ffffff' stroke-opacity='0' id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1 1' d='M197,79.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path stroke='#ffffff' stroke-opacity='0.01' d='M247.66671,124.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='#000000' fill-rule='nonzero' fill='none'><path id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1' d='M164,207.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path d='M214.66671,252.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='none' font-size='none' font-weight='none' font-family='none' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='3' stroke='none' fill-rule='nonzero' fill='#ffffff'><path stroke='#ffffff' stroke-opacity='0' id='ie5683671-0bce-4883-e8fc-35001dd3b430outlineShape 1 1' d='M164,207.29954c47.82704,0 79.71173,4.94009 79.71173,24.70046c0,19.76037 -31.88469,24.70046 -79.71173,24.70046c-47.82704,0 -79.71173,-4.94009 -79.71173,-24.70046c0,-19.76037 31.88469,-24.70046 79.71173,-24.70046z'></path><path stroke='#ffffff' stroke-opacity='0.01' d='M214.66671,252.79386c0,0 23.61628,6.87804 47.81278,10.72586c20.57453,3.27184 41.52051,3.48028 41.52051,3.48028v0c0,0 -20.6147,-2.75919 -40.47949,-8.51972c-15.5364,-4.50535 -30.16942,-8.48333 -30.06961,-11.9386'></path></g><g style='mix-blend-mode: normal' text-anchor='start' font-size='12' font-weight='normal' font-family='sans-serif' stroke-dashoffset='0' stroke-dasharray='' stroke-miterlimit='10' stroke-linejoin='miter' stroke-linecap='butt' stroke-width='1' stroke='none' fill-rule='nonzero' fill='none'></g></svg>
                <div data-bubble='{`version`:`1.0`,`style`:`speech`,`tails`:[{`tipX`:337,`tipY`:139,`midpointX`:296,`midpointY`:133,`autoCurve`:true}],`level`:1}' style='left: 31.1477%; top: 26.6435%; width: 34.4672%; height: 8.95077%;' class='bloom-textOverPicture ui-resizable ui-draggable'>
                    <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V'>
                        <div data-languagetipcontent='English' aria-label='false' role='textbox' spellcheck='true' tabindex='0' class='bloom-editable Bubble-style bloom-content1 bloom-contentNational1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                            <p>" + bubbleText + @"</p>
                        </div>
                    </div>
                </div>";

			string html = GetHtmlForGetBookLevelTests(textBoxText, textOverPictureHtml);
			TestBookHtmlReturnsExpectedLevel(html, expectedLevel);
		}

		#region GetWordCount
		[TestCase("a - b")]
		public void GetWordCount_PunctuationBetweenWords_CountsAsSeparator(string input)
		{
			Assert.That(BookAnalyzer.GetWordCount(input), Is.EqualTo(2));
		}

		[TestCase("3.14")]
		[TestCase("can't")]
		[TestCase("can-do")]
		public void GetWordCount_PunctuationWithinWords_NotSeparator(string input)
		{
			Assert.That(BookAnalyzer.GetWordCount(input), Is.EqualTo(1));
		}

		[TestCase("$100")]
		[TestCase("Me?")]
		[TestCase("You!")]
		[TestCase("¿me?")]
		[TestCase("(¡tu!")]
		[TestCase("\"Quotation\"")]
		public void GetWordCount_PunctuationStartEndWords_NotSeparator(string input)
		{
			Assert.That(BookAnalyzer.GetWordCount(input), Is.EqualTo(1));
		}
		#endregion

		// Expected from Bloom 6.1+
		[TestCase("SubscriptionCode", "Test-Thing-***-***")]
		// Expected from Bloom <=6.0
		[TestCase("BrandingProjectName", "Test-Thing")]
		public void LoadFromUploadedSettings_SettingsExist_LoadsSettingsCorrectly(string key, string value)
		{
			using (var tempFolder = new TemporaryFolder("BookAnalyzerTests_LoadFromUploadedSettings_SettingsExist_LoadsSettingsCorrectly"))
			{
				// Create the necessary directory structure
				var collectionFilesDir = Path.Combine(tempFolder.FolderPath, "collectionFiles");
				Directory.CreateDirectory(collectionFilesDir);

				// Create a sample collection settings file
				var uploadCollectionSettingsPath = Path.Combine(collectionFilesDir, "book.uploadCollectionSettings");
				var collectionSettings = $@"<?xml version='1.0' encoding='utf-8'?>
<Collection version='0.2'>
  <Language1Iso639Code>sw</Language1Iso639Code>
  <Language2Iso639Code>en</Language2Iso639Code>
  <Language3Iso639Code>fr</Language3Iso639Code>
  <SignLanguageIso639Code>sgn</SignLanguageIso639Code>
  <Language1Name>Swahili</Language1Name>
  <Language2Name>English</Language2Name>
  <Language3Name>French</Language3Name>
  <SignLanguageName>Sign Language</SignLanguageName>
  <Country>Tanzania</Country>
  <Province>Arusha</Province>
  <District>Monduli</District>
  <{key}>{value}</{key}>
  <DefaultBookTags>bookshelf:test</DefaultBookTags>
</Collection>";
				File.WriteAllText(uploadCollectionSettingsPath, collectionSettings);

				var analyzer = new BookAnalyzer(GetMonoLingualHtml("6.1.0"), GetMetaData(), tempFolder.FolderPath);

				// System under test - load settings
				var result = analyzer.LoadFromUploadedSettings();

				// Verification
				Assert.That(result, Is.True, "LoadFromUploadedSettings should return true when a settings file exists");
				Assert.That(analyzer.Language1Code, Is.EqualTo("sw"), "Language1Code should be loaded from settings");
				Assert.That(analyzer.Language2Code, Is.EqualTo("en"), "Language2Code should be loaded from settings");
				Assert.That(analyzer.Language3Code, Is.EqualTo("fr"), "Language3Code should be loaded from settings");
				Assert.That(analyzer.SignLanguageCode, Is.EqualTo("sgn"), "SignLanguageCode should be loaded from settings");
				Assert.That(analyzer.Country, Is.EqualTo("Tanzania"), "Country should be loaded from settings");
				Assert.That(analyzer.Province, Is.EqualTo("Arusha"), "Province should be loaded from settings");
				Assert.That(analyzer.District, Is.EqualTo("Monduli"), "District should be loaded from settings");
				Assert.That(analyzer.SubscriptionCode, Is.EqualTo(value), "SubscriptionCode should be loaded from settings");
				Assert.That(analyzer.GetBookshelf(), Is.EqualTo("bookshelf:test"), "Bookshelf should be loaded from settings");
			}
		}

		[Test]
		public void LoadFromUploadedSettings_NoSettingsExist_ReturnsFalse()
		{
			using (var tempFolder = new TemporaryFolder("BookAnalyzerTests_LoadFromUploadedSettings_NoSettingsExist_ReturnsFalse"))
			{
				var analyzer = new BookAnalyzer(GetMonoLingualHtml(), GetMetaData(), tempFolder.FolderPath);

				// System under test - try to load settings when none exist
				var result = analyzer.LoadFromUploadedSettings();

				// Verification
				Assert.That(result, Is.False, "LoadFromUploadedSettings should return false when no settings file exists");
			}
		}


		[Test]
		public void GetBestPHashImageSources_Handles_Overlays()
		{
			var analyzer = new BookAnalyzer(kPagesWithOverlayImages, GetMetaData());

			// SUT
			var images = analyzer.GetBestPHashImageSources();

			Assert.That(images, Has.Count.EqualTo(5), "There should be five images in the book");
			Assert.That(images[0], Is.EqualTo("Vacation%202010%20386.jpg"), "First image should be Vacation 2010 386.jpg");
		}
		[Test]
		public void GetBestPHashImageSources_Handles_CoverImageOnly()
		{
			var analyzer = new BookAnalyzer(kPagesWithOnlyCoverImage, GetMetaData());

			// SUT
			var images = analyzer.GetBestPHashImageSources();

			Assert.That(images, Has.Count.EqualTo(1), "There should be only one image in the book");
			Assert.That(images[0], Is.EqualTo("100_1165.jpg"), "Image should be 100_1165.jpg");
		}

		private const string kHtmlUnmodifiedPages = @"<html>
  <head>
    <meta charset='UTF-8' />
  </head>
  <body>
    <div id='bloomDataDiv'>
      <div data-book='contentLanguage1' lang='*'>en</div>
      <div data-book='contentLanguage1Rtl' lang='*'>False</div>
      <div data-book='languagesOfBook' lang='*'>English</div>
    </div>
    <div class='bloom-page cover coverColor bloom-frontMatter frontCover outsideFrontCover side-right A5Portrait' data-page='required singleton' data-export='front-matter-cover' data-xmatter-page='frontCover' id='89a32796-8cf9-4b0f-a694-43a3d705f620' data-page-number=''>
      <div class='pageLabel' lang='en' data-i18n='TemplateBooks.PageLabel.Front Cover'>Front Cover</div>
    </div>
    <div class='bloom-page numberedPage customPage bloom-combinedPage side-right A5Portrait bloom-monolingual' data-page='' id='002627bd-4853-487d-986a-88ea67e0f31c' data-pagelineage='adcd48df-e9ab-4a07-afd4-6a24d0398382' data-page-number='1' lang=''>
      <div class='pageLabel' data-i18n='TemplateBooks.PageLabel.Basic Text &amp; Picture' lang='en'>Basic Text &amp; Picture</div>
      <div class='marginBox'>
        <div style='min-height: 42px;' class='split-pane horizontal-percent'>
          <div class='split-pane-component position-top' style='bottom: 50%'>
            <div class='split-pane-component-inner'>
              <div title='placeHolder.png 6.58 KB 341 x 335 81 DPI (should be 300-600) Bit Depth: 32' class='bloom-imageContainer bloom-leadingElement'>
                <img src='placeHolder.png' alt='place holder'></img>
                <div class='bloom-translationGroup bloom-imageDescription bloom-trailingElement'></div>
              </div>
            </div>
          </div>
          <div class='split-pane-divider horizontal-divider' style='bottom: 50%' />
          <div class='split-pane-component position-bottom' style='height: 50%'>
            <div class='split-pane-component-inner'>
              <div class='bloom-translationGroup bloom-trailingElement' data-default-languages='auto'>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class='bloom-page numberedPage customPage bloom-combinedPage side-left A5Portrait bloom-monolingual' data-page='' id='1e8377b3-f8b3-4fc6-976b-dc3262463880' data-pagelineage='adcd48df-e9ab-4a07-afd4-6a24d0398382' data-page-number='2' lang=''>
      <div class='pageLabel' data-i18n='TemplateBooks.PageLabel.Basic Text &amp; Picture' lang='en'>Basic Text &amp; Picture</div>
      <div class='marginBox'>
        <div style='min-height: 42px;' class='split-pane horizontal-percent'>
          <div class='split-pane-component position-top' style='bottom: 50%'>
            <div class='split-pane-component-inner'>
              <div title='aor_ACC029M.png 35.14 KB 1500 x 806 355 DPI (should be 300-600) Bit Depth: 1' class='bloom-imageContainer bloom-leadingElement'>
                <img src='aor_ACC029M.png' alt=''></img>
                <div class='bloom-translationGroup bloom-imageDescription bloom-trailingElement'></div>
              </div>
            </div>
          </div>
          <div class='split-pane-divider horizontal-divider' style='bottom: 50%' />
          <div class='split-pane-component position-bottom' style='height: 50%'>
            <div class='split-pane-component-inner'>
			  <!-- This vestigial box-header-off group needs to be ignored when counting groups on a page -->
              <div class='box-header-off bloom-translationGroup'>
              </div>
              <div class='bloom-translationGroup bloom-trailingElement' data-default-languages='auto'>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class='bloom-page cover coverColor outsideBackCover bloom-backMatter side-left A5Portrait' data-page='required singleton' data-export='back-matter-back-cover' data-xmatter-page='outsideBackCover' id='f6afe49f-a2fc-480e-80fe-b3262e87868d' data-page-number=''>
      <div class='pageLabel' lang='en' data-i18n='TemplateBooks.PageLabel.Outside Back Cover'>Outside Back Cover</div>
    </div>
  </body>
</html>
";

		private const string kHtmlModifiedPage = @"<html>
  <head>
    <meta charset='UTF-8' />
  </head>
  <body>
    <div class='bloom-page numberedPage customPage bloom-combinedPage A5Portrait bloom-monolingual side-left' data-page='' id='5a72f533-cd59-4e8d-9da5-2fa052144621' data-pagelineage='adcd48df-e9ab-4a07-afd4-6a24d0398382' data-page-number='1' lang=''>
      <div class='pageLabel' data-i18n='TemplateBooks.PageLabel.Basic Text &amp; Picture' lang='en'>Basic Text &amp; Picture</div>
      <div class='marginBox'>
        <div style='min-height: 42px;' class='split-pane horizontal-percent'>
          <div class='split-pane-component position-top' style='bottom: 50%'>
            <div min-height='60px 150px 250px' min-width='60px 150px 250px' style='position: relative;' class='split-pane-component-inner'>
              <div title='PT-eclipse1.jpg 526.06 KB 4010 x 2684 1915 DPI (should be 300-600) Bit Depth: 24' class='bloom-imageContainer bloom-leadingElement'>
                <img src='PT-eclipse1.jpg' alt='sun hidden by moon with corona shining all around the moon&apos;s obscuring disk' height='217' width='324'></img>
                <div class='bloom-translationGroup bloom-imageDescription bloom-trailingElement'>
                  <div data-languagetipcontent='English' aria-label='false' role='textbox' spellcheck='true' tabindex='0' style='min-height: 24px;' class='bloom-editable ImageDescriptionEdit-style bloom-content1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                    <p>sun hidden by moon with corona shining all around the moon's obscuring disk</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class='split-pane-divider horizontal-divider' style='bottom: 50%'></div>
          <div class='split-pane-component position-bottom' style='height: 50%'>
            <div style='min-height: 42px;' class='split-pane horizontal-percent'>
              <div class='split-pane-component position-top'>
                <div min-height='60px 150px' min-width='60px 150px 250px' style='position: relative;' class='split-pane-component-inner'>
                  <div class='bloom-translationGroup bloom-trailingElement' data-default-languages='auto'>
                    <div data-languagetipcontent='English' data-audiorecordingmode='Sentence' aria-label='false' role='textbox' spellcheck='true' tabindex='0' style='min-height: 24px;' class='bloom-editable normal-style bloom-content1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                      <p>Solar Eclipse photographed by Paul Thordarson</p>
                    </div>
                  </div>
                </div>
              </div>
              <div class='split-pane-divider horizontal-divider'></div>
              <div class='split-pane-component position-bottom'>
                <div min-height='60px 150px' min-width='60px 150px 250px' style='position: relative;' class='split-pane-component-inner adding'>
                  <div class='box-header-off bloom-translationGroup'>
                  </div>
                  <div class='bloom-translationGroup bloom-trailingElement'>
                    <div data-languagetipcontent='English' aria-label='false' role='textbox' spellcheck='true' tabindex='0' style='min-height: 24px;' class='bloom-editable normal-style bloom-content1 bloom-visibility-code-on' contenteditable='true' lang='en'>
                      <p>This is a test.</p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </body>
</html>
";

    private string kPagesWithOverlayImages = @"<html>
  <head>
    <meta charset='UTF-8' />
  </head>
  <body>
    <div class='bloom-page cover coverColor bloom-frontMatter frontCover outsideFrontCover darkCoverColor side-right USComicPortrait' data-page='required singleton' data-export='front-matter-cover' data-xmatter-page='frontCover' id='94adfac3-c563-432a-bd23-5c0a91648fce' lang='es' data-page-number=''>
      <div class='marginBox'>
        <div class='bloom-translationGroup bookTitle' data-default-languages='V,N1' data-visibility-variable='cover-title-LN-show'>
          <div class='bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow bloom-visibility-code-on bloom-content1 bloom-contentFirst' lang='en' contenteditable='true' data-book='bookTitle' style='padding-bottom: 3px;'>
            <p>Paper Comic</p>
          </div>
        </div>
        <div class='bloom-canvas' data-imgsizebasedon='545,735'>
          <img data-book='coverImage' src='100_1165.jpg' onerror='this.classList.add('bloom-imageLoadError')' data-copyright='Copyright © 2010, Stephen McConnel' data-creator='Stephen McConnel' data-license='cc-by' alt='' />
          <div class='bloom-translationGroup bloom-imageDescription bloom-trailingElement' data-default-languages='auto'>
            <div class='bloom-editable ImageDescriptionEdit-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' data-book='coverImageDescription' />
            <div class='bloom-editable ImageDescriptionEdit-style bloom-contentNational1' lang='es' contenteditable='true' data-book='coverImageDescription' />
          </div>
        </div>
        <div class='bottomBlock'>
          <div class='bottomTextContent'>
            <div class='creditsRow' data-hint='You may use this space for author/illustrator, or anything else.'>
              <div class='bloom-translationGroup' data-default-languages='[CoverCreditsLanguage] DEFAULT:V'>
                <div class='bloom-editable smallCoverCredits Cover-Default-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' data-book='smallCoverCredits'>
                  <p>Dr Steve</p>
                </div>
              </div>
            </div>
            <div class='bottomRow' data-have-topic='false'>
              <div class='coverBottomLangName Cover-Default-style' data-derived='languagesOfBook' lang='es'>English</div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class='bloom-page numberedPage customPage' id='5a72f533-cd59-4e8d-9da5-2fa052144621' data-page-number='1' lang='en' data-tool-id='overlay'>
      <div class='marginBox'>
        <div class='split-pane-component-inner'>
          <div title='' class='bloom-canvas bloom-has-canvas-element' style=''>
            <svg version='1.1' xmlns='http://www.w3.org/2000/svg' data-lang='en'></svg>
            <img class='bloom-imageObjectFit-cover' src='placeHolder.png' />
            <div class='bloom-canvas-element' style='height: 341.887px; left: 10px; top: 20px; width: 307.904px;' data-bubble='{`version`:`1.0`,`style`:`none`,`tails`:[],`level`:2,`backgroundColors`:[`transparent`],`shadowOffset`:0}'>
              <div tabindex='0' class='bloom-imageContainer bloom-leadingElement'>
                <img src='Vacation%202010%20386.jpg' />
              </div>
            </div>
            <div class='bloom-canvas-element' style='height: 431.793px; left: 23px; top: 369.203px; width: 576.954px;' data-bubble='{`version`:`1.0`,`style`:`none`,`tails`:[],`level`:3,`backgroundColors`:[`transparent`],`shadowOffset`:0}'>
              <div tabindex='0' class='bloom-imageContainer bloom-leadingElement'>
                <img src='Vacation%202010%20481.jpg' />
              </div>
            </div>
            <div class='bloom-canvas-element' style='height: 340.837px; left: 321.023px; top: 20px; width: 278.977px;' data-bubble='{`version`:`1.0`,`style`:`none`,`tails`:[],`level`:4,`backgroundColors`:[`transparent`],`shadowOffset`:0}'>
              <div tabindex='0' class='bloom-imageContainer bloom-leadingElement'>
                <img src='Vacation%202010%20410.jpg' />
              </div>
            </div>
            <div class='bloom-canvas-element' style='left: 190px; top: 890px; width: 290px; height: 30px;' data-bubble='{`version`:`1.0`,`style`:`caption`,`tails`:[],`level`:5,`backgroundColors`:[`#FFFFFF`,`#DFB28B`],`shadowOffset`:5}'>
              <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V' style='font-size: 16px;'>
                <div class='bloom-editable Bubble-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' tabindex='0' spellcheck='false' role='textbox' aria-label='false' data-bubble-alternate='{`lang`:`en`,`style`:`left: 190px; top: 890px; width: 290px; height: 30px;`,`tails`:[]}' data-languagetipcontent='English'>
                  <p>This is a test!</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class='bloom-page numberedPage customPage' id='5592ae39-52f3-4e7a-9a88-4ec1b4273230' data-page-number='2' lang='en' data-tool-id='overlay'>
      <div class='marginBox'>
        <div class='split-pane-component-inner'>
          <div title='' class='bloom-canvas bloom-has-canvas-element' style=''>
            <svg version='1.1' xmlns='http://www.w3.org/2000/svg' data-lang='en'></svg>
            <img class='bloom-imageObjectFit-cover' src='placeHolder.png' />
            <div class='bloom-canvas-element' style='height: 458.234px; left: 10px; top: 20px; width: 611.324px;' data-bubble='{`version`:`1.0`,`style`:`none`,`tails`:[],`level`:2,`backgroundColors`:[`transparent`],`shadowOffset`:0}'>
              <div tabindex='0' class='bloom-imageContainer bloom-leadingElement'>
                <img src='100_2556.jpg' alt='' />
              </div>
            </div>
            <div class='bloom-canvas-element' style='height: 449.839px; left: 20px; top: 480px; width: 600.121px;' data-bubble='{`version`:`1.0`,`style`:`none`,`tails`:[],`level`:3,`backgroundColors`:[`transparent`],`shadowOffset`:0}'>
              <div tabindex='0' class='bloom-imageContainer bloom-leadingElement'>
                <img src='100_1250.jpg' alt='' />
              </div>
            </div>
            <div class='bloom-canvas-element' style='left: 230px; top: 930px; width: 140px; height: 30px;' data-bubble='{`version`:`1.0`,`style`:`caption`,`tails`:[],`level`:4,`backgroundColors`:[`#FFFFFF`,`#DFB28B`],`shadowOffset`:5}' data-bloom-active='true'>
              <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V' style='font-size: 16px;'>
                <div class='bloom-editable Bubble-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' tabindex='0' spellcheck='false' role='textbox' aria-label='false' data-bubble-alternate='{`lang`:`en`,`style`:`left: 230px; top: 930px; width: 140px; height: 30px;`,`tails`:[]}' data-languagetipcontent='English'>
                  <p>Two more pictures</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </body>
</html>
";

		private string kPagesWithOnlyCoverImage = @"<html>
  <head>
    <meta charset='UTF-8' />
  </head>
  <body>
    <div class='bloom-page cover coverColor bloom-frontMatter frontCover outsideFrontCover darkCoverColor side-right USComicPortrait' data-page='required singleton' data-export='front-matter-cover' data-xmatter-page='frontCover' id='94adfac3-c563-432a-bd23-5c0a91648fce' lang='es' data-page-number=''>
      <div class='marginBox'>
        <div class='bloom-translationGroup bookTitle' data-default-languages='V,N1' data-visibility-variable='cover-title-LN-show'>
          <div class='bloom-editable bloom-nodefaultstylerule Title-On-Cover-style bloom-padForOverflow bloom-visibility-code-on bloom-content1 bloom-contentFirst' lang='en' contenteditable='true' data-book='bookTitle' style='padding-bottom: 3px;'>
            <p>Paper Comic</p>
          </div>
        </div>
        <div class='bloom-canvas' data-imgsizebasedon='545,735'>
          <img data-book='coverImage' src='100_1165.jpg' onerror='this.classList.add('bloom-imageLoadError')' data-copyright='Copyright © 2010, Stephen McConnel' data-creator='Stephen McConnel' data-license='cc-by' alt='' />
          <div class='bloom-translationGroup bloom-imageDescription bloom-trailingElement' data-default-languages='auto'>
            <div class='bloom-editable ImageDescriptionEdit-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' data-book='coverImageDescription' />
            <div class='bloom-editable ImageDescriptionEdit-style bloom-contentNational1' lang='es' contenteditable='true' data-book='coverImageDescription' />
          </div>
        </div>
        <div class='bottomBlock'>
          <div class='bottomTextContent'>
            <div class='creditsRow' data-hint='You may use this space for author/illustrator, or anything else.'>
              <div class='bloom-translationGroup' data-default-languages='[CoverCreditsLanguage] DEFAULT:V'>
                <div class='bloom-editable smallCoverCredits Cover-Default-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' data-book='smallCoverCredits'>
                  <p>Dr Steve</p>
                </div>
              </div>
            </div>
            <div class='bottomRow' data-have-topic='false'>
              <div class='coverBottomLangName Cover-Default-style' data-derived='languagesOfBook' lang='es'>English</div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <div class='bloom-page numberedPage customPage' id='5a72f533-cd59-4e8d-9da5-2fa052144621' data-page-number='1' lang='en' data-tool-id='overlay'>
      <div class='marginBox'>
        <div class='split-pane-component-inner'>
          <div title='' class='bloom-canvas bloom-has-canvas-element' style=''>
            <svg version='1.1' xmlns='http://www.w3.org/2000/svg' data-lang='en'></svg>
            <img class='bloom-imageObjectFit-cover' src='placeHolder.png' />
            <div class='bloom-canvas-element' style='left: 190px; top: 890px; width: 290px; height: 30px;' data-bubble='{`version`:`1.0`,`style`:`caption`,`tails`:[],`level`:5,`backgroundColors`:[`#FFFFFF`,`#DFB28B`],`shadowOffset`:5}'>
              <div class='bloom-translationGroup bloom-leadingElement' data-default-languages='V' style='font-size: 16px;'>
                <div class='bloom-editable Bubble-style bloom-visibility-code-on bloom-content1' lang='en' contenteditable='true' tabindex='0' spellcheck='false' role='textbox' aria-label='false' data-bubble-alternate='{`lang`:`en`,`style`:`left: 190px; top: 890px; width: 290px; height: 30px;`,`tails`:[]}' data-languagetipcontent='English'>
                  <p>This is a test!</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </body>
</html>
";
	}
}
