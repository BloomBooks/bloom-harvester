using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YouTrackSharp.Infrastructure;
using YouTrackSharp.Issues;


namespace BloomHarvester.WebLibraryIntegration   // Review: Could posisibly put in Bloom.web or Bloom.Communication instead?
{
	public class YouTrackIssueConnector
	{
		private static readonly string _issueTrackingBackend = "issues.bloomlibrary.org";
		private static readonly string _youTrackProjectKey = "BL";	// Or "SB" for Sandbox
		
		public static void SubmitToYouTrack(Exception exception, string additionalDescription = "", bool exitImmediately = true)
		{
			string summary = $"[BH] Exception \"{exception.Message}\"";
			string description = GetIssueDescription(exception, additionalDescription);

			// Don't create YouTrack issues when running in Debug mode.
#if DEBUG
			Console.Out.WriteLine("Issue caught but skipping creating YouTrack issue because running in DEBUG mode. Exception was:\n" + exception.ToString());
#else
			string youTrackIssueId = SubmitToYouTrack(summary, description);
			Console.Out.WriteLine($"Created YouTrack issue {youTrackIssueId}");
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

		private static string SubmitToYouTrack(string summary, string description)
		{
			Connection youTrackConnection = new Connection(_issueTrackingBackend, 0, true, "youtrack");
			youTrackConnection.Authenticate("auto_report_creator", "thisIsInOpenSourceCode");
			var issueManagement = new IssueManagement(youTrackConnection);
			dynamic youTrackIssue = new Issue();
			youTrackIssue.ProjectShortName = _youTrackProjectKey;
			youTrackIssue.Type = "Awaiting Classification";
			youTrackIssue.Summary = summary;
			youTrackIssue.Description = description;
			string youTrackIssueId = issueManagement.CreateIssue(youTrackIssue);

			return youTrackIssueId;
		}

		public static string GetIssueDescription(Exception exception, string additionalDescription)
		{
			StringBuilder bldr = new StringBuilder();
			bldr.AppendLine($"Error Report from Bloom Harvester on {DateTime.UtcNow.ToUniversalTime()} (UTC):");
			bldr.AppendLine(additionalDescription);
			bldr.AppendLine();

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

			return bldr.ToString();
		}
	}
}
