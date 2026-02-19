using BloomHarvester.Parse.Model;
using NUnit.Framework;
using System.Collections.Generic;
using Newtonsoft.Json;
using BloomHarvester;

namespace BloomHarvesterTests.Parse.Model
{
	[TestFixture]
	public class BookModelTests
	{
		internal const string kDefaultObjectId = "FakeObjectId";
		internal const string kDefaultBaseUrl = "https://s3.amazonaws.com/FakeBucket/fakeUploader%40gmail.com%2fFakeGuid%2fFakeTitle%2f";
		internal const string kDefaultTitle = "FakeTitle";

		/// <summary>
		/// A convenience method to create a default book, which has no need of setting readonly parameters
		/// If you require setting readonly parameters, call BookModel's non-default constructor instead
		/// </summary>
		/// <returns>A BookModel object that has the unittests default values for the required fields</returns>
		internal static BookModel CreateBookModel()
		{
			return new BookModel(kDefaultBaseUrl, kDefaultTitle) { ObjectId = kDefaultObjectId } ;
		}

		[TestCase("HarvestState")]
		[TestCase("HarvesterId")]
		[TestCase("HarvesterMajorVersion")]
		[TestCase("HarvesterMinorVersion")]
		[TestCase("HarvestStartedAt")]
		[TestCase("HarvestLogEntries")]
		[TestCase("Features")]
		[TestCase("Tags")]
		[TestCase("Show")]
		public void BookModel_GetWriteableMembers_IncludesExpectedMember(string expectedMemberName)
		{
			var book = new BookModel();
			var method = typeof(BookModel).GetMethod("GetWriteableMembers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			var writeableMembers = method.Invoke(book, null) as HashSet<string>;

			Assert.That(writeableMembers.Contains(expectedMemberName), Is.True);
		}

		[Test]
		public void BookModel_GetParseKeys_ContainsImportantKeys()
		{
			var parseKeys = BookModel.GetParseKeys();

			Assert.That(parseKeys.Count, Is.GreaterThanOrEqualTo(1));

			var parseKeySet = new HashSet<string>(parseKeys);
			Assert.That(parseKeySet.Contains("objectId"), "objectId: Parent class keys should also be returned");

			Assert.That(parseKeySet.Contains("harvestState"), "harvestState");
			Assert.That(parseKeySet.Contains("harvesterMajorVersion"), "harvesterMajorVersion");
			Assert.That(parseKeySet.Contains("harvesterMinorVersion"), "harvesterMinorVersion");
			Assert.That(parseKeySet.Contains("harvestLog"), "harvestLog");
			Assert.That(parseKeySet.Contains("harvestStartedAt"), "harvestStartedAt");

			Assert.That(parseKeySet.Contains("baseUrl"), "baseUrl");
			Assert.That(parseKeySet.Contains("inCirculation"), "inCirculation");
			Assert.That(parseKeySet.Contains("lastUploaded"), "lastUploaded");
			Assert.That(parseKeySet.Contains("phashOfFirstContentImage"), "pHash");
			Assert.That(parseKeySet.Contains("show"), "show");
			Assert.That(parseKeySet.Contains("tags"), "tags");
			Assert.That(parseKeySet.Contains("title"), "title");

			// ENHANCE: Add as many of the other keys as you want to verify.
		}
		[Test]
		public void BookModel_NonEmptyTag_SerializedInJson()
		{
			var book = new BookModel();
			book.Tags = new string[] { "computedLevel:1" };

			string bookJson = JsonConvert.SerializeObject(book);
			string expectedJsonFragment = "\"tags\":[\"computedLevel:1\"]";

			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}

		[Test]
		public void BookModel_GetNewBookUpdateOperation_AddsUpdateSource()
		{
			// System under test
			var bookUpdateOp = BookModel.GetNewBookUpdateOperation();
			var result = bookUpdateOp._updatedFieldValues;

			// Verification
			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");

			CollectionAssert.AreEquivalent(expectedResult, result);
		}

		#region GetBloomLibraryBookDetailLink
		[Test]
		public void BookModel_GetBloomLibraryBookDetailLink_Prod_PopulatesLink()
		{
			var bookModel = new BookModel() { ObjectId = "myObjectId" };

			string url = bookModel.GetDetailLink(EnvironmentSetting.Prod);

			Assert.That(url, Is.EqualTo("https://bloomlibrary.org/browse/detail/myObjectId"));
		}

		[Test]
		public void BookModel_GetBloomLibraryBookDetailLink_Dev_PopulatesLink()
		{
			var bookModel = new BookModel() { ObjectId = "myObjectId" };

			string url = bookModel.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.EqualTo("https://dev.bloomlibrary.org/browse/detail/myObjectId"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		public void BookModel_GetBloomLibraryBookDetailLink_BadInput_ErrorReported(string badObjectId)
		{
			var book = new BookModel() { ObjectId = badObjectId };

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.Null);
		}
		#endregion

		#region UpdateTagsForBookshelf
		[Test]
		public void BookModel_UpdateTagsForBookshelf_IgnoresEmptyWhenNoBookshelf()
		{
			var bookModel = new BookModel() { Tags = new string[] { "computedLevel:1" } };
			bookModel.UpdateBookshelfTag("");
			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1" }, bookModel.Tags, "Tags should contain only computedLevel:1");
		}

		[Test]
		public void BookModel_UpdateTagsForBookshelf_RemovesBookshelfWhenEmptyNewValue()
		{
			var bookModel = new BookModel() { Tags = new string[] { "computedLevel:1", "bookshelf:Removed" } };
			bookModel.UpdateBookshelfTag("");
			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1" }, bookModel.Tags, "Tags should contain only computedLevel:1");
		}

		[Test]
		public void BookModel_UpdateTagsForBookshelf_AddsBookshelfWhenNoneExisted()
		{
			var bookModel = new BookModel() { Tags = new string[] { "computedLevel:1" } };
			bookModel.UpdateBookshelfTag("bookshelf:Added");
			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1", "bookshelf:Added" }, bookModel.Tags, "Tags should contain bookshelf:Added");
		}

		[Test]
		public void BookModel_UpdateTagsForBookshelf_ReplacesBookshelfWhenOneExists()
		{
			var bookModel = new BookModel() { Tags = new string[] { "computedLevel:1", "bookshelf:Replaced" } };
			bookModel.UpdateBookshelfTag("bookshelf:Replacement");
			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1", "bookshelf:Replacement" }, bookModel.Tags, "bookshelf: tags should be only bookshelf:Replacement");
		}

		[Test]
		public void BookModel_UpdateTagsForBookshelf_ReplacesBookshelvesWhenTwoExist()
		{
			var bookModel = new BookModel() { Tags = new string[] { "computedLevel:1", "bookshelf:First Replaced", "bookshelf:Second Replaced" } };
			bookModel.UpdateBookshelfTag("bookshelf:Replacement");
			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1", "bookshelf:Replacement" }, bookModel.Tags, "bookshelf: tags should be only bookshelf:Replacement");
		}

		[Test]
		public void BookModel_UpdateTagsForBookshelf_RetainsSameBookshelfWhenItAlreadyExists()
		{
			var bookModel = new BookModel() { Tags = new string[] { "computedLevel:1", "bookshelf:Retain" } };
			bookModel.UpdateBookshelfTag("bookshelf:Retain");
			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1", "bookshelf:Retain" }, bookModel.Tags, "bookshelf: tags should still be one bookshelf:Retain");
		}
#endregion
	}
}
