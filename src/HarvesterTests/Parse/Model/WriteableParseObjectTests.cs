using BloomHarvester.Parse.Model;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloomHarvesterTests.Parse.Model
{
	class WriteableParseObjectTests
	{
		[Test]
		public void WriteableParseObject_GetPendingUpdates_ModifyAWriteableProperty_ItemAdded()
		{
			var row = new CustomParseClass();
			row.MyWriteableProperty1 = true;	// This will stay the same
			row.MyWriteableProperty2 = true;	// This will be modified

			row.MarkAsDatabaseVersion();

			// System under test
			row.MyWriteableProperty2 = false;
			var pendingUpdatesResult = row.GetPendingUpdates();

			// Verification
			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("MyWriteableProperty2", "false");
			CollectionAssert.AreEquivalent(expectedResult, pendingUpdatesResult._updatedFieldValues);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ModifyANonWriteableProperty_NothingAdded()
		{
			var row = new CustomParseClass();
			row.MyNonWriteableProperty1 = true;

			row.MarkAsDatabaseVersion();

			// System under test
			row.MyWriteableProperty2 = false;
			var pendingUpdatesResult = row.GetPendingUpdates();

			// Verification
			Assert.That(pendingUpdatesResult._updatedFieldValues.Any(), Is.False);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ModifyAWriteableField_ItemAdded()
		{
			var row = new CustomParseClass();
			row._myWriteableField1 = "My Test Book";    // This will stay the same
			row._myWriteableField2 = true;              // This will be modified

			row.MarkAsDatabaseVersion();

			// System under test
			row._myWriteableField2 = false;
			var pendingUpdatesResult = row.GetPendingUpdates();

			// Verification
			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("_myWriteableField2", "false");
			CollectionAssert.AreEquivalent(expectedResult, pendingUpdatesResult._updatedFieldValues);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ModifyANonWriteableField_NothingAdded()
		{
			// Non-writeable means that it cannot update (write) the value in the database
			// It is hypothetically possible to update the in-memory version, although not necessarily useful...
			// This is just a test case though to make sure it's ok if that scenario did happen
			var row = new CustomParseClass();
			row._myNonWriteableField = true;
			row.MarkAsDatabaseVersion();

			// System under test
			row._myNonWriteableField = false;
			var pendingUpdatesResult = row.GetPendingUpdates();

			// Verification
			Assert.That(pendingUpdatesResult._updatedFieldValues.Any(), Is.False);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ArrayModified_ItemAdded()
		{
			var book = new Book();
			book.Features = new string[] { "talkingBook" };
			book.MarkAsDatabaseVersion();

			book.Features = new string[] { "talkingBook", "talkingBook:en" };
			var pendingUpdates = book.GetPendingUpdates();

			Assert.That(pendingUpdates._updatedFieldValues.Any(kvp => kvp.Key == "features"), Is.True);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ArrayNotModified_NothingAdded()
		{
			var book = new Book();
			book.Features = new string[] { "talkingBook" };
			book.MarkAsDatabaseVersion();

			var pendingUpdates = book.GetPendingUpdates();

			Assert.That(pendingUpdates._updatedFieldValues.Any(), Is.False);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ListModified_ItemAdded()
		{
			var book = new Book();
			book.HarvestLogEntries = new List<string>(new string[] { "message1" });
			book.MarkAsDatabaseVersion();

			book.HarvestLogEntries.Add("message2");
			var pendingUpdates = book.GetPendingUpdates();

			Assert.That(pendingUpdates._updatedFieldValues.Any(kvp => kvp.Key == "harvestLog"), Is.True);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ListNotModified_NothingAdded()
		{
			var book = new Book();
			book.HarvestLogEntries = new List<string>(new string[] { "message1" });
			book.MarkAsDatabaseVersion();

			var pendingUpdates = book.GetPendingUpdates();

			Assert.That(pendingUpdates._updatedFieldValues.Any(), Is.False);
		}

		/// <summary>
		/// Regression test to check that Date deserialization/equality is working well
		/// </summary>
		[Test]
		public void WriteableParseObject_GetPendingUpdates_NonNullDate_NothingAdded()
		{
			var book = new Book();
			book.HarvestStartedAt = new ParseDate(new DateTime(2020, 03, 04));

			book.MarkAsDatabaseVersion();

			// System under test
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			Assert.That(pendingUpdatesResult._updatedFieldValues.Any(), Is.False);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_DateModified_ItemAdded()
		{
			var book = new Book();
			book.HarvestStartedAt = new ParseDate(new DateTime(2020, 03, 04));

			book.MarkAsDatabaseVersion();

			book.HarvestStartedAt = new ParseDate(DateTime.Now);

			// System under test
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			Assert.That(pendingUpdatesResult._updatedFieldValues.Any(kvp => kvp.Key == "harvestStartedAt"), Is.True);
		}

		/// <summary>
		/// Test dynamic objects - they're a lot trickier than statically typed ones
		/// </summary>
		[Test]
		public void WriteableParseObject_GetPendingUpdates_DynamicModified_ItemAdded()
		{
			var book = new Book();
			book.Show = JsonConvert.DeserializeObject("{ \"pdf\": { \"harvester\": true} }");
			book.MarkAsDatabaseVersion();

			book.Show.pdf.harvester = false;
			var pendingUpdates = book.GetPendingUpdates();

			Assert.That(pendingUpdates._updatedFieldValues.Any(kvp => kvp.Key == "show"), Is.True);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_DynamicNotModified_NothingAdded()
		{
			var book = new Book();
			book.Show = JsonConvert.DeserializeObject("{ \"pdf\": { \"harvester\": true} }");
			book.MarkAsDatabaseVersion();

			var pendingUpdates = book.GetPendingUpdates();

			Assert.That(pendingUpdates._updatedFieldValues.Any(), Is.False);
		}

		[TestCase("a", "a", true)]
		[TestCase("a", "b", false)]
		[TestCase(1, 5 / 5, true)]  // True - evaluate to same
		[TestCase(1, 2, false)]
		[TestCase(1, 1.0f, false)]  // False - different types
		[TestCase("1", 1, false)]   // False - different types
		public void WriteableParseObject_AreObjectsEqual_ScalarInput_ReturnsCorrectResult(object obj1, object obj2, bool expectedResult)
		{
			bool result = WriteableParseObject.AreObjectsEqual(obj1, obj2);
			Assert.That(result, Is.EqualTo(expectedResult));
		}

		[Test]
		public void WriteableParseObject_AreObjectsEqual_ArraysSameValuesButDifferentInstances_ReturnsTrue()
		{
			var array1 = new string[] { "a", "b", "c" };

			var list2 = new List<string>();
			list2.Add("a");
			list2.Add("b");
			list2.Add("c");
			var array2 = list2.ToArray();

			// Test arrays
			bool result = WriteableParseObject.AreObjectsEqual(array1, array2);
			Assert.That(result, Is.True, "Array");

			// Repeat for lists
			var list1 = new List<string>(array1);
			result = WriteableParseObject.AreObjectsEqual(list1, list2);
			Assert.That(result, Is.True, "List");
		}

		[TestCase(new string[] { "a", "b" }, new string[] { "a", "b", "c" }, TestName = "AreObjectsEqual_ArraysDifferentLengths_ReturnsFalse")]
		[TestCase(new string[] { "a", "b" }, new string[] { "a", "c" }, TestName = "AreObjectsEqual_ArraysSameLengthDifferentValue_ReturnsFalse")]
		[TestCase(null, new string[] { }, TestName = "AreObjectsEqual_Arrays1stIsNull_ReturnsFalse")]
		[TestCase(new string[] { }, null, TestName = "AreObjectsEqual_Arrays2ndIsNull_ReturnsFalse")]
		public void WriteableParseObject_AreObjectsEqual_DifferentArrays_ReturnsFalse(object[] array1, object[] array2)
		{
			bool result = WriteableParseObject.AreObjectsEqual(array1, array2);
			Assert.That(result, Is.False, "Array");

			// Repeat for lists
			var list1 = array1 != null ? new List<object>(array1) : null;
			var list2 = array2 != null ? new List<object>(array2) : null;
			result = WriteableParseObject.AreObjectsEqual(list1, list2);
			Assert.That(result, Is.False, "List");
		}

		[Test]
		public void WriteableParseObject_AreObjectsEqual_DynamicObjectsEqual_ReturnsTrue()
		{
			dynamic obj1 = JsonConvert.DeserializeObject("{\"epub\":{\"harvester\":false}}");
			dynamic obj2 = JsonConvert.DeserializeObject(" { \"epub\" : { \"harvester\" : false } }");
			bool result = WriteableParseObject.AreObjectsEqual(obj1, obj2);
			Assert.That(result, Is.True);
		}

		[Test]
		public void WriteableParseObject_AreObjectsEqual_DynamicObjectsDifferent_ReturnsFalse()
		{
			dynamic obj1 = JsonConvert.DeserializeObject("{\"epub\":{\"harvester\":false}}");
			dynamic obj2 = JsonConvert.DeserializeObject("{\"epub\":{\"harvester\":true}}");
			bool result = WriteableParseObject.AreObjectsEqual(obj1, obj2);
			Assert.That(result, Is.False);
		}
	}

	/// <summary>
	///  This class adds a few more writeable field to the book, to allow us to test some cases
	/// </summary>
	class CustomParseClass : WriteableParseObject
	{
		[JsonProperty]
		public string _myWriteableField1;

		[JsonProperty]
		public bool _myWriteableField2;

		[JsonProperty]
		public bool _myNonWriteableField;

		[JsonProperty]
		public bool MyWriteableProperty1 { get; set; }

		[JsonProperty]
		public bool MyWriteableProperty2 { get; set; }

		[JsonProperty]
		public bool MyNonWriteableProperty1 { get; set; }

		public override WriteableParseObject Clone()
		{
			string jsonOfThis = JsonConvert.SerializeObject(this);
			var newBook = JsonConvert.DeserializeObject<CustomParseClass>(jsonOfThis);
			return newBook;
		}

		protected override HashSet<string> GetWriteableMembers()
		{
			// Returns the fields/properties which are allowed to write to the database for.
			var writeableMembers = new HashSet<string>();
			writeableMembers.Add("_myWriteableField1");
			writeableMembers.Add("_myWriteableField2");
			writeableMembers.Add("MyWriteableProperty1");
			writeableMembers.Add("MyWriteableProperty2");
			return writeableMembers;
		}

		internal override string GetParseClassName()
		{
			return "customParseClass";
		}
	}
}
