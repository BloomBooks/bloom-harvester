using BloomHarvester;
using BloomHarvester.Logger;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using BloomHarvesterTests.Parse.Model;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using SIL.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using BloomHarvester.IO;

namespace BloomHarvesterTests
{
	[TestFixture]
	class BookTests
	{
		internal static Book CreateDefaultBook(IFileIO fileIO=null)
		{
			if (fileIO == null)
				fileIO = new FileIO();
			return new Book(BookModelTests.CreateBookModel(), new NullLogger(), fileIO);
		}

		internal static Book CreateBook(BookModel model, IFileIO fileIO=null)
		{
			if (fileIO == null)
				fileIO = new FileIO();
			return new Book(model, new NullLogger(), fileIO);
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

			var method = book.GetType().GetMethod("GetTagDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
			var map = method.Invoke(book, null) as Dictionary<string, List<string>>;
			
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
			var method = book.GetType().GetMethod("GetTagDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
			var map = method.Invoke(book, null) as Dictionary<string, List<string>>;


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
			var method = book.GetType().GetMethod("GetTagDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
			var map = method.Invoke(book, null) as Dictionary<string, List<string>>;


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
			var method = book.GetType().GetMethod("GetTagDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
			var map = method.Invoke(book, null) as Dictionary<string, List<string>>;


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
			var method = book.GetType().GetMethod("GetTagDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
			var map = method.Invoke(book, null) as Dictionary<string, List<string>>;


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
			book.SetHarvesterEvaluation("social", true);

			//Verify
			string bookJson = JsonConvert.SerializeObject(book.Model);
			string expectedJsonFragment = "\"show\":{\"epub\":{\"harvester\":false},\"pdf\":{\"harvester\":true},\"bloomReader\":{\"harvester\":true},\"readOnline\":{\"harvester\":true},\"social\":{\"harvester\":true}}";
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
				Show = JsonConvert.DeserializeObject("{\"epub\":{\"harvester\":false, \"librarian\":true, \"user\":true},\"pdf\":{\"harvester\":false, \"librarian\":false, \"user\":false},\"bloomReader\":{\"harvester\":false, \"librarian\":false},\"readOnline\":{\"harvester\":false, \"user\":true},\"social\":{\"harvester\":false, \"user\":true}}")
			});

			// System under test
			book.SetHarvesterEvaluation("epub", true);
			book.SetHarvesterEvaluation("pdf", true);
			book.SetHarvesterEvaluation("bloomReader", true);
			book.SetHarvesterEvaluation("readOnline", true);
			book.SetHarvesterEvaluation("social", true);

			//Verify
			string bookJson = JsonConvert.SerializeObject(book.Model);
			string expectedJsonFragment = "\"show\":{\"epub\":{\"harvester\":true,\"librarian\":true,\"user\":true},\"pdf\":{\"harvester\":true,\"librarian\":false,\"user\":false},\"bloomReader\":{\"harvester\":true,\"librarian\":false},\"readOnline\":{\"harvester\":true,\"user\":true},\"social\":{\"harvester\":true,\"user\":true}}";
			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}
		#endregion

		[TestCase("")]
		[TestCase("\r")]
		[TestCase("\n")]
		[TestCase("\r\n")]
		public void Book_UpdatePerceptualHash_NormalHash_ModelUpdated(string lineEnding)
		{
			var book = CreateBook(new BookModel());
			string pHashDigest = "0xADA0BB7900AC75A7B67B66FEB6BF84D2B4AAAAAD9B9DB6C0B2A3B7AFAFA7ABACA7B0B3AFAEABB2AF";
			using (var file = new TempFile(pHashDigest + lineEnding))
			{
				book.UpdatePerceptualHash(file.Path);
			}

			Assert.That(book.Model.PHashOfFirstContentImage, Is.EqualTo(pHashDigest));
		}

		[TestCase("null")]
		[TestCase("null\r")]
		[TestCase("null\n")]
		[TestCase("null\r\n")]
		[TestCase("0x00000000000000000000000000000000000000000000000000000000000000000000000000000000")]
		[TestCase("0x00000000000000000000000000000000000000000000000000000000000000000000000000000000\r\n")]
		public void Book_UpdatePerceptualHash_InvalidHash_ModelUpdatedWithNullValue(string invalidPHash)
		{
			var book = CreateBook(new BookModel());
			using (var file = new TempFile(invalidPHash))
			{
				book.UpdatePerceptualHash(file.Path);
			}

			// Namely, null value not a literal string "null"
			Assert.That(book.Model.PHashOfFirstContentImage, Is.EqualTo(null));			
		}

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
			metaData.SubscriptionDescriptor = "SIL-LEAD";

			// Test
			book.UpdateMetadataIfNeeded(metaData);
			UpdateOperation pendingUpdates = book.Model.GetPendingUpdates();

			// Verification
			var updateDict = pendingUpdates._updatedFieldValues;
			Assert.IsTrue(updateDict.Any(), "PendingUpdates.Any()");

			var expectedResult = new Dictionary<string, string>
			{
				{ "updateSource", "\"bloomHarvester\"" },   // This key should be present so Parse knows it's not a BloomDesktop upload
				{ "features", "[\"talkingBook\",\"talkingBook:en\"]" },
				{ "brandingProjectName", "\"SIL-LEAD\"" }
			};
			CollectionAssert.AreEquivalent(updateDict, expectedResult);
		}
	}
}
