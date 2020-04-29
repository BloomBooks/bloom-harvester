using System;
using System.IO;

namespace BloomHarvester.IO
{
	interface IDriveInfo
	{
		/// <summary>
		/// Indicates the amount of available free space on a drive, in bytes.
		/// </summary>
		long AvailableFreeSpace { get; }
	}

	/// <summary>
	/// A wrapper around System.IO.DriveInfo.
	/// The only purpose of this is to basically have DriveInfo implement an interface,
	/// so that unit tests can change the numbers that DriveInfo methods return so that we can test corner cases
	/// which would otherwise be hard or risky to setup.
	/// (and also, ensure that tests which are testing not-corner-cases are indeed setup into not-corner-cases)
	/// </summary>
	class HarvesterDriveInfo : IDriveInfo
	{
		// FYI, DriveInfo is sealed, so we wrap it instead of subclassing it
		public HarvesterDriveInfo(DriveInfo driveInfo)
		{
			_driveInfo = driveInfo;
		}

		private DriveInfo _driveInfo;

		// The number of bytes available to the current user
		public long AvailableFreeSpace { get => _driveInfo.AvailableFreeSpace; }
	}
}
