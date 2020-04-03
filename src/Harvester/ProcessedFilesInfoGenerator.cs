using BloomHarvester.Parse.Model;
using BloomHarvester.WebLibraryIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BloomHarvester
{
	class ProcessedFilesInfoGenerator : Harvester
	{
		private string OutputPath { get; set; }
		private ProcessedFilesInfoGenerator(GenerateProcessedFilesTSVOptions options)
			:base(new HarvesterOptions() {
				QueryWhere = options.QueryWhere,
				ParseDBEnvironment = options.ParseDBEnvironment,
				Environment = options.Environment,
				SuppressLogs = true
			})
		{
			OutputPath = options.OutputPath;
		}

		internal static void GenerateTSV(GenerateProcessedFilesTSVOptions options)
		{
			using (ProcessedFilesInfoGenerator generator = new ProcessedFilesInfoGenerator(options))
			{
				generator.GenerateProcessedFilesTSV();
			}
		}

		private void GenerateProcessedFilesTSV()
		{
			string bloomLibrarySite = "bloomlibrary.org";
			if (_options.ParseDBEnvironment == EnvironmentSetting.Dev)
			{
				bloomLibrarySite = "dev." + bloomLibrarySite;
			}

			string[] fieldsToDereference = new string[] { "langPointers", "uploader" };
			IEnumerable<BookModel> bookList = this.ParseClient.GetBooks(out bool didExitPrematurely, _options.QueryWhere, fieldsToDereference);

			if (didExitPrematurely)
			{
				_logger.LogError("GetBooks() encountered an error and did not return all results. Aborting.");
				return;
			}

			using (StreamWriter sw = new StreamWriter(this.OutputPath))
			{
				foreach (var book in bookList)
				{
					if (book.IsInCirculation == false)
					{
						continue;
					}

					Console.Out.WriteLine(book.ObjectId);
					string folder = HarvesterS3Client.RemoveSiteAndBucketFromUrl(book.BaseUrl);

					string langCode = "";
					string langName = "";
					if (book.Languages != null && book.Languages.Length > 0)
					{
						var language = book.Languages.First();
						langCode = language.IsoCode;
						langName = language.Name;
					}

					string bloomLibraryUrl = $"https://{bloomLibrarySite}/browse/detail/{book.ObjectId}";

					string pdfUrl = _bloomS3Client.GetFileWithExtension(folder, "pdf", book.Title);
					pdfUrl = $"{HarvesterS3Client.GetBloomS3UrlPrefix()}{_downloadBucketName}/{pdfUrl}";

					string ePubUrl = _s3UploadClient.GetFileWithExtension(folder, "epub", book.Title);
					if (!String.IsNullOrEmpty(ePubUrl))
					{
						ePubUrl = $"{HarvesterS3Client.GetBloomS3UrlPrefix()}{_uploadBucketName}/{ePubUrl}";
					}
					else
					{
						ePubUrl = "";
					}

					sw.WriteLine($"{book.Title}\t{langCode}\t{langName}\t{bloomLibraryUrl}\t{pdfUrl}\t{ePubUrl}");
				}
			}
		}

	}
}
