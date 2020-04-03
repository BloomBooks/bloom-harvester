using BloomHarvester;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;

namespace BloomHarvesterTests
{
	[TestFixture]
	class BookTests
	{
		private static Book CreateBook(BookModel model)
		{
			return new Book(model, new NullLogger());
		}

		#region GetTagDictionary
		[TestCase(null, "null array case")]
		[TestCase(new string[] { }, "mpty array case")]
		public void Book_GetTagDictionary_Empty(string[] initialValue, string message)
		{
			var book = CreateBook(new BookModel()
			{
				Tags = initialValue
			});

			var invoker = new VSUnitTesting.PrivateObject(book);
			var map = invoker.Invoke("GetTagDictionary") as Dictionary<string, List<string>>;

			Assert.That(map.Count, Is.EqualTo(0), message);
		}

		[Test]
		public void Book_GetTagDictionary_SingleTag()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				Tags = new string[] { "system:Incoming" }
			});

			// System under test
			var invoker = new VSUnitTesting.PrivateObject(book);
			var map = invoker.Invoke("GetTagDictionary") as Dictionary<string, List<string>>;

			// Verification
			var expected = new Dictionary<string, List<string>>();
			expected.Add("system", (new string[] { "Incoming" }).ToList());

			CollectionAssert.AreEquivalent(expected, map);
		}

		[Test]
		public void Book_GetTagDictionary_TagWithSpace()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				Tags = new string[] { "system: Incoming" }
			});

			// System under test
			var invoker = new VSUnitTesting.PrivateObject(book);
			var map = invoker.Invoke("GetTagDictionary") as Dictionary<string, List<string>>;

			// Verification
			var expected = new Dictionary<string, List<string>>();
			expected.Add("system", (new string[] { "Incoming" }).ToList());

			CollectionAssert.AreEquivalent(expected, map);
		}

		[Test]
		public void Book_GetTagDictionary_TwoDistinctTags()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				Tags = new string[] { "system:Incoming", "computedLevel:2" }
			});

			// System under test
			var invoker = new VSUnitTesting.PrivateObject(book);
			var map = invoker.Invoke("GetTagDictionary") as Dictionary<string, List<string>>;

			// Verification
			var expected = new Dictionary<string, List<string>>();
			expected.Add("system", (new string[] { "Incoming" }).ToList());
			expected.Add("computedLevel", (new string[] { "2" }).ToList());

			CollectionAssert.AreEquivalent(expected, map);
		}

		[Test]
		public void Book_GetTagDictionary_DuplicateKey_MergedIntoSingeList()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				Tags = new string[] { "bookshelf:Guatemala", "bookshelf:Comics" }
			});

			// System under test
			var invoker = new VSUnitTesting.PrivateObject(book);
			var map = invoker.Invoke("GetTagDictionary") as Dictionary<string, List<string>>;

			// Verification
			var expected = new Dictionary<string, List<string>>();
			expected.Add("bookshelf", (new string[] { "Guatemala", "Comics" }).ToList());

			CollectionAssert.AreEquivalent(expected, map);
		}
		#endregion

		#region SetTags
		[TestCase(null, "null array")]
		[TestCase(new string[0], "empty array")]
		public void Book_SetTags_OneOtherTagBefore_NewTagAdded(string[] initialValue, string message)
		{
			var book = CreateBook(new BookModel()
			{
				Tags = initialValue
			});

			var stubAnalyzer = Substitute.For<IBookAnalyzer>();
			stubAnalyzer.GetBookComputedLevel().Returns(1);
			book.Analyzer = stubAnalyzer;

			book.SetTags();

			CollectionAssert.AreEquivalent(new string[] { "computedLevel:1" }, book.Model.Tags, message);
		}

		[Test]
		public void Book_SetTags_OneOtherTagBefore_NewTagAdded()
		{
			var book = CreateBook(new BookModel()
			{
				Tags = new string[] { "system:Incoming" }
			});

			var stubAnalyzer = Substitute.For<IBookAnalyzer>();
			stubAnalyzer.GetBookComputedLevel().Returns(2);
			book.Analyzer = stubAnalyzer;

			book.SetTags();

			CollectionAssert.AreEquivalent(new string[] { "system:Incoming", "computedLevel:2" }, book.Model.Tags);
		}

		[Test]
		public void Book_SetTags_TagAlreadyExists_TagReplaced()
		{
			var book = CreateBook(new BookModel()
			{
				Tags = new string[] { "computedLevel:2" }
			});

			var stubAnalyzer = Substitute.For<IBookAnalyzer>();
			stubAnalyzer.GetBookComputedLevel().Returns(3);
			book.Analyzer = stubAnalyzer;

			book.SetTags();

			CollectionAssert.AreEquivalent(new string[] { "computedLevel:3" }, book.Model.Tags);
		}
		#endregion

		#region SetHarvesterEvaluation
		[Test]
		public void Book_SetHarvesterEvaluation_InsertsProperly()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				HarvestState = "Done",
				HarvestStartedAt = new ParseDate(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
			});

			// System under test
			book.SetHarvesterEvaluation("epub", false);
			book.SetHarvesterEvaluation("pdf", true);
			book.SetHarvesterEvaluation("bloomReader", true);
			book.SetHarvesterEvaluation("readOnline", true);

			//Verify
			string bookJson = JsonConvert.SerializeObject(book.Model);
			string expectedJsonFragment = "\"show\":{\"epub\":{\"harvester\":false},\"pdf\":{\"harvester\":true},\"bloomReader\":{\"harvester\":true},\"readOnline\":{\"harvester\":true}}";
			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}

		[Test]
		public void Book_SetHarvesterEvaluation_UpdatesProperly()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				HarvestState = "Done",
				HarvestStartedAt = new ParseDate(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>(),
				Show = JsonConvert.DeserializeObject("{\"epub\":{\"harvester\":false, \"librarian\":true, \"user\":true},\"pdf\":{\"harvester\":false, \"librarian\":false, \"user\":false},\"bloomReader\":{\"harvester\":false, \"librarian\":false},\"readOnline\":{\"harvester\":false, \"user\":true}}")
			});

			// System under test
			book.SetHarvesterEvaluation("epub", true);
			book.SetHarvesterEvaluation("pdf", true);
			book.SetHarvesterEvaluation("bloomReader", true);
			book.SetHarvesterEvaluation("readOnline", true);

			//Verify
			string bookJson = JsonConvert.SerializeObject(book.Model);
			string expectedJsonFragment = "\"show\":{\"epub\":{\"harvester\":true,\"librarian\":true,\"user\":true},\"pdf\":{\"harvester\":true,\"librarian\":false,\"user\":false},\"bloomReader\":{\"harvester\":true,\"librarian\":false},\"readOnline\":{\"harvester\":true,\"user\":true}}";
			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}
		#endregion

		#region GetBloomLibraryBookDetailLink
		[Test]
		public void Book_GetBloomLibraryBookDetailLink_Prod_PopulatesLink()
		{
			var book = CreateBook(new BookModel()
			{
				ObjectId = "myObjectId"
			});

			string url = book.GetDetailLink(EnvironmentSetting.Prod);

			Assert.That(url, Is.EqualTo("https://bloomlibrary.org/browse/detail/myObjectId"));
		}

		[Test]
		public void Book_GetBloomLibraryBookDetailLink_Dev_PopulatesLink()
		{
			var book = CreateBook(new BookModel()
			{
				ObjectId = "myObjectId"
			});

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.EqualTo("https://dev.bloomlibrary.org/browse/detail/myObjectId"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		public void Book_GetBloomLibraryBookDetailLink_BadInput_ErrorReported(string badObjectId)
		{
			var book = CreateBook(new BookModel()
			{
				ObjectId = badObjectId
			});

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.Null);
		}
		#endregion

		[Test]
		public void Book_UpdateMetadata_NotEqual_PendingUpdatesFound()
		{
			// Setup
			var book = CreateBook(new BookModel()
			{
				Features = new string[] { "talkingBook" }
			});
			book.Model.MarkAsDatabaseVersion();

			var metaData = new Bloom.Book.BookMetaData();
			metaData.Features = new string[] { "talkingBook", "talkingBook:en" };

			// Test
			book.UpdateMetadataIfNeeded(metaData);
			UpdateOperation pendingUpdates = book.Model.GetPendingUpdates();

			// Verification
			var updateDict = pendingUpdates._updatedFieldValues;
			Assert.IsTrue(updateDict.Any(), "PendingUpdates.Any()");

			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");   // This key should be present so Parse knows it's not a BloomDesktop upload
			expectedResult.Add("features", "[\"talkingBook\",\"talkingBook:en\"]");
			CollectionAssert.AreEquivalent(updateDict, expectedResult);
		}
	}
}
