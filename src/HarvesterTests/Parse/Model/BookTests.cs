using System;
using System.Collections.Generic;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using NUnit.Framework;

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
			string expectedJson = "{\"harvestState\":\"Done\",\"harvesterMajorVersion\":1,\"harvesterMinorVersion\":0,\"harvestStartedAt\":{\"__type\":\"Date\",\"iso\":\"2019-09-11T00:00:00.000Z\"},\"harvestLog\":[],\"baseUrl\":\"www.amazon.com\",\"show\":{\"epub\":{\"harvester\":false},\"pdf\":{\"harvester\":true},\"bloomReader\":{\"harvester\":true},\"readOnline\":{\"harvester\":true}},\"objectId\":null}";
			Assert.AreEqual(expectedJson, bookJson);
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
			string expectedJson = "{\"harvestState\":\"Done\",\"harvesterMajorVersion\":1,\"harvesterMinorVersion\":0,\"harvestStartedAt\":{\"__type\":\"Date\",\"iso\":\"2019-09-11T00:00:00.000Z\"},\"harvestLog\":[],\"baseUrl\":\"www.amazon.com\",\"show\":{\"epub\":{\"harvester\":true,\"librarian\":true,\"user\":true},\"pdf\":{\"harvester\":true,\"librarian\":false,\"user\":false},\"bloomReader\":{\"harvester\":true,\"librarian\":false},\"readOnline\":{\"harvester\":true,\"user\":true}},\"objectId\":null}";
			Assert.AreEqual(expectedJson, bookJson);
		}
	}
}
