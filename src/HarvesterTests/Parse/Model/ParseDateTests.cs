using System;
using System.Collections.Generic;
using System.Text;
using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BloomHarvesterTests.Parse.Model
{
	[TestFixture]
	public class DateTests
	{
		[Test]
		public void ParseDate_GetJson_UtcInput_CorrectJson()
		{
			var parseDate = new ParseDate(new DateTime(2013, 1, 2, 14, 3, 4, 5, DateTimeKind.Utc));

			string resultJson = JsonConvert.SerializeObject(parseDate);

			Assert.That(resultJson, Is.EqualTo("{\"__type\":\"Date\",\"iso\":\"2013-01-02T14:03:04.005Z\"}"));
		}

		[Test]
		public void ParseDate_UtcTime_JsonInput_UtcNotLocal()
		{
			string json = "{\"__type\":\"Date\",\"iso\":\"2013-01-02T14:03:04.005Z\"}";

			ParseDate parseDate = JsonConvert.DeserializeObject<ParseDate>(json);

			Assert.That(parseDate.UtcTime, Is.EqualTo(new DateTime(2013, 1, 2, 14, 3, 4, 5, DateTimeKind.Utc)));
		}

		[Test]
		public void ParseDate_Equals_SameValues_ReturnsTue()
		{
			var date1 = new ParseDate(new DateTime(2013, 1, 2, 14, 3, 4, 5, DateTimeKind.Utc));
			var date2 = new ParseDate(DateTime.Now);
			date2.UtcTime = new DateTime(2013, 1, 2, 14, 3, 4, 5, DateTimeKind.Utc);

			bool result = date1.Equals(date2);

			Assert.That(result, Is.True);
		}

		[Test]
		public void ParseDate_Equals_DifferentObjects_ReturnsFalse()
		{
			var date1 = new ParseDate(new DateTime(2013, 1, 2, 14, 3, 4, 5, DateTimeKind.Utc));
			// Only off by 1 second, but should return false.
			var date2 = new ParseDate(new DateTime(2013, 1, 2, 14, 3, 4, 6, DateTimeKind.Utc));

			bool result = date1.Equals(date2);

			Assert.That(result, Is.False);
		}
	}
}
