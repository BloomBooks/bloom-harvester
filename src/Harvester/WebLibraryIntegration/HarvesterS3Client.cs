using Amazon.S3;
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
		public HarvesterS3Client(string bucketName)
			: base(bucketName)
		{
		}

		protected override IAmazonS3 CreateAmazonS3Client(string bucketName, AmazonS3Config s3Config)
		{
			return new AmazonS3Client(
				Environment.GetEnvironmentVariable("BloomHarvesterS3Key"),
				Environment.GetEnvironmentVariable("BloomHarvesterS3SecretKey"),
				s3Config);
		}
	}
}
