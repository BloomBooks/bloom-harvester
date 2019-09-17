using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BloomHarvester.WebLibraryIntegration;
using CommandLine;

[assembly: InternalsVisibleTo("BloomHarvesterTests")]

namespace BloomHarvester
{
	// This class should just get the command line arguments parsed, pass it off to Harvester, then get out of the way as much as possible.
	class Program
	{
		// Command line arguments sample: "harvestAll --environment=dev --parseDBEnvironment=prod"
		//
		// Some presets that you might copy and paste in:
		// harvest --mode=all --environment=dev --parseDBEnvironment=local --suppressLogs --count=2
		// harvest --mode=warnings --environment=dev --parseDBEnvironment=local --suppressLogs
		// harvest --mode=all --environment=dev --parseDBEnvironment=local --suppressLogs "--queryWhere={ \"objectId\":\"38WdeYJ0yF\"}"
		// harvest --mode=all --environment=dev --parseDBEnvironment=dev --suppressLogs "--queryWhere={ \"objectId\":\"JUCL9OMOza\"}"
		// harvest --mode=all --environment=dev --parseDBEnvironment=dev --suppressLogs "--queryWhere={ \"title\":{\"$in\":[\"Vaccinations\",\"Fox and Frog\",\"The Moon and the Cap\"]}}"
		// harvest --mode=all --environment=dev --parseDBEnvironment=dev --suppressLogs "--queryWhere={ \"title\":{\"$regex\":\"^^A\"}}"	// Note that the "^" in the regex apparently needed to be escaped with another "^" before it. Not sure why...
		// harvest --mode=all --environment=dev --parseDBEnvironment=prod --suppressLogs "--queryWhere={ \"title\":{\"$regex\":\"^^A\"},\"tags\":\"bookshelf:Ministerio de Educación de Guatemala\"}"	// Note that the "^" in the regex apparently needed to be escaped with another "^" before it. Not sure why...
		// harvest --mode=default --environment=dev --parseDBEnvironment=dev --suppressLogs "--queryWhere={ \"objectId\":{\"$in\":[\"ze17yO6jIm\",\"v4YABQJLB2\"]}}"
		// harvest --mode=default --environment=dev --parseDBEnvironment=dev --suppressLogs "--queryWhere={ \"uploader\":{\"$in\":[\"SXsqpDHGKk\"]}}"
		//
		// updateState --parseDBEnvironment=dev --id="ze17yO6jIm" --newState="InProgress"
		// Alternatively, you can use Parse API Console.
		//   * Request type: PUT
		//   * Endpoint: classes/books/{OBJECTID}
		//   * Query Parameters: {"updateSource":"bloomHarvester","harvestState":"{NEWSTATE}"}
		[STAThread]
		public static void Main(string[] args)
		{
			// See https://github.com/commandlineparser/commandline for documentation about CommandLine.Parser
			var parser = new CommandLine.Parser((settings) =>
			{
				settings.CaseInsensitiveEnumValues = true;
				settings.CaseSensitive = false;
				settings.HelpWriter = Console.Error;
			});

			try
			{
				parser.ParseArguments<HarvesterOptions, UpdateStateInParseOptions>(args)
					.WithParsed<HarvesterOptions>(options =>
					{
						Harvester.RunHarvest(options);
					})
					.WithParsed<UpdateStateInParseOptions>(options =>
					{
						Harvester.UpdateHarvesterState(options.ParseDBEnvironment, options.ObjectId, options.NewState);
					})
					.WithNotParsed(errors =>
					{
						Console.Out.WriteLine("Error parsing command line arguments.");
						Environment.Exit(1);
					});
			}
			catch (Exception e)
			{
				YouTrackIssueConnector.ReportExceptionToYouTrack(e, "An exception was thrown which was not handled by the program.");
				throw;
			}
		}
	}

	[Verb("harvest", HelpText = "Run Harvester on a set of books")]
	public class HarvesterOptions
	{
		[Option("mode", Required = true, HelpText = "Which mode to run Harvester in, e.g. \"Default\", \"All\", \"NewOrUpdatedOnly\"")]
		public HarvestMode Mode { get; set; }

		[Option('e', "environment", Required = false, Default = EnvironmentSetting.Dev, HelpText = "Sets all environments to read/write from. Valid values are Default, Dev, Test, or Prod. If any individual component's environment are set to non-default, that value will take precedence over this.")]
		public EnvironmentSetting Environment { get; set; }

		[Option("parseDBEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from Parse DB. Valid values are Default, Dev, Test, or Prod. If specified (to non-Default), takes precedence over the general 'environment' option.")]
		public EnvironmentSetting ParseDBEnvironment { get; set; }

		[Option("logEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from the logging resource. Valid values are Default, Dev, Test, or Prod. If specified (to non-Default), takes precedence over the general 'environment' option.")]
		public EnvironmentSetting LogEnvironment { get; set; }

		// ENHANCE: Perhaps this parameter should also suppress creating YouTrack issues?
		[Option("suppressLogs", Required = false, Default = false, HelpText = "If true, will prevent log messages from being logged to the log environment (which may incur fees). Will write those logs to Standard Error instead.")]
		public bool SuppressLogs { get; set; }

		[Option("queryWhere", Required = false, Default = "", HelpText = "If specified, adds a WHERE clause to the request query when retrieving the list of books to process. This should be in the JSON format used by Parse REST API to pass WHERE clauses. See https://docs.parseplatform.org/rest/guide/#query-constraints")]
		public string QueryWhere { get; set; }

		[Option("readOnly", Required = false, Default = false, HelpText = "If specified, harvester just downloads the books")]
		public bool ReadOnly { get; set; }

		[Option("count", Required = false, Default = -1, HelpText = "The amount of records to process. Default -1. If specified to a positive value, then processing will end after processing the specified number of books.")]
		public int Count { get; set; }

		public virtual string GetPrettyPrint()
		{
			return $"mode: {Mode}\n" +
				$"environment: {Environment}\n" +
				$"parseDBEnvironment: {ParseDBEnvironment}\n" +
				$"logEnvironment: {LogEnvironment}\n" +
				$"suppressLogs: {SuppressLogs}\n" +
				$"queryWhere: {QueryWhere}\n" +
				$"count: {Count}"; ;
		}
	}

	[Verb("updateState", HelpText ="Updates the harvestState field in Parse")]
	public class UpdateStateInParseOptions
	{
		[Option("parseDBEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from Parse DB. Valid values are Default, Dev, Test, or Prod. If specified (to non-Default), takes precedence over the general 'environment' option.")]
		public EnvironmentSetting ParseDBEnvironment { get; set; }

		[Option("id", Required = true, HelpText = "The objectId of the item to update.")]
		public string ObjectId { get; set; }

		[Option("newState", Required = true, HelpText = "The new state to set it to")]
		public Parse.Model.HarvestState NewState { get; set; }
	}
}
