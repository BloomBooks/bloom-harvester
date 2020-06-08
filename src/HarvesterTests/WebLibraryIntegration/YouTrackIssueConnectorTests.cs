using System;
using BloomHarvester;
using BloomHarvester.Parse.Model;
using BloomHarvester.WebLibraryIntegration;
using NUnit.Framework;


namespace BloomHarvesterTests.WebLibraryIntegration
{
	[TestFixture]
	public class YouTrackIssueConnectorTests
	{
		private YouTrackIssueConnector _connector;

		[OneTimeSetUp]
		public void InitializeYouTrack()
		{
			_connector = YouTrackIssueConnector.GetInstance(EnvironmentSetting.Test, "SB");
		}

		[SetUp]
		public void ResetYouTrackConnector()
		{
			_connector.TestErrorReports.Clear();
		}

		[Test]
		public void TestIssueSubmission()
		{
			var issueId = YouTrackIssueConnector.SubmitToYouTrack("Harvester Test Issue",
				"This is a test from Harvester, which apparently cannot be overemphasized.", "AUT");
			Assert.That(issueId, Is.Not.Null);
			Assert.That(issueId, Does.StartWith("AUT-"));

			// Creating a new submitter seems a bit wasteful, and a bit too far into implementation details,
			// but it's the only way to delete the newly created issue.
			var submitter = new Bloom.YouTrackIssueSubmitter("AUT");
			var deleted = submitter.DeleteIssue(issueId);
			Assert.That(deleted, Is.True);
		}

		// The remaining tests don't touch SubmitToYouTrack.  The Test environment ensures that the final report
		// summary and description are accumulated in local memory instead.

		[Test]
		public void TestReportException()
		{
			var model = CreateTestBookModel();
			var description = "What was I thinking when I hit the Enter key with a hammer?";
			try
			{
				throw new ApplicationException("This is a test.");	// throwing is the only way to get the stack trace afaict
			}
			catch (ApplicationException except)
			{
				// SUT
				_connector.ReportException(except, description, model, false);
			}

			Assert.That(_connector.TestErrorReports.Count, Is.EqualTo(1), "There should be exactly one error report stored.");
			var report = _connector.TestErrorReports[0];
			Assert.That(report.ProjectKey, Is.EqualTo("SB"), "The Sandbox project key is used for the report.");
			Assert.That(report.Summary, Does.Contain("\"This is a test.\""), "The report summary includes the exception message.");
			Assert.That(report.Summary, Does.Contain("\"My Book\""), "The report summary contains the book title.");
			Assert.That(report.Description, Does.StartWith(description), "The report description starts with the user's text");
			Assert.That(report.Description, Does.Contain("BookId: 87654321"), "The report description includes the book's object id.");
			Assert.That(report.Description, Does.Contain("URL: https://test.bloomlibrary.org/browse/detail/87654321"), "The report description includes the book's URL.");
			Assert.That(report.Description, Does.Contain("Title: My Book"), "The report description includes the book title.");
			Assert.That(report.Description, Does.Contain("Environment: Test"), "The report description includes the book's environment.");
			Assert.That(report.Description, Does.Contain("System.ApplicationException: This is a test."), "The report description includes the exception type and message.");
			Assert.That(report.Description, Does.Contain(" at BloomHarvesterTests.WebLibraryIntegration.YouTrackIssueConnectorTests.TestReportException() in "),
				"The report description includes the stack trace.");
		}

		[Test]
		public void TestReportError()
		{
			var model = CreateTestBookModel();
			var summary = "Houston, we have a problem.";
			var description = "Flames are shooting out the back of my computer.";
			var details =
				"Perhaps I shouldn't have been working on implementing the long lost HCF computer op code in my local computer with custom hardware." +
				"  But fireworks displays are always so much fun if they don't burn down the office!";

			// SUT
			_connector.ReportError(summary, description, details, model);

			Assert.That(_connector.TestErrorReports.Count, Is.EqualTo(1), "There should be exactly one error report stored.");
			var report = _connector.TestErrorReports[0];
			Assert.That(report.ProjectKey, Is.EqualTo("SB"), "The Sandbox project key is used for the report.");
			Assert.That(report.Summary, Does.Contain(summary), "The report summary includes the provided summary.");
			Assert.That(report.Summary, Does.Contain("\"My Book\""), "The report summary contains the book title.");
			Assert.That(report.Description, Does.Contain("BookId: 87654321"), "The report description includes the book's object id.");
			Assert.That(report.Description, Does.Contain("URL: https://test.bloomlibrary.org/browse/detail/87654321"), "The report description includes the book's URL.");
			Assert.That(report.Description, Does.Contain("Title: My Book"), "The report description includes the book title.");
			Assert.That(report.Description, Does.Contain("Environment: Test"), "The report description includes the book's environment.");
			Assert.That(report.Description, Does.StartWith(description), "The report description starts with the provided description.");
			Assert.That(report.Description, Does.Contain(details), "The report description includes the provided details.");
		}

		[Test]
		public void TestReportMissingFont()
		{
			//ReportMissingFont(string missingFontName, string harvesterId, BookModel bookModel = null)
			var model = CreateTestBookModel();
			var harvesterId = "ComputerName";
			var missingFontName = "My Favorite Font";

			// SUT
			_connector.ReportMissingFont(missingFontName, harvesterId, model);

			Assert.That(_connector.TestErrorReports.Count, Is.EqualTo(1), "There should be exactly one error report stored.");
			var report = _connector.TestErrorReports[0];
			Assert.That(report.ProjectKey, Is.EqualTo("SB"), "The Sandbox project key is used for the report.");
			Assert.That(report.Summary, Does.Contain(missingFontName), "The report summary includes the provided summary.");
			Assert.That(report.Summary, Does.Contain("\"My Book\""), "The report summary contains the book title.");
			Assert.That(report.Description, Does.Contain("BookId: 87654321"), "The report description includes the book's object id.");
			Assert.That(report.Description, Does.Contain("URL: https://test.bloomlibrary.org/browse/detail/87654321"), "The report description includes the book's URL.");
			Assert.That(report.Description, Does.Contain("Title: My Book"), "The report description includes the book title.");
			Assert.That(report.Description, Does.Contain("Environment: Test"), "The report description includes the book's environment.");
			Assert.That(report.Description, Does.StartWith($"Missing font \"{missingFontName}\" on machine \"{harvesterId}\""), "The report description starts with missing font basics");
		}

		private BookModel CreateTestBookModel()
		{
			var model = new BookModel("https://phony.url/bookTitle", "My Book")
			{
				ObjectId = "87654321"
			};
			return model;
		}
	}
}
