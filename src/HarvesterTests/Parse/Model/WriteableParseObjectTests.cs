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
		public void WriteableParseObject_GetPendingUpdates_ModifyAProperty_JsonUpdated()
		{
			var book = new Book();
			book.HarvestState = HarvestState.New.ToString();    // Will be modified
			book.HarvesterMajorVersion = 2;                     // Will stay the same

			book.MarkAsDatabaseVersion();

			// System under test
			book.HarvestState = HarvestState.InProgress.ToString();
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			string resultJson = pendingUpdatesResult.ToJson();
			Assert.AreEqual("{\"harvestState\":\"InProgress\",\"updateSource\":\"bloomHarvester\"}", resultJson);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ModifyAWriteableField_JsonUpdated()
		{
			var book = new CustomTestBook();
			book._myField1 = "My Test Book";    // This will stay the same
			book._myField2 = true;              // This will be modified

			book.MarkAsDatabaseVersion();

			// System under test
			book._myField2 = false;
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			string resultJson = pendingUpdatesResult.ToJson();
			Assert.AreEqual("{\"_myField2\":false,\"updateSource\":\"bloomHarvester\"}", resultJson);
		}

		[Test]
		public void WriteableParseObject_GetPendingUpdates_ModifyANonwriteableField_NothingHappens()
		{
			var book = new Book();
			book.BaseUrl = "www.s3.com/blahblah";
			book.MarkAsDatabaseVersion();

			// System under test
			book.BaseUrl = "www.wrongwebstie.com";
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			string resultJson = pendingUpdatesResult.ToJson();
			Assert.IsFalse(pendingUpdatesResult._updatedFieldValues.Any());
		}
		/// <summary>
		/// Regression test to check that Date deserialization/equality is working well
		/// </summary>
		[Test]
		public void WriteableParseObject_GetPendingUpdates_NonNullHarvestStartedAt_NoPendingUpdates()
		{
			var book = new Book();
			book.HarvestStartedAt = new Date(new DateTime(2020, 03, 04));

			book.MarkAsDatabaseVersion();

			// System under test
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			string resultJson = pendingUpdatesResult.ToJson();
			Assert.IsFalse(resultJson.Contains("harvestStartedAt"));
		}

		/// <summary>
		/// Show field is trickier than others because it's a dynamic object
		/// </summary>
		[Test]
		public void WriteableParseObject_GetPendingUpdates_NonNullShow_NoPendingUpdates()
		{
			var book = new Book();
			var jsonString = $"{{ \"pdf\": {{ \"harvester\": true }} }}";
			book.Show = JsonConvert.DeserializeObject(jsonString);

			book.MarkAsDatabaseVersion();

			// System under test
			var pendingUpdatesResult = book.GetPendingUpdates();

			// Verification
			string resultJson = pendingUpdatesResult.ToJson();

			// Show was not modified in this test. It shouldn't appear as an update.
			Assert.IsFalse(resultJson.Contains("Show"), "Show");
			Assert.IsFalse(resultJson.Contains("show"), "show");
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
			Assert.AreEqual(expectedResult, result);
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
			Assert.AreEqual(true, result, "Array");

			// Repeat for lists
			var list1 = new List<string>(array1);
			result = WriteableParseObject.AreObjectsEqual(list1, list2);
			Assert.AreEqual(true, result, "List");
		}

		[TestCase(new string[] { "a", "b" }, new string[] { "a", "b", "c" }, TestName = "AreObjectsEqual_ArraysDifferentLengths_ReturnsFalse")]
		[TestCase(new string[] { "a", "b" }, new string[] { "a", "b", "c" }, TestName = "AreObjectsEqual_ArraysSameLengthDifferentValue_ReturnsFalse")]
		[TestCase(null, new string[] { }, TestName = "AreObjectsEqual_Arrays1stIsNull_ReturnsFalse")]
		[TestCase(new string[] { }, null, TestName = "AreObjectsEqual_Arrays2ndIsNull_ReturnsFalse")]
		public void WriteableParseObject_AreObjectsEqual_DifferentArrays_ReturnsFalse(object[] array1, object[] array2)
		{
			bool result = WriteableParseObject.AreObjectsEqual(array1, array2);
			Assert.AreEqual(false, result, "Array");

			// Repeat for lists
			var list1 = array1 != null ? new List<object>(array1) : null;
			var list2 = array2 != null ? new List<object>(array2) : null;
			result = WriteableParseObject.AreObjectsEqual(list1, list2);
			Assert.AreEqual(false, result, "List");
		}
	}

	/// <summary>
	///  This class adds a few more writeable field to the book, to allow us to test some cases
	/// </summary>
	class CustomTestBook : Book
	{
		[JsonProperty]
		public string _myField1;

		[JsonProperty]
		public bool _myField2;

		public override WriteableParseObject Clone()
		{
			string jsonOfThis = JsonConvert.SerializeObject(this);
			var newBook = JsonConvert.DeserializeObject<CustomTestBook>(jsonOfThis);
			return newBook;
		}

		protected override HashSet<string> GetWriteableMembers()
		{
			var writeableMembers = base.GetWriteableMembers();
			writeableMembers.Add("_myField1");
			writeableMembers.Add("_myField2");
			return writeableMembers;
		}
	}
}
