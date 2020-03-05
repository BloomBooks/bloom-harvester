using System;
using System.Collections.Generic;
using System.Linq;
using BloomHarvester;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BloomHarvesterTests.Parse.Model
{
	[TestFixture]
	public class BookTests
	{
		[Test]
		public void Book_SetHarvesterEvaluation_InsertsProperly()
		{
			// Setup
			var book = new Book()
			{
				HarvestState = "Done",
				HarvestStartedAt = new ParseDate(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
			};

			// System under test
			book.SetHarvesterEvaluation("epub", false);
			book.SetHarvesterEvaluation("pdf", true);
			book.SetHarvesterEvaluation("bloomReader", true);
			book.SetHarvesterEvaluation("readOnline", true);

			//Verify
			string bookJson = JsonConvert.SerializeObject(book);
			string expectedJsonFragment = "\"show\":{\"epub\":{\"harvester\":false},\"pdf\":{\"harvester\":true},\"bloomReader\":{\"harvester\":true},\"readOnline\":{\"harvester\":true}}";
			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}

		[Test]
		public void Book_SetHarvesterEvaluation_UpdatesProperly()
		{
			// Setup
			var book = new Book()
			{
				HarvestState = "Done",
				HarvestStartedAt = new ParseDate(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>(),
				Show = JsonConvert.DeserializeObject("{\"epub\":{\"harvester\":false, \"librarian\":true, \"user\":true},\"pdf\":{\"harvester\":false, \"librarian\":false, \"user\":false},\"bloomReader\":{\"harvester\":false, \"librarian\":false},\"readOnline\":{\"harvester\":false, \"user\":true}}")
			};

			// System under test
			book.SetHarvesterEvaluation("epub", true);
			book.SetHarvesterEvaluation("pdf", true);
			book.SetHarvesterEvaluation("bloomReader", true);
			book.SetHarvesterEvaluation("readOnline", true);

			//Verify
			string bookJson = JsonConvert.SerializeObject(book);
			string expectedJsonFragment = "\"show\":{\"epub\":{\"harvester\":true,\"librarian\":true,\"user\":true},\"pdf\":{\"harvester\":true,\"librarian\":false,\"user\":false},\"bloomReader\":{\"harvester\":true,\"librarian\":false},\"readOnline\":{\"harvester\":true,\"user\":true}}";
			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}

		[Test]
		public void Book_GetBloomLibraryBookDetailLink_Prod_PopulatesLink()
		{
			var book = new Book();
			book.ObjectId = "myObjectId";

			string url = book.GetDetailLink(EnvironmentSetting.Prod);

			Assert.That(url, Is.EqualTo("https://bloomlibrary.org/browse/detail/myObjectId"));
		}

		[Test]
		public void Book_GetBloomLibraryBookDetailLink_Dev_PopulatesLink()
		{
			var book = new Book();
			book.ObjectId = "myObjectId";

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.EqualTo("https://dev.bloomlibrary.org/browse/detail/myObjectId"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		public void Book_GetBloomLibraryBookDetailLink_BadInput_ErrorReported(string badObjectId)
		{
			var book = new Book();
			book.ObjectId = badObjectId;

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.Null);
		}

		[Test]
		public void Book_UpdateMetadata_NotEqual_PendingUpdatesFound()
		{
			// Setup
			var book = new Book();
			book.Features = new string[] { "talkingBook" };
			book.MarkAsDatabaseVersion();

			var metaData = new Bloom.Book.BookMetaData();
			metaData.Features = new string[] { "talkingBook", "talkingBook:en" };

			// Test
			book.UpdateMetadataIfNeeded(metaData);
			UpdateOperation pendingUpdates = book.GetPendingUpdates();

			// Verification
			var updateDict = pendingUpdates._updatedFieldValues;
			Assert.IsTrue(updateDict.Any(), "PendingUpdates.Any()");

			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");   // This key should be present so Parse knows it's not a BloomDesktop upload
			expectedResult.Add("features", "[\"talkingBook\",\"talkingBook:en\"]");
			CollectionAssert.AreEquivalent(updateDict, expectedResult);
		}

		[Test]
		public void Book_GetNewBookUpdateOperation_AddsUpdateSource()
		{
			// System under test
			var bookUpdateOp = Book.GetNewBookUpdateOperation();
			var result = bookUpdateOp._updatedFieldValues;

			// Verification
			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");

			CollectionAssert.AreEquivalent(expectedResult, result);
		}
	}
}
