using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Bloom.WebLibraryIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.WebLibraryIntegration
{
	internal class HarvesterS3Client : BloomS3Client
	{
		public const string HarvesterUnitTestBucketName = "bloomharvest-unittests";
		public const string HarvesterSandboxBucketName = "bloomharvest-sandbox";
		public const string HarvesterProductionBucketName = "bloomharvest";

		public HarvesterS3Client(string bucketName)
			: base(bucketName)
		{
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

		protected override IAmazonS3 CreateAmazonS3Client(string bucketName, AmazonS3Config s3Config)
		{
			return new AmazonS3Client(
				Environment.GetEnvironmentVariable("BloomHarvesterS3Key"),
				Environment.GetEnvironmentVariable("BloomHarvesterS3SecretKey"),
				RegionEndpoint.USEast1);
		}

		public void UploadFile(string filePath)
		{
			using (var fileTransferUtility = new TransferUtility(GetAmazonS3(_bucketName)))
			{
				fileTransferUtility.Upload(filePath, _bucketName);
			}
		}

		public void UploadDirectory(string directoryToUpload, string uploadFolderKey)
		{
			// Delete the directory first in case the directory to upload is a strict subset of the existing contents.
			DeleteBookData(_bucketName, uploadFolderKey);

			using (var fileTransferUtility = new TransferUtility(GetAmazonS3(_bucketName)))
			{
				fileTransferUtility.UploadDirectory(directoryToUpload, $"{_bucketName}/{uploadFolderKey}", "*.*", System.IO.SearchOption.AllDirectories);
			}
		}
	}
}
