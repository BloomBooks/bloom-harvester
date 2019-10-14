using System;
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
	}
}
