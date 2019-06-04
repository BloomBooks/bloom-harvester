using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BloomHarvester;

namespace BloomHarvesterTests
{
	[TestFixture]
	class HarvesterTests
	{
		[Test]
		public void GetBookIdFromBaseUrl_EncodedWithTitle_DecodedWithoutTitle()
		{
			string input = "https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists%40gmail.com%2f8cba3b47-2ceb-47fd-9ac7-3172824849e4%2fHow+Snakes+Came+to+Be%2f";
			string output = Harvester.GetBookIdFromBaseUrl(input);
			Assert.AreEqual("https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists@gmail.com/8cba3b47-2ceb-47fd-9ac7-3172824849e4", output);
		}
	}
}
