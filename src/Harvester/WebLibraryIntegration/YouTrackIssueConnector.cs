using BloomHarvester.Parse.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using YouTrackSharp;
using YouTrackSharp.Issues;


namespace BloomHarvester.WebLibraryIntegration   // Review: Could posisibly put in Bloom.web or Bloom.Communication instead?
{
	internal interface IIssueReporter
	{
		bool Disabled { get; set; }
		void ReportException(Exception exception, string additionalDescription, BookModel bookModel, EnvironmentSetting environment, bool exitImmediately = true);
		void ReportError(string errorSummary, string errorDescription, string errorDetails, EnvironmentSetting environment, BookModel bookModel = null);
		void ReportMissingFont(string missingFontName, string harvesterId, EnvironmentSetting environment, BookModel bookModel = null);
	}

	internal class YouTrackIssueConnector : IIssueReporter
	{
		private IIssuesService _issuesService;
		private YouTrackIssueConnector()
		{
#if !DEBUG
			// ENHANCE: Maybe creating the issuesService can go in the constructor instead?
			const string TokenPiece1 = @"YXV0b19yZXBvcnRfY3JlYXRvcg==.NzQtMA==.V9k0yNUN7Df5eqo4QEk5N4BBKqmEHV";
			var youTrackConnection = new BearerTokenConnection($"https://{_issueTrackingBackend}/youtrack/", $"perm:{TokenPiece1}");
			_issuesService = youTrackConnection.CreateIssuesService();
#endif
		}

		private static readonly string _issueTrackingBackend = "issues.bloomlibrary.org";
		private static readonly string _youTrackProjectKeyErrors = "BH";  // Or "SB" for Sandbox
		private static readonly string _youTrackProjectKeyMissingFonts = "BH";  // Or "SB" for Sandbox

		public bool Disabled { get; set; } // Should default to Not Disabled

		private static YouTrackIssueConnector _instance;

		// Singleton Instance
		public static YouTrackIssueConnector Instance
		{
			get
			{
				if (_instance == null)
					_instance = new YouTrackIssueConnector();

				return _instance;
			}
		}

		private void ReportToYouTrack(string projectKey, string summary, string description, bool exitImmediately)
		{
			Console.Error.WriteLine("ERROR: " + summary);
			Console.Error.WriteLine("==========================");
			Console.Error.WriteLine(description);
			Console.Error.WriteLine("==========================");
			Console.Error.WriteLine("==========================");
#if DEBUG
			Console.Out.WriteLine("***Issue caught but skipping creating YouTrack issue because running in DEBUG mode.***");
#else
			if (Disabled)
			{
				Console.Out.WriteLine("***Issue caught but skipping creating YouTrack issue because error reporting to YouTrack is disabled.***");
			}
			else
			{
				string youTrackIssueId = SubmitToYouTrack(summary, description, projectKey);
				if (!String.IsNullOrEmpty(youTrackIssueId))
				{
					Console.Out.WriteLine($"Created YouTrack issue {youTrackIssueId}");
				}
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

		private string SubmitToYouTrack(string summary, string description, string youTrackProjectKey)
		{
			bool isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced();
			if (isSilenced)
			{
				// Alerts are silenced because too many alerts.
				// Skip creating the YouTrack issue for this.
				return "";
			}

			var youTrackIssue = new Issue() { Summary = summary, Description = description };
			youTrackIssue.SetField("Type", "Awaiting Classification");

			// ENHANCE: We could async/await this instead, if we wanted to
			string youTrackIssueId = _issuesService.CreateIssue(youTrackProjectKey, youTrackIssue).Result;
			
			return youTrackIssueId;
		}

		public void ReportException(Exception exception, string additionalDescription, BookModel bookModel, EnvironmentSetting environment, bool exitImmediately = true)
		{
			string summary = $"[BH] [{environment}] Exception \"{exception.Message}\"";
			string description =
				additionalDescription + "\n\n" +
				GetDiagnosticInfo(bookModel, environment) + "\n\n" +
				GetIssueDescriptionFromException(exception);

			ReportToYouTrack(_youTrackProjectKeyErrors, summary, description, exitImmediately);
		}

		private static string GetIssueDescriptionFromException(Exception exception)
		{
			StringBuilder bldr = new StringBuilder();
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

		public void ReportError(string errorSummary, string errorDescription, string errorDetails, EnvironmentSetting environment, BookModel bookModel = null)
		{
			string summary = $"[BH] [{environment}] Error: {errorSummary}";

			string description =
				errorDescription + '\n' +
				'\n' +
				GetDiagnosticInfo(bookModel, environment) + '\n' +
				errorDetails;

			ReportToYouTrack(_youTrackProjectKeyErrors, summary, description, exitImmediately: false);
		}

		public void ReportMissingFont(string missingFontName, string harvesterId, EnvironmentSetting environment, BookModel bookModel = null)
		{
			string summary = $"[BH] [{environment}] Missing Font: \"{missingFontName}\"";

			string description = $"Missing font \"{missingFontName}\" on machine \"{harvesterId}\".\n\n";
			description += GetDiagnosticInfo(bookModel, environment);

			ReportToYouTrack(_youTrackProjectKeyMissingFonts, summary, description, exitImmediately: false);
		}

		private static string GetDiagnosticInfo(BookModel bookModel, EnvironmentSetting environment)
		{
			var assemblyVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version ?? new Version(0, 0);

			return
				(bookModel == null ? "" : bookModel.GetBookDiagnosticInfo(environment) + '\n') + 
				$"Environment: {environment}\n" +
				$"Harvester Version: {assemblyVersion.Major}.{assemblyVersion.Minor}\n" +
				$"Time: {DateTime.UtcNow.ToUniversalTime()} (UTC)";
		}
	}
}
