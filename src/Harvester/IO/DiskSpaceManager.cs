using System;
using BloomHarvester.Logger;
using BloomHarvester.WebLibraryIntegration;

namespace BloomHarvester.IO
{
	interface IDiskSpaceManager
	{
		bool CleanupIfNeeded();
	}

	internal class DiskSpaceManager : IDiskSpaceManager
	{
		public DiskSpaceManager(IDriveInfo driveInfo, IMonitorLogger logger, IIssueReporter issueReporter)
		{
			this.DriveInfo = driveInfo;
			this.Logger = logger;
			this.IssueReporter = issueReporter;
		}

		public IDriveInfo DriveInfo { get; set; }
		public IMonitorLogger Logger { get; set; }
		public IIssueReporter IssueReporter { get; set; }

		public bool CleanupIfNeeded()
		{
			bool isDiskSpaceLow = IsDiskSpaceLow();

			if (isDiskSpaceLow)
				Cleanup();

			return isDiskSpaceLow;
		}

		private bool IsDiskSpaceLow()
		{
			// For now, just say < 1 GB is low
			return this.DriveInfo.AvailableFreeSpace < 1073741824;
		}

		private void Cleanup()
		{
			this.Logger.TrackEvent("Low Disk Space");

			// ENHANCE: In the future, you could delete some old books out of the Harvester directory
			// But let's not bother with any risky stuff like deleting stuff off the hard drive.
			// For now, we'll just yell about the situation
			string description = $"Harvester only has {this.DriveInfo.AvailableFreeSpace} bytes left. Please cleanup some space soon.";
			this.IssueReporter.ReportError("Low Disk Space", description, ""); 
		}
	}
}
