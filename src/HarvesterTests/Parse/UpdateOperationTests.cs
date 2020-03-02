using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BloomHarvester.Parse;
using NUnit.Framework;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BloomHarvesterTests.Parse
{
	class UpdateOperationTests
	{
		[Test]
		public void UpdateOperation_AddsString_DeserializesCorrectly()
		{
			// Setup
			var updateOp = new UpdateOperation();
			updateOp.UpdateFieldWithString("field1", "str1");

			// System under test
			string resultJson = updateOp.ToJson();

			// Verify
			string expectedJson = "{\"field1\":\"str1\"}";
			Assert.AreEqual(expectedJson, resultJson);
		}

		[Test]
		public void UpdateOperation_AddsInts_DeserializesCorrectly()
		{
			// Setup
			var updateOp = new UpdateOperation();
			updateOp.UpdateFieldWithNumber("field1", "19");

			// System under test
			string resultJson = updateOp.ToJson();

			// Verify
			string expectedJson = "{\"field1\":19}";
			Assert.AreEqual(expectedJson, resultJson);
		}

		[Test]
		public void UpdateOperationDeferredUpdate_NonDuplicateKey_AddedWithoutDisturbingPrevious()
		{
			// Setup
			var updateOp = new UpdateOperation();
			updateOp.UpdateFieldWithNumber("field1", 1);

			// System under test
			var obj2 = new string[] { "Hello world" };
			updateOp.DeferUpdateOfFieldWithObject("field2", obj2);

			// Verify
			var updateOpExaminer = new VSUnitTesting.PrivateObject(updateOp);
			var result1 = (Dictionary<string, string>)updateOpExaminer.GetFieldOrProperty("_updatedFieldValues");
			var result2 = (Dictionary<string, object>)updateOpExaminer.GetFieldOrProperty("_deferredUpdatedFieldObjects");

			var expected1 = new Dictionary<string, string>();
			expected1.Add("field1", "1");
			CollectionAssert.AreEquivalent(expected1, result1, "_updatedFieldValues");

			var expected2 = new Dictionary<string, object>();
			expected2.Add("field2", obj2);
			CollectionAssert.AreEquivalent(expected2, result2, "_deferredUpdatedFieldObjects");
		}

		[Test]
		public void UpdateOperationDeferredUpdate_DuplicateKey_ClearsPrevious()
		{
			// Setup
			var updateOp = new UpdateOperation();
			var obj1a = new string[] { "Hello world" };
			updateOp.UpdateFieldWithJson("field1", BloomHarvester.Parse.Model.Book.ToJson(obj1a));
			updateOp.UpdateFieldWithNumber("field2", 2);

			// System under test
			var obj1b = new string[] { "Goodbye world" };
			updateOp.DeferUpdateOfFieldWithObject("field1", obj1b);

			// Verify
			var updateOpExaminer = new VSUnitTesting.PrivateObject(updateOp);
			var result1 = (Dictionary<string, object>)updateOpExaminer.GetFieldOrProperty("_deferredUpdatedFieldObjects");
			var result2 = (Dictionary<string, string>)updateOpExaminer.GetFieldOrProperty("_updatedFieldValues");

			// Easier part of the test
			var expected1 = new Dictionary<string, object>();
			expected1.Add("field1", obj1b);
			CollectionAssert.AreEquivalent(expected1, result1);

			// Harder part of the test
			var expected2 = new Dictionary<string, string>();
			expected2.Add("field2", "2");
			CollectionAssert.AreEquivalent(expected2, result2, "_updatedFieldValues should no longer have field1 (overwritten when calling DeferUpdateOfFieldWithobject");
		}
	}
}
