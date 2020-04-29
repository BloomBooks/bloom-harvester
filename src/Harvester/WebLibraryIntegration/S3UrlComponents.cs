using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.WebLibraryIntegration
{
	class S3UrlComponents
	{
		public string Protocol { get; set; }
		public string Domain { get; set; }
		public string Bucket { get; set; }
		public string Submitter { get; set; }
		public string BookGuid { get; set; }
		public string BookTitle { get; set; }

		public S3UrlComponents(string decodedUrl)
		{
			InitializeFromUrl(decodedUrl, this);
		}

		/// <summary>
		/// Initializes the object based on the specified URL
		/// </summary>
		/// <param name="decodedUrl">A s3 url starting with http/https)</param>
		/// <param name="obj">The object to initialize</param>
		public static void InitializeFromUrl(string decodedUrl, S3UrlComponents obj)
		{
			if (string.IsNullOrEmpty(decodedUrl))
				return;

			string protocolSuffix = "://";
			int protocolSuffixIndex = decodedUrl.IndexOf(protocolSuffix);
			if (protocolSuffixIndex < 0)
				return;

			obj.Protocol = decodedUrl.Substring(0, protocolSuffixIndex);

			int domainStartIndex = protocolSuffixIndex + protocolSuffix.Length;
			string urlWithoutProtocol = decodedUrl.Substring(domainStartIndex);
			string[] fields = urlWithoutProtocol.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

			if (fields.Length > 0)
				obj.Domain = fields[0];

			if (fields.Length > 1)
				obj.Bucket = fields[1];

			if (fields.Length > 2)
				obj.Submitter = fields[2];

			if (fields.Length > 3)
				obj.BookGuid = fields[3];

			if (fields.Length > 4)
				obj.BookTitle = fields[4];

			if (String.IsNullOrEmpty(obj.BookTitle))
			{
				// It's going to cause problems for the Harvester code if the BookTItle is null.
				// We use the BookTitle when creating paths to read/write to the file system.
				// if it's null/empty, the path structure will look different...
				// Theoretically, seems like there's non-zero risk that we could overwrite the parent folder?!
				//
				// Let's just abort now instead.
				throw new ArgumentException("Book has null/empty title");
			}
		}
	}
}
