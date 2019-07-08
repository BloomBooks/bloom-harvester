using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Bloom;
using Bloom.Api;
using Bloom.Book;
using Newtonsoft.Json;

namespace BloomHarvester
{
	/// <summary>
	/// Analyze a book and extract various information the harvester needs
	/// </summary>
	public class BookAnalyzer
	{
		private HtmlDom _dom;
		public BookAnalyzer(string html, string meta)
		{
			_dom = new HtmlDom(XmlHtmlConverter.GetXmlDomFromHtml(html, false));
			Language1Code = _dom.SelectSingleNode(
					"//div[contains(@class, 'bloom-editable') and contains(@class, 'bloom-content1') and @lang]")
				?.Attributes["lang"]?.Value ?? "";
			// Bloom defaults language 2 to en if not specified.
			Language2Code = _dom.SelectSingleNode(
					"//div[contains(@class, 'bloom-editable') and contains(@class, 'bloom-content2') and @lang]")
				?.Attributes["lang"]?.Value ?? "en";
			Language3Code = _dom.SelectSingleNode(
					"//div[contains(@class, 'bloom-editable') and contains(@class, 'bloom-content3') and @lang]")
				?.Attributes["lang"]?.Value ?? "";

			var metaObj = DynamicJson.Parse(meta);
			if (metaObj.IsDefined("brandingProjectName"))
			{
				this.Branding = metaObj.brandingProjectName;

				// This won't generate the current official subscription code for the given branding.
				// (For one thing, we have no way of knowing when it should expire.)
				// Just one that Bloom's CollectionSettings class will accept for at least a day
				// So we can generate the uploads with the correct branding.
				var expiryTimeSpan = DateTime.Now - new DateTime(1899, 12, 30);
				var expiryTimeCode = expiryTimeSpan.Days + 2 - 40000;
				var checkSum = CheckSum(Branding);
				SubscriptionCode = $"{Branding}-{expiryTimeCode,6}-{(Math.Floor(Math.Sqrt(expiryTimeCode)) + checkSum) % 10000,4}";
			}

			var bloomCollectionElement =
				new XElement("Collection",
					new XElement("Language1Iso639Code", new XText(Language1Code)),
					new XElement("Language2Iso639Code", new XText(Language2Code)),
					new XElement("Language3Iso639Code", new XText(Language3Code)),
					new XElement("BrandingProjectName", new XText("SIL-LEAD")),
					new XElement("SubscriptionCode", SubscriptionCode));
			var sb = new StringBuilder();
			using (var writer = XmlWriter.Create(sb))
				bloomCollectionElement.WriteTo(writer);
			BloomCollection = sb.ToString();
		}

		// Duplicated from Bloom's CollectionSettingsApi class (rather than make another PR and another method public)
		// We aren't likely to change this because shipping versions of Bloom use it to validate published codes.
		private static int CheckSum(string input)
		{
			var sum = 0;
			input = input.ToUpperInvariant();
			for (var i = 0; i < input.Length; i++)
			{
				sum += input[i] * i;
			}
			return sum;
		}

		public static BookAnalyzer fromFolder(string bookFolder)
		{
			var filename = Path.GetFileName(bookFolder);
			var bookPath = Path.Combine(bookFolder, filename + ".htm");
			var metaPath = Path.Combine(bookFolder, "meta.json");
			return new BookAnalyzer(File.ReadAllText(bookPath, Encoding.UTF8),
				File.ReadAllText(metaPath, Encoding.UTF8));
		}

		public string WriteBloomCollection(string bookFolder)
		{
			var collectionFolder = Path.GetDirectoryName(bookFolder);
			var result = Path.Combine(collectionFolder, "temp.bloomCollection");
			File.WriteAllText(result, BloomCollection, Encoding.UTF8);
			return result;
		}

		public string Language1Code { get;}
		public string Language2Code { get; }
		public string Language3Code { get; set; }
		public string Branding { get; }
		public string SubscriptionCode { get; }

		/// <summary>
		/// The content appropriate to a skeleton BookCollection file for this book.
		/// </summary>
		public string BloomCollection { get; set; }
	}
}
