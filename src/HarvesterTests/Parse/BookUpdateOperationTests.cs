using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloomHarvester.Parse;
using NUnit.Framework;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BloomHarvesterTests.Parse
{
	class BookUpdateOperationTests
	{
		/// <summary>
		/// A convenience method to get at a private variable more easily
		/// </summary>
		private static Dictionary<string, string> GetUpdatedFieldValues(BookUpdateOperation bookUpdateOp)
		{
			// Note that the type needs to be of the class that actually defined the field. In this case, that's its base class.
			var updateOpExaminer = new VSUnitTesting.PrivateObject(bookUpdateOp, new VSUnitTesting.PrivateType(typeof(UpdateOperation)));
			return (Dictionary<string, string>)updateOpExaminer.GetFieldOrProperty("_updatedFieldValues");
		}

		[Test]
		public void BookUpdateOperation_Constructor_AddsUpdateSource()
		{
			// System under test
			var bookUpdateOp = new BookUpdateOperation();

			// Verification			
			var result = GetUpdatedFieldValues(bookUpdateOp);

			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");

			CollectionAssert.AreEquivalent(expectedResult, result);
		}

		[Test]
		public void BookUpdateOperation_Clear_UpdateSourceRemains()
		{
			// Setup
			var bookUpdateOp = new BookUpdateOperation();

			// System under test
			bookUpdateOp.Clear();

			// Verification
			var result = GetUpdatedFieldValues(bookUpdateOp);

			var expectedResult = new Dictionary<string, string>();
			expectedResult.Add("updateSource", "\"bloomHarvester\"");

			CollectionAssert.AreEquivalent(expectedResult, result);
		}
	}
}
