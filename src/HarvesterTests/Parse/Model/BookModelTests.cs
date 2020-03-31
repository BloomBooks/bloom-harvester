using BloomHarvester.Parse.Model;
using NUnit.Framework;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BloomHarvesterTests.Parse.Model
{
	[TestFixture]
	public class BookModelTests
	{
		[TestCase("HarvestState")]
		[TestCase("HarvesterId")]
		[TestCase("HarvesterMajorVersion")]
		[TestCase("HarvesterMinorVersion")]
		[TestCase("HarvestStartedAt")]
		[TestCase("HarvestLogEntries")]
		[TestCase("Features")]
		[TestCase("Tags")]
		[TestCase("Show")]
		public void BookModel_GetWriteableMembers_IncludesExpectedMember(string expectedMemberName)
		{
			var book = new BookModel();
			var invoker = new VSUnitTesting.PrivateObject(book);
			var writeableMembers = invoker.Invoke("GetWriteableMembers") as HashSet<string>;

			Assert.That(writeableMembers.Contains(expectedMemberName), Is.True);
		}
		
		[Test]
		public void BookModel_NonEmptyTag_SerializedInJson()
		{
			var book = new BookModel();
			book.Tags = new string[] { "computedLevel:1" };

			string bookJson = JsonConvert.SerializeObject(book);
			string expectedJsonFragment = "\"tags\":[\"computedLevel:1\"]";

			Assert.That(bookJson.Contains(expectedJsonFragment), Is.True);
		}

		[Test]
		public void BookModel_GetNewBookUpdateOperation_AddsUpdateSource()
		{
			// System under test
			var bookUpdateOp = BookModel.GetNewBookUpdateOperation();
			var result = bookUpdateOp._updatedFieldValues;

			// Verification
			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");

			CollectionAssert.AreEquivalent(expectedResult, result);
		}
	}
}
