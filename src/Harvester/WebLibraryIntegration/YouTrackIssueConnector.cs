using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using YouTrackSharp.Infrastructure;
using YouTrackSharp.Issues;


namespace BloomHarvester.WebLibraryIntegration   // Review: Could posisibly put in Bloom.web or Bloom.Communication instead?
{
	internal class YouTrackIssueConnector
	{
		private static readonly string _issueTrackingBackend = "issues.bloomlibrary.org";
		private static readonly string _youTrackProjectKeyErrors = "BH";  // Or "SB" for Sandbox
		private static readonly string _youTrackProjectKeyMissingFonts = "BH";  // Or "SB" for Sandbox

		private static void ReportToYouTrack(string projectKey, string summary, string description, string consoleMessage, bool exitImmediately)
		{
#if DEBUG
			Console.Out.WriteLine("Issue caught but skipping creating YouTrack issue because running in DEBUG mode. " + consoleMessage);
#else
			Console.Out.WriteLine(consoleMessage);
			string youTrackIssueId = SubmitToYouTrack(summary, description, projectKey);
			if (!String.IsNullOrEmpty(youTrackIssueId))
			{
				Console.Out.WriteLine($"Created YouTrack issue {youTrackIssueId}");
			}
#endif

			if (exitImmediately)
			{
				// Exit immediately can avoid a couple awkward situations
				// If you don't exit the program, the immediate caller (which first caught the exception) could...
				// 1) re-throw the exception as is.  Then an even earlier caller might catch the re-thrown exception and call this again, writing the same issue multiple times.
				// 2) The caller might not attempt to throw or otherwise cause the premature termination of the program. If you run on all books, then you could have thousands of issues per run in the issue tracker.
				Environment.Exit(2);
			}
		}

		private static string SubmitToYouTrack(string summary, string description, string youTrackProjectKey)
		{
			AlertManager.Instance.TryReportAlert(out bool isSilenced);
			if (isSilenced)
			{
				// Alerts are silenced because too many alerts.
				// Skip creating the YouTrack issue for this.
				return "";
			}

			Connection youTrackConnection = new Connection(_issueTrackingBackend, 0, true, "youtrack");
			youTrackConnection.Authenticate("auto_report_creator", "thisIsInOpenSourceCode");
			var issueManagement = new IssueManagement(youTrackConnection);
			dynamic youTrackIssue = new Issue();
			youTrackIssue.ProjectShortName = youTrackProjectKey;
			youTrackIssue.Type = "Awaiting Classification";
			youTrackIssue.Summary = summary;
			youTrackIssue.Description = description;
			string youTrackIssueId = issueManagement.CreateIssue(youTrackIssue);

			return youTrackIssueId;
		}

		internal static void ReportExceptionToYouTrack(Exception exception, string additionalDescription, EnvironmentSetting environment, bool exitImmediately = true)
		{
			string summary = $"[BH] [{environment}] Exception \"{exception.Message}\"";
			string description = GetIssueDescriptionFromException(exception, additionalDescription, environment);
			string consoleMessage = "Exception was:\n" + exception.ToString();

			ReportToYouTrack(_youTrackProjectKeyErrors, summary, description, consoleMessage, exitImmediately);
		}

		private static string GetIssueDescriptionFromException(Exception exception, string additionalDescription, EnvironmentSetting environment)
		{
			StringBuilder bldr = new StringBuilder();
			bldr.AppendLine($"Error Report from Bloom Harvester on {DateTime.UtcNow.ToUniversalTime()} (UTC):");
			bldr.AppendLine($"Environment: {environment}");
			bldr.AppendLine(additionalDescription);
			bldr.AppendLine();

			if (exception != null)
			{
				bldr.AppendLine("# Exception Info");    // # means Level 1 Heading in markdown.
				string exceptionInfo = exception.ToString();
				bldr.AppendLine(exception.ToString());

				string exceptionType = exception.GetType().ToString();
				if (exceptionInfo == null || !exceptionInfo.Contains(exceptionType))
				{
					// Just in case the exception info didn't already include the exception message. (The base class does, but derived classes aren't guaranteed)
					bldr.AppendLine();
					bldr.AppendLine(exceptionType);
				}


				if (exceptionInfo == null || !exceptionInfo.Contains(exception.Message))
				{
					// Just in case the exception info didn't already include the exception message. (The base class does, but derived classes aren't guaranteed)
					bldr.AppendLine();
					bldr.AppendLine("# Exception Message");
					bldr.AppendLine(exception.Message);
				}

				if (exceptionInfo == null || !exceptionInfo.Contains(exception.StackTrace))
				{
					// Just in case the exception info didn't already include the stack trace. (The base class does, but derived classes aren't guaranteed)
					bldr.AppendLine();
					bldr.AppendLine("# Stack Trace");
					bldr.AppendLine(exception.StackTrace);
				}
			}

			return bldr.ToString();
		}

		public static void ReportErrorToYouTrack(string errorSummary, string errorDetails, EnvironmentSetting environment, Parse.Model.Book book = null)
		{
			string summary = $"[BH] [{environment}] Error: {errorSummary}";

			string description = "";
			if (book != null)
			{
				description = $"Book: {book.ObjectId ?? "null"} ({book.BaseUrl ?? "No URL"})\n";
			}
			description += $"Environment: {environment}\n";
			description += errorDetails;

			ReportToYouTrack(_youTrackProjectKeyErrors, summary, description, description, exitImmediately: false);
		}

		public static void ReportMissingFontToYouTrack(string missingFontName, string harvesterId, EnvironmentSetting environment, Parse.Model.Book book = null)
		{
			string summary = $"[BH] [{environment}] Missing Font: \"{missingFontName}\"";

			string description;
			if (book == null)
			{
				description = $"Missing font \"{missingFontName}\" on machine \"{harvesterId}\".";
			}
			else
			{
				description = $"Missing font \"{missingFontName}\" referenced in book {book.ObjectId} ({book.BaseUrl}) on machine \"{harvesterId}\".";
			}
			description += $"Environment: {environment}\n";

			ReportToYouTrack(_youTrackProjectKeyMissingFonts, summary, description, description, exitImmediately: false);
		}
	}
}
