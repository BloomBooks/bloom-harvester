using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;

namespace BloomHarvesterTests.Parse
{
	[TestFixture]
	public class ParseClientTests
	{
		[Test]
		public void GetBatchJson_OneItem_ProducesCorrectJson()
		{
			// Setup
			var book = new Book()
			{
				BaseUrl = "www.amazon.com",
				HarvestState = "Done",
				HarvestStartedAt = new Date(new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc)),
				HarvesterVersion = "1.0",
				HarvestLogEntries = new List<string>()
			};
			string bookJson = JsonConvert.SerializeObject(book);

			var inputList = new List<BatchableOperation>
			{
				new BatchableOperation(RestSharp.Method.POST, "/classes/c1", bookJson)
			};

			// System under test
			string resultJson = ParseClient.GetBatchJson(inputList);

			// Verify
			string expectedJson = "{\"requests\": [{\"method\":\"POST\",\"path\":\"/classes/c1\",\"body\":{\"harvestState\":\"Done\",\"harvesterVersion\":\"1.0\",\"harvestStartedAt\":{\"__type\":\"Date\",\"iso\":\"2019-09-11T00:00:00.000Z\"},\"harvestLog\":[],\"baseUrl\":\"www.amazon.com\",\"objectId\":null}}] }";
			Assert.AreEqual(expectedJson, resultJson);
		}
	}
}
