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
			DateTime startDateTime = new DateTime(2019, 9, 11, 0, 0, 0, DateTimeKind.Utc);
			// Setup
			var book = new Book()
			{
				HarvestState = "Done",
				HarvestStartedAt = new ParseDate(startDateTime),
				HarvesterMajorVersion = 1,
				HarvesterMinorVersion = 0,
				HarvestLogEntries = new List<string>()
			};
			string bookJson = JsonConvert.SerializeObject(book);

			var inputList = new List<BatchableOperation>
			{
				new BatchableOperation(RestSharp.Method.POST, "/classes/c1", bookJson)
			};

			// System under test
			string resultJson = ParseClient.GetBatchJson(inputList);

			// Verification
			// Testing the JSON itself is very brittle, because as more fields get added, we would have to keep updating the expected JSON with the default values.
			// So instead, we deserialize the produced JSON and verify that the deserialized object has the correct values for the fields we assigned.
			dynamic result = JsonConvert.DeserializeObject(resultJson);
			//dynamic result = JObject.Parse(resultJson);
			Assert.AreEqual(1, result.requests.Count, "Requests.Count");

			dynamic request = result.requests[0];
			Assert.AreEqual("POST", request.method.Value, "Request Method");
			Assert.AreEqual("/classes/c1", request.path.Value, "Request Path");

			dynamic body = request.body;
			Assert.AreEqual("Done", body.harvestState.Value, "HarvestState");
			Assert.AreEqual(1, body.harvesterMajorVersion.Value, "HarvesterMajorVersion");
			Assert.AreEqual(0, body.harvesterMinorVersion.Value, "HarvesterMinorVersion");
			Assert.AreEqual(0, body.harvestLog.Count, "harvestLog");

			dynamic harvestStartedAt = body.harvestStartedAt;
			Assert.AreEqual("Date", harvestStartedAt.__type.Value, "HarvestStartedAt.__type");
			// Just an FYI, JsonConvert will serialize the DateTime in ISO-8601 format, but that's not the same format that DateTime.ToString() produces by default.
			// So, this code is comparing the two objects directly
			Assert.AreEqual(startDateTime, harvestStartedAt.iso.Value, "HarvestStartedAt.iso (DateTime)");
		}
	}
}
