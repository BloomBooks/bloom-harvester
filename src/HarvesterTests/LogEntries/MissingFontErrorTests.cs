using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloomHarvester.LogEntries;
using NUnit.Framework;

namespace BloomHarvesterTests.LogEntry
{
	[TestFixture]
	class MissingFontErrorTests
	{
		[Test]
		public void MissingFontError_BasicInput_CorrectToString()
		{
			var obj = new MissingFontError("MadeUpFontName");
			string output = obj.ToString();
			Assert.AreEqual("Error: Missing font MadeUpFontName", output);
		}
	}
}
