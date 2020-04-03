using System;
using System.Collections.Generic;
using System.Xml.Linq;
using BloomHarvester;
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
		private BookAnalyzer _emptyBrandingAnalyzer;
		private BookAnalyzer _silleadBrandingAnalyzer;
		private XElement _twoLanguageCollection;
		private XElement _threeLanguageCollection;
		private XElement _silleadCollection;
		private BookAnalyzer _epubCheckAnalyzer;
		private BookAnalyzer _epubCheckAnalyzer2;

		private const string kHtml = @"
<html>
	<head>
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

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			// Insert the appropriate contentLanguageX/bloom-content*X information for the different types of books
			string contentLanguage1Xml = "<div data-book='contentLanguage1' lang='*'>xk</div>";
			string contentLanguage2Xml = "<div data-book='contentLanguage2' lang='*'>fr</div>";
			string contentLanguage3Xml = "<div data-book='contentLanguage3' lang='*'>de</div>";

			var contentClassForLanguage3 = "bloom-contentNational2";

			string trilingualHtml = String.Format(kHtml, contentLanguage1Xml + contentLanguage2Xml + contentLanguage3Xml, contentClassForLanguage3);
			_trilingualAnalyzer = new BookAnalyzer(trilingualHtml, GetMetaData());

			var bilingualHtml = String.Format(kHtml, contentLanguage1Xml + contentLanguage2Xml, contentClassForLanguage3);
			_bilingualAnalyzer = new BookAnalyzer(bilingualHtml, GetMetaData());

			var monoLingualHtml = String.Format(kHtml, contentLanguage1Xml, contentClassForLanguage3).Replace("bloom-content2 ", "");
			_emptyBrandingAnalyzer = new BookAnalyzer(monoLingualHtml, GetMetaData(@"""brandingProjectName"":"""","));
			_silleadBrandingAnalyzer = new BookAnalyzer(monoLingualHtml, GetMetaData(@"""brandingProjectName"":""SIL-LEAD"","));
			_monolingualBookInTrilingualCollectionAnalyzer = new BookAnalyzer(monoLingualHtml, GetMetaData());

			contentClassForLanguage3 = "";
			var monoLingualBookInBilingualCollectionHtml = String.Format(kHtml, contentLanguage1Xml, contentClassForLanguage3).Replace("bloom-content2 ", "");
			_monolingualBookInBilingualCollectionAnalyzer = new BookAnalyzer(monoLingualBookInBilingualCollectionHtml, GetMetaData());

			_twoLanguageCollection = XElement.Parse(_monolingualBookInBilingualCollectionAnalyzer.BloomCollection);
			_threeLanguageCollection = XElement.Parse(_monolingualBookInTrilingualCollectionAnalyzer.BloomCollection);
			_silleadCollection = XElement.Parse(_silleadBrandingAnalyzer.BloomCollection);

			_epubCheckAnalyzer = new BookAnalyzer(kHtmlUnmodifiedPages, GetMetaData());
			_epubCheckAnalyzer2 = new BookAnalyzer(kHtmlModifiedPage, GetMetaData());
		}

		private string GetMetaData(string brandingJson = "")
		{
			const string meta = @"{""a11y_NoEssentialInfoByColor"":false,""a11y_NoTextIncludedInAnyImages"":false,""epub_HowToPublishImageDescriptions"":0,""epub_RemoveFontStyles"":false,""bookInstanceId"":""11c2c600-35af-488b-a8d6-3479edcb9217"",""suitableForMakingShells"":false,""suitableForMakingTemplates"":false,""suitableForVernacularLibrary"":true,""bloomdVersion"":0,""experimental"":false,{0}""folio"":false,""isRtl"":false,""title"":""Aeneas"",""allTitles"":""{\""en\"":\""Aeneas\"",\""es\"":\""Spanish title\""}"",""baseUrl"":null,""bookOrder"":null,""isbn"":"""",""bookLineage"":""056B6F11-4A6C-4942-B2BC-8861E62B03B3"",""downloadSource"":""ken@example.com/11c2c600-35af-488b-a8d6-3479edcb9217"",""license"":""cc-by"",""formatVersion"":""2.0"",""licenseNotes"":""Please be nice to John"",""copyright"":""Copyright © 2018, JohnT"",""credits"":"""",""tags"":[],""pageCount"":10,""languages"":[],""langPointers"":[{""__type"":""Pointer"",""className"":""language"",""objectId"":""2cy807OQoe""},{""__type"":""Pointer"",""className"":""language"",""objectId"":""VUiYTJhOyJ""},{""__type"":""Pointer"",""className"":""language"",""objectId"":""jwP3nu7XGY""}],""summary"":null,""allowUploadingToBloomLibrary"":true,""bookletMakingIsAppropriate"":true,""LeveledReaderTool"":null,""LeveledReaderLevel"":0,""country"":"""",""province"":"""",""district"":"""",""xmatterName"":null,""uploader"":{""__type"":""Pointer"",""className"":""_User"",""objectId"":""TWGrqk7NaR""},""tools"":[],""currentTool"":""talkingBookTool"",""toolboxIsOpen"":true,""author"":null,""subjects"":null,""hazards"":null,""a11yFeatures"":null,""a11yLevel"":null,""a11yCertifier"":null,""readingLevelDescription"":null,""typicalAgeRange"":null,""features"":[""blind"",""signLanguage""],""language-display-names"":{""xk"":""Vernacular"",""fr"":""French"",""pt"":""Portuguese"",""de"":""German""}}";
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
		}

		[Test]
		public void Language3Code_InMonolingualBook()
		{
			Assert.That(_monolingualBookInBilingualCollectionAnalyzer.Language3Code, Is.Empty);
			Assert.That(_monolingualBookInTrilingualCollectionAnalyzer.Language3Code, Is.EqualTo("de"));
		}

		[Test]
		public void Branding_Specified()
		{
			Assert.That(_silleadBrandingAnalyzer.Branding, Is.EqualTo("SIL-LEAD"));
		}

		[Test]
		public void Branding_Empty()
		{
			Assert.That(_emptyBrandingAnalyzer.Branding, Is.EqualTo(""));
		}

		[Test]
		public void Branding_Missing()
		{
			Assert.That(_monolingualBookInBilingualCollectionAnalyzer.Branding, Is.Null);
		}

		[Test]
		public void BookCollection_HasLanguage1Code()
		{
			Assert.That(_twoLanguageCollection.Element("Language1Iso639Code")?.Value, Is.EqualTo("xk"));
			Assert.That(_threeLanguageCollection.Element("Language1Iso639Code")?.Value, Is.EqualTo("xk"));
		}

		[Test]
		public void BookCollection_HasLanguage2Code()
		{
			Assert.That(_twoLanguageCollection.Element("Language2Iso639Code")?.Value, Is.EqualTo("fr"));
			Assert.That(_threeLanguageCollection.Element("Language2Iso639Code")?.Value, Is.EqualTo("fr"));
		}

		[Test]
		public void BookCollection_HasLanguage3Code()
		{
			Assert.That(_threeLanguageCollection.Element("Language3Iso639Code")?.Value, Is.EqualTo("de"));
		}

		[Test]
		public void BookCollection_HasBranding()
		{
			Assert.That(_silleadCollection.Element("BrandingProjectName")?.Value, Is.EqualTo("SIL-LEAD"));
		}

		[Test]
		public void BookCollection_HasCorrectLanguageNames()
		{
			Assert.That(_threeLanguageCollection.Element("Language1Name")?.Value, Is.EqualTo("Vernacular"));
			Assert.That(_threeLanguageCollection.Element("Language2Name")?.Value, Is.EqualTo("French"));
			Assert.That(_threeLanguageCollection.Element("Language3Name")?.Value, Is.EqualTo("German"));
		}

		[Test]
		public void IsEpubSuitable_Works()
		{
			Assert.That(_epubCheckAnalyzer.IsEpubSuitable(), Is.True, "Unmodified Basic Text & Picture pages should be suitable for Epub");
			Assert.That(_epubCheckAnalyzer2.IsEpubSuitable(), Is.False, "Modified Basic Text & Picture page should not be suitable for Epub");
		}

		private string CreateHtmlForGetBookLevelTests(string page1Text)
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
				<div class=' bloom-textOverPicture'>
				  <div class='bloom-translationGroup'>
					<div class='bloom-editable' lang='en'>This is a bunch of really really long text that would bump it above Level 1, but it's in an imageDescription, which is also ignored.
					</div>
				  </div>
				</div>
              </div>
            </div>
          </div>
          <div class='split-pane-divider horizontal-divider' style='bottom: 50%' />
          <div class='split-pane-component position-bottom' style='height: 50%'>
            <div class='split-pane-component-inner'>
              <div class='bloom-translationGroup bloom-trailingElement' data-default-languages='auto'>
				<div class='bloom-editable' lang='en'>{page1Text}</div>
				<div class='bloom-editable' lang='es'>Este texto va a ser ignored porque no es en la lingua prima del libro. Uno dos tres cuatro cinco seis siete ocho nueve diaz.</div>
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

		[TestCase("One.Two.Three.Four.Five.Six.Seven.Eight.Nine.Ten.Eleven", 1, Description = "Punctuation test")]
		public void TestBookLevel(string input, int expectedLevel)
		{
			string html = CreateHtmlForGetBookLevelTests($"<p>{input}</p>");

			var analyzer = new BookAnalyzer(html, GetMetaData());
			Assert.That(analyzer.GetBookComputedLevel(), Is.EqualTo(expectedLevel));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(10)]
		public void GetBookLevel_Level1Book(int numWords)
		{
			var list = new List<string>();
			for (int i = 0; i < numWords; ++i)
			{
				list.Add((i).ToString());
			}
			string input = String.Join(" ", list);

			TestBookLevel(input, 1);
		}

		[TestCase(11)]
		[TestCase(25)]
		public void GetBookLevel_Level2Book(int numWords)
		{
			var list = new List<string>();
			for (int i = 0; i < numWords; ++i)
			{
				list.Add((i).ToString());
			}
			string input = String.Join(" ", list);

			TestBookLevel(input, 2);
		}

		[TestCase(26)]
		[TestCase(50)]
		public void GetBookLevel_Level3Book(int numWords)
		{
			var list = new List<string>();
			for (int i = 0; i < numWords; ++i)
			{
				list.Add((i).ToString());
			}
			string input = String.Join(" ", list);

			TestBookLevel(input, 3);
		}

		[TestCase(51)]
		[TestCase(75)]
		public void GetBookLevel_Level4Book(int numWords)
		{
			var list = new List<string>();
			for (int i = 0; i < numWords; ++i)
			{
				list.Add((i).ToString());
			}
			string input = String.Join(" ", list);

			TestBookLevel(input, 4);
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
	}
}
