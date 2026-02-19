using BloomHarvester.Parse;
using NUnit.Framework;

namespace BloomHarvesterTests.Parse
{
	[TestFixture]
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
		public void UpdateOperation_UpdateFieldWithObject_Int_SameAsUpdateFieldWithNumber()
		{
			int value = 2;

			var updateOp1 = new UpdateOperation();
			updateOp1.UpdateFieldWithNumber("field", value);
			string json1 = updateOp1.ToJson();

			var updateOp2 = new UpdateOperation();
			updateOp2.UpdateFieldWithObject("field", value);
			string json2 = updateOp2.ToJson();

			Assert.AreEqual(json1, json2);
		}

		[Test]
		public void UpdateOperation_UpdateFieldWithObject_Double_SameAsUpdateFieldWithNumber()
		{
			double value = 3.14;

			var updateOp1 = new UpdateOperation();
			updateOp1.UpdateFieldWithNumber("field", value);
			string json1 = updateOp1.ToJson();

			var updateOp2 = new UpdateOperation();
			updateOp2.UpdateFieldWithObject("field", value);
			string json2 = updateOp2.ToJson();

			Assert.AreEqual(json1, json2);
		}


		[TestCase(null)]
		[TestCase("")]
		[TestCase("a")]
		[TestCase("Hello world")]
		public void UpdateOperation_UpdateFieldWithObject_String_SameAsUpdateFieldWithString(string inputValue)
		{
			var updateOp1 = new UpdateOperation();
			updateOp1.UpdateFieldWithString("field", inputValue);
			string json1 = updateOp1.ToJson();

			var updateOp2 = new UpdateOperation();
			updateOp2.UpdateFieldWithObject("field", inputValue);
			string json2 = updateOp2.ToJson();

			Assert.AreEqual(json1, json2);
		}
	}
}
