using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Bloom.WebLibraryIntegration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.S3.Model;
using System.Web;
using System.Diagnostics;

namespace BloomHarvester.WebLibraryIntegration
{
	interface IS3Client
	{
		void UploadFile(string filePath, string uploadFolderKey, string cacheControl);
		void UploadDirectory(string directoryToUpload, string uploadFolderKey);
		void DeleteDirectory(string folderKey);
		string GetFileWithExtension(string bookFolder, string extension, string idealBaseName = "");
		bool DoesFileExist(string fileName);
	}

	public class HarvesterS3Client : BloomS3Client, IS3Client
	{
		public const string HarvesterUnitTestBucketName = "bloomharvest-unittests";
		public const string HarvesterSandboxBucketName = "bloomharvest-sandbox";
		public const string HarvesterProductionBucketName = "bloomharvest";

		private EnvironmentSetting _s3Environment;
		private bool _forReading;

		public HarvesterS3Client(string bucketName, EnvironmentSetting s3Environment, bool forReading)
			: base(bucketName)
		{
			_s3Environment = s3Environment;
			_forReading = forReading;
		}

		internal static IEnumerable<string> EnumerateAllBloomLibraryBucketNames()
		{
			yield return ProductionBucketName;
			yield return SandboxBucketName;
			yield return UnitTestBucketName;
		}

		internal static IEnumerable<string> EnumerateAllHarvesterBucketNames()
		{
			yield return HarvesterProductionBucketName;
			yield return HarvesterSandboxBucketName;
			yield return HarvesterUnitTestBucketName;
		}

		internal static string GetBloomS3UrlPrefix()
		{
			return "https://s3.amazonaws.com/";
		}

		internal static string RemoveSiteAndBucketFromUrl(string url)
		{
			string decodedUrl = HttpUtility.UrlDecode(url);
			string urlWithoutTitle = Harvester.RemoveBookTitleFromBaseUrl(decodedUrl);			
			var bookOrder = urlWithoutTitle.Substring(GetBloomS3UrlPrefix().Length);
			var index = bookOrder.IndexOf('/');
			var folder = bookOrder.Substring(index + 1);

			return folder;
		}

		protected override IAmazonS3 CreateAmazonS3Client(AmazonS3Config s3Config,
			string bucketName) // was: bucketName, AmazonS3Config s3Config)
		{
			var bucketType = _forReading ? "Books" : "Harvester";
			// The keys aren't very important for reading, since the data we want to read is public,
			// but it seems they do need to belong to the right project. Note that currently both the
			// read buckets are in the same project as the dev harvester bucket, so the same keys
			// work for both dev and prod reading as for dev writing, while we need different ones for prod writing.
			// However, this may not always be the case, so the code is set up for the complete set of
			// four pairs of keys. In particular we may eventually remove public read access from
			// the BloomBooks buckets (Bloom only needs to write to them, only the harvester needs to
			// read), at which point keys giving the harvester read access will become critical.
			// We may be able to consolidate down to one set of keys for the harvester user (which
			// is configured to have appropriate access to all buckets) once we get back to having
			// all the buckets in one account/project (i.e., sil-lead).
			return new AmazonS3Client(
				Environment.GetEnvironmentVariable($"Bloom{bucketType}S3Key{_s3Environment}"),
				Environment.GetEnvironmentVariable($"Bloom{bucketType}S3SecretKey{_s3Environment}"),
				RegionEndpoint.USEast1);
		}

		/// <summary>
		/// Uploads a single file to AWS S3
		/// </summary>
		/// <param name="filePath">full pathname of file to upload</param>
		/// <param name="uploadFolderKey">The key prefix of the S3 object to create (i.e., which subfolder to upload it to)</param>
		/// <param name="cacheControl">optional value for HTTP Cache-Control header.  If not set, S3 does whatever S3 does for cache control.</param>
		public void UploadFile(string filePath, string uploadFolderKey, string cacheControl)
		{
			using (var fileTransferUtility = new TransferUtility(GetAmazonS3WithAccessKey(_bucketName)))
			{
				// This uploads but does not set public read
				// fileTransferUtility.Upload(filePath, $"{_bucketName}/{uploadFolderKey}");
				var request = new TransferUtilityUploadRequest()
				{
					BucketName = _bucketName,
					FilePath = filePath,
					Key = uploadFolderKey + "/" + Path.GetFileName(filePath)
				};
				// The effect of this is that navigating to the file's URL is always treated as an attempt to download the file.
				// At one point we avoided doing this for the PDF (typically a preview) which we want to navigate to in the Preview button
				// of BloomLibrary. But now we do the preview another way and PDFs are also treated as things to download.
				// I'm not sure whether it still matters for any files. This code was copied from Bloom.
				// Comment there says it was temporarily important for the BookOrder file when the Open In Bloom button just downloaded it.
				// However, now the download link uses the bloom: prefix to get the URL passed directly to Bloom,
				// so it may not be needed for anything. Still, at least for the files a browser would not know how to
				// open, it seems desirable to download them rather than try to open them, if such a thing should ever happen.
				// So I'm leaving the code in for now.
				request.Headers.ContentDisposition = "attachment";

				if (!String.IsNullOrEmpty(cacheControl))
					request.Headers.CacheControl = cacheControl;
				// It is possible to also set the filename (after attachment, put ; filename='" + Path.GetFileName(file) + "').
				// Currently the default seems to be to use the file's name from the key, which is fine, so not messing with this.
				// At one point we did try it, and found that AWSSDK can't cope with setting this for files with non-ascii names.
				// It seems that the header we insert here eventually becomes a header for a web request, and these allow only ascii.
				// There may be some way to encode non-ascii filenames to get the effect, if we ever want it again. Or AWS may fix the problem.
				// If you put setting the filename back in without such a workaround, be sure to test with a non-ascii book title.
				if (_bucketName == "harvester-font-backup")
					request.CannedACL = S3CannedACL.BucketOwnerRead;
				else
					request.CannedACL = S3CannedACL.PublicRead; // Allows any browser to download it.

				try
				{
					fileTransferUtility.Upload(request);

				}
				catch
				{
					throw;
				}
			}
		}

