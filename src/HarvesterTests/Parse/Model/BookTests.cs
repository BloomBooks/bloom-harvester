using System;
using System.Collections.Generic;
using BloomHarvester;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using NUnit.Framework;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BloomHarvesterTests.Parse.Model
{
	[TestFixture]
	public class BookTests
	{
		[Test]
		public void SetHarvesterEvaluation_InsertsProperly()
		{
			// Setup
			var book = new Book()
			{
				BaseUrl = "www.amazon.com",
				HarvestState = "Done",
				HarvestStartedAt = new Date(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
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
		public void SetHarvesterEvaluation_UpdatesProperly()
		{
			// Setup
			var book = new Book()
			{
				BaseUrl = "www.amazon.com",
				HarvestState = "Done",
				HarvestStartedAt = new Date(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
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
		public void GetBloomLibraryBookDetailLink_Prod_PopulatesLink()
		{
			var book = new Book();
			book.ObjectId = "myObjectId";

			string url = book.GetDetailLink(EnvironmentSetting.Prod);

			Assert.That(url, Is.EqualTo("https://bloomlibrary.org/browse/detail/myObjectId"));
		}

		[Test]
		public void GetBloomLibraryBookDetailLink_Dev_PopulatesLink()
		{
			var book = new Book();
			book.ObjectId = "myObjectId";

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.EqualTo("https://dev.bloomlibrary.org/browse/detail/myObjectId"));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		public void GetBloomLibraryBookDetailLink_BadInput_ErrorReported(string badObjectId)
		{
			var book = new Book();
			book.ObjectId = badObjectId;

			string url = book.GetDetailLink(EnvironmentSetting.Dev);

			Assert.That(url, Is.Null);
		}

		[Test]
		public void UpdateMetadata_NotEqual_UpdateOpRegistered()
		{
			// Setup
			var book = new Book();
			book.Features = new string[] { "talkingBook" };
			book.UpdateOp.Clear();

			var metaData = new Bloom.Book.BookMetaData();
			metaData.Features = new string[] { "talkingBook", "talkingBook:en" };

			// Test
			book.UpdateMetadataIfNeeded(metaData);

			// Verification
			var updateOp = new VSUnitTesting.PrivateObject(book.UpdateOp, new VSUnitTesting.PrivateType(typeof(UpdateOperation)));
			var updateDict = (Dictionary<string, string>)updateOp.GetFieldOrProperty("_updatedFieldValues");

			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("features", "[\"talkingBook\", \"talkingBook:en\"]");
			expectedResult.Add("updateSource", "\"bloomHarvester\"");	// This key should be present so Parse knows it's not a BloomDesktop upload
			CollectionAssert.AreEquivalent(updateDict, expectedResult);
		}
	}
}
