using Bloom;
using Bloom.WebLibraryIntegration;

namespace BloomHarvester.WebLibraryIntegration
{
	internal interface IBookTransfer
	{
		string HandleDownloadWithoutProgress(string url, string destRoot);
	}

	/// <summary>
	/// This class is nearly exactly the same as Bloom's version of BookTransfer,
	/// except we mark that it implements the IBookTransfer interace (to make our unit testing life easier)
	/// </summary>
	class HarvesterBookTransfer : BookTransfer, IBookTransfer
	{
		internal HarvesterBookTransfer(BloomParseClient parseClient, BloomS3Client bloomS3Client, BookThumbNailer htmlThumbnailer)
			: base(parseClient, bloomS3Client, htmlThumbnailer, new Bloom.BookDownloadStartingEvent())
		{
		}

		public new string HandleDownloadWithoutProgress(string url, string destRoot)
		{
			// Just need to declare this as public instead of internal (interfaces...)
			return base.HandleDownloadWithoutProgress(url, destRoot);
		}
	}
}
