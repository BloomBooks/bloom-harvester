﻿using Bloom.FontProcessing;
using BloomHarvester.WebLibraryIntegration;
using System;
using System.Collections.Generic;
using System.IO;

namespace BloomHarvester
{
	public class FontBackup
	{
		private BackupFontsOptions _options;

		public FontBackup(BackupFontsOptions options)
		{
			_options = options;
		}

		public static void BackupFonts(BackupFontsOptions options)
		{
			var fontBackup = new FontBackup(options);
			fontBackup.BackupFonts();
		}

		private void BackupFonts()
		{
			var filesToBackup = new List<string>();
			foreach (var file in Directory.GetFiles("C:\\Windows\\Fonts", "*.ttf", SearchOption.AllDirectories))
			{
				if (IsShareable(file))
					filesToBackup.Add(file);
			}
			foreach (var file in Directory.GetFiles("C:\\Windows\\Fonts", "*.otf", SearchOption.AllDirectories))
			{
				if (IsShareable(file))
					filesToBackup.Add(file);
			}
			// Use the Prod Environment setting because we are using the same credentials for the harvester-font-backup
			// bucket as the production bloomharvest bucket. Note that those two buckets are currently in the "sil-lead"
			// AWS instance while the bloomharvest-sandbox bucket is in the SIL instance.
			using (var s3UploadClient = new HarvesterS3Client("harvester-font-backup", EnvironmentSetting.Prod, false))
			{
				foreach (var file in filesToBackup)
				{
					s3UploadClient.UploadFile(file, "harvester-fonts", "no-cache");
				}
			}
		}

		private bool IsShareable(string filePath)
		{
			var metadata = new FontMetadata(filePath);
			if (_options.Verbose)
				Console.WriteLine($"Checking font file {filePath}: suitability={metadata.determinedSuitability}");
			return metadata.determinedSuitability == FontMetadata.kOK;
		}
	}
}
