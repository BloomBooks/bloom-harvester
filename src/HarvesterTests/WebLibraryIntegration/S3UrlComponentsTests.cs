using BloomHarvester.WebLibraryIntegration;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvesterTests.WebLibraryIntegration
{
	[TestFixture]
	class S3UrlComponentsTests
	{
		[Test]
		public void TestConstruction()
		{
			string url = "https://s3.amazonaws.com/BloomLibraryBooks-Sandbox/hattonlists@gmail.com/b227733c-d902-413c-b12d-59b43e423270/Cricket and Grasshopper/";
			var components = new S3UrlComponents(url);

			Assert.AreEqual("https", components.Protocol, "Protocol should match");
			Assert.AreEqual("s3.amazonaws.com", components.Domain, "Domain should match");
			Assert.AreEqual("BloomLibraryBooks-Sandbox", components.Bucket, "Bucket should match");
			Assert.AreEqual("hattonlists@gmail.com", components.Submitter, "Uploader should match");
			Assert.AreEqual("b227733c-d902-413c-b12d-59b43e423270", components.BookGuid, "GUID should match");
			Assert.AreEqual("Cricket and Grasshopper", components.BookTitle, "Title should match");
		}
	}
}
