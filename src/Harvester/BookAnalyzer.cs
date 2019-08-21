using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
			}

			var bloomCollectionElement =
				new XElement("Collection",
					new XElement("Language1Iso639Code", new XText(Language1Code)),
					new XElement("Language2Iso639Code", new XText(Language2Code)),
					new XElement("Language3Iso639Code", new XText(Language3Code)),
					new XElement("BrandingProjectName", new XText(Branding??"")));
			var sb = new StringBuilder();
			using (var writer = XmlWriter.Create(sb))
				bloomCollectionElement.WriteTo(writer);
			BloomCollection = sb.ToString();
		}

		public static BookAnalyzer fromFolder(string bookFolder)
		{
			var filename = Path.GetFileName(bookFolder);
			var bookPath = Bloom.Book.BookStorage.FindBookHtmlInFolder(bookFolder);						 
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
