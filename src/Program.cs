using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BloomHarvester.WebLibraryIntegration;
using CommandLine;

namespace BloomHarvester
{
	// This class should just get the command line arguments parsed, pass it off to Harvester, then get out of the way as much as possible.
	class Program
	{
		// Command line arguments sample: "harvestAll --environment=dev --parseDBEnvironment=prod"
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
				parser.ParseArguments<HarvestAllOptions, HarvestHighPriorityOptions, HarvestLowPriorityOptions>(args)
					.WithParsed<HarvestAllOptions>(options =>
					{
						Harvester.RunHarvestAll(options);
					})
					// TODO: Replace placeholders
					.WithParsed<HarvestHighPriorityOptions>(options => { throw new NotImplementedException("HarvestHighPriority"); })
					.WithParsed<HarvestLowPriorityOptions>(options => { throw new NotImplementedException("HarvestLowPriority"); })
					.WithNotParsed(errors =>
					{
						Console.Out.WriteLine("Error parsing command line arguments.");
						Environment.Exit(1);
					});
			}
			catch (Exception e)
			{
				YouTrackIssueConnector.SubmitToYouTrack(e, "An exception was thrown which was not handled by the program.");
				throw;
			}
		}
	}

	public abstract class HarvesterCommonOptions
	{
		[Option('e', "environment", Required = false, Default = EnvironmentSetting.Dev, HelpText = "Sets all environments to read/write from. Valid values are Default, Dev, Test, or Prod. If any individual component's environment are set to non-default, that value will take precedence over this.")]
		public EnvironmentSetting Environment { get; set; }

		[Option("parseDBEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from Parse DB. Valid values are Default, Dev, Test, or Prod. If specified (to non-Default), takes precedence over the general 'environment' option.")]
		public EnvironmentSetting ParseDBEnvironment { get; set; }

		[Option("logEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from the logging resource. Valid values are Default, Dev, Test, or Prod. If specified (to non-Default), takes precedence over the general 'environment' option.")]
		public EnvironmentSetting LogEnvironment { get; set; }

		[Option("suppressLogs", Required = false, Default = false, HelpText = "If true, will prevent log messages from being logged to the log environment (which may incur fees). Will write those logs to Standard Error instead.")]
		public bool SuppressLogs { get; set; }

		public virtual string GetPrettyPrint()
		{
			return $"environment: {Environment}\n" +
				$"parseDBEnvironment: {ParseDBEnvironment}\n" +
				$"logEnvironment: {LogEnvironment}\n" +
				$"suppressLogs: {SuppressLogs}";
		}
	}

	[Verb("harvestAll", HelpText = "Run Harvester on all books.")]
	public class HarvestAllOptions  : HarvesterCommonOptions
	{
		[Option("Count", Required = false, Default =-1, HelpText = "The amount of records to process. Default -1. If specified to a positive value, then processing will end after processing the specified number of books.")]
		public int Count { get; set; }

		public override string GetPrettyPrint()
		{
			return base.GetPrettyPrint() + "\n" +
				$"Count: {Count}";
		}
	}

	[Verb("harvestHighPriority", HelpText = "Run Harvester on high-priority items.")]
	public class HarvestHighPriorityOptions : HarvesterCommonOptions
	{
		// PLACEHOLDER
	}

	[Verb("harvestLowPriority", HelpText = "Run Harvester on low-priority items.")]
	public class HarvestLowPriorityOptions : HarvesterCommonOptions
	{
		// PLACEHOLDER
	}
}