		/// <summary>
		/// This method was moved here from BloomDesktop's BloomS3Client class, copying the version from
		/// the 5.6 branch as of 21 March 2024, since it has been removed from that class in Bloom 5.7
		/// but is still needed here.
		/// </summary>
		/// <param name="bucketName"></param>
		/// <param name="key"></param>
		public void DeleteBookData(string bucketName, string key)
		{
			if (BookUpload.IsDryRun)
				return;

			var listMatchingObjectsRequest = new ListObjectsRequest()
			{
				BucketName = bucketName,
				Prefix = key
			};

			ListObjectsResponse matchingFilesResponse;
			do
			{
				// Note: ListObjects can only return 1,000 objects at a time,
				//       and DeleteObjects can only delete 1,000 objects at a time.
				//       So a loop is needed if the book contains 1,001+ objects.
				matchingFilesResponse = GetAmazonS3WithAccessKey(bucketName).ListObjects(listMatchingObjectsRequest);
				if (matchingFilesResponse.S3Objects.Count == 0)
					return;

				var deleteObjectsRequest = new DeleteObjectsRequest()
				{
					BucketName = bucketName,
					Objects = matchingFilesResponse.S3Objects.Select(s3Object => new KeyVersion() { Key = s3Object.Key }).ToList()
				};

				var response = GetAmazonS3WithAccessKey(bucketName).DeleteObjects(deleteObjectsRequest);
				Debug.Assert(response.DeleteErrors.Count == 0);

				// Prep the next request (if needed)
				listMatchingObjectsRequest.Marker = matchingFilesResponse.NextMarker;
			}
			while (matchingFilesResponse.IsTruncated);  // Returns true if haven't reached the end yet
		}

		/// <summary>
		/// Uploads a directory to AWS S3
		/// </summary>
		/// <param name="directoryToUpload">The local directory whose contents should be uploaded to S3</param>
		/// <param name="uploadFolderKey">The key prefix of the S3 objects to create (i.e., which subfolder to upload it to)</param>
		public void UploadDirectory(string directoryToUpload, string uploadFolderKey)
		{
			// Delete the directory first in case the directory to upload is a strict subset of the existing contents.
			DeleteBookData(_bucketName, uploadFolderKey);

			using (var transferUtility = new TransferUtility(GetAmazonS3WithAccessKey(_bucketName)))
			{
				var request = new TransferUtilityUploadDirectoryRequest
				{
					Directory = directoryToUpload,
					BucketName = _bucketName,
					KeyPrefix = uploadFolderKey,
					SearchPattern = "*.*",
					SearchOption = System.IO.SearchOption.AllDirectories,
					CannedACL = S3CannedACL.PublicRead
				};
				// Set cache control for uploaded items.  See https://issues.bloomlibrary.org/youtrack/issue/BL-8198
				// and https://forums.aws.amazon.com/thread.jspa?threadID=144559.
				request.Metadata.Add("Cache-Control", "no-cache");

				// Enhance: Could call the BloomDesktop code directly in future if desired.
				transferUtility.UploadDirectory(request);
			}
		}

		public void DeleteDirectory(string folderKey)
		{
			DeleteBookData(_bucketName, folderKey);
		}

		public string GetFileWithExtension(string bookFolder, string extension, string idealBaseName="")
		{
			var s3 = GetAmazonS3WithAccessKey(_bucketName);
			var request = new ListObjectsV2Request();
			request.BucketName = _bucketName;
			request.Prefix = bookFolder;

			var response = s3.ListObjectsV2(request);

			string idealFileName = $"{idealBaseName}.{extension}".ToLowerInvariant();
			var idealTargets = response.S3Objects.Where(x => x.Key.ToLowerInvariant() == idealFileName);
			if (idealTargets.Any())
			{
				return idealTargets.First().Key;
			}

			foreach (var item in response.S3Objects)
			{
				if (item.Key.ToLowerInvariant().EndsWith($".{extension}".ToLowerInvariant()))
				{
					return item.Key;
				}
			}

			return null;
		}

		public bool DoesFileExist(string fileName)
		{
			var s3 = GetAmazonS3WithAccessKey(_bucketName);
			Amazon.S3.IO.S3FileInfo s3FileInfo = new Amazon.S3.IO.S3FileInfo(s3, _bucketName, fileName);
			return s3FileInfo.Exists;
		}
	}
}
