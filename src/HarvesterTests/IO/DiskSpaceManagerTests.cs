using System;
using System.Collections.Generic;
using System.Linq;
using BloomHarvester.IO;
using BloomHarvester.Logger;
using BloomHarvester.WebLibraryIntegration;
using NSubstitute;
using NUnit.Framework;

namespace BloomHarvesterTests.IO
{
	[TestFixture]
	class DiskSpaceManagerTests
	{
		IDriveInfo _fakeDriveInfo;
		IIssueReporter _issueReporter;
		IMonitorLogger _logger;

		[SetUp]
		public void SetupBeforeEachTest()
		{
			_fakeDriveInfo = Substitute.For<IDriveInfo>();
			_issueReporter = Substitute.For<IIssueReporter>();
			_logger = Substitute.For<IMonitorLogger>();
		}

		[Test]
		public void CleanupIfNeeded_DiskSpaceLow_PerformsCleanupAndReportsState()
		{
			_fakeDriveInfo.AvailableFreeSpace.Returns(1000);

			var manager = new DiskSpaceManager(_fakeDriveInfo, _logger, _issueReporter);

			var wasCleaned = manager.CleanupIfNeeded();

			Assert.That(wasCleaned, Is.True);
			_issueReporter.ReceivedWithAnyArgs(1).ReportError(default, default, default, default);
			_logger.Received(1).TrackEvent("Low Disk Space");
		}

		[Test]
		public void CleanupIfNeeded_DiskSpaceSufficient_NoCleanup()
		{
			_fakeDriveInfo.AvailableFreeSpace.Returns(Int32.MaxValue);

			var manager = new DiskSpaceManager(_fakeDriveInfo, _logger, _issueReporter);

			var wasCleaned = manager.CleanupIfNeeded();

			Assert.That(wasCleaned, Is.False);
			_issueReporter.DidNotReceiveWithAnyArgs().ReportError(default, default, default, default);
			_logger.DidNotReceiveWithAnyArgs().TrackEvent(default);
		}
	}
}
