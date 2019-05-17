using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
	}

	public abstract class HarvesterCommonOptions
	{
		[Option('e', "environment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets all environments to read/write from. Valid values are Default, Dev, or Prod. If any individual component's envioronment are set to non-default, that value will take precedence over this.")]
		public EnvironmentSetting Environment { get; set; }

		[Option("parseDBEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from Parse DB. Valid values are Default, Dev, or Prod. If specified, takes precedence over the general 'environment' option.")]
		public EnvironmentSetting ParseDBEnvironment { get; set; }

		[Option("logEnvironment", Required = false, Default = EnvironmentSetting.Default, HelpText = "Sets the environment to read/write from the logging resource. Valid values are Default, Dev, or Prod. If specified, takes precedence over the general 'environment' option.")]
		public EnvironmentSetting LogEnvironment { get; set; }

		public virtual string GetPrettyPrint()
		{
			return $"environment: {Environment}\n" +
				$"parseDBEnvironment: {ParseDBEnvironment}\n" +
				$"logEnvironment: {LogEnvironment}";
		}
	}

	[Verb("harvestAll", HelpText = "Run Harvester on all books.")]
	public class HarvestAllOptions  : HarvesterCommonOptions
	{
		// TODO: Remove placeholder
		[Option("HarvestAllPlaceholder", Required = false)]
		public string HarvestAllPlaceholder { get; set; }

		public override string GetPrettyPrint()
		{
			return base.GetPrettyPrint() + "\n" +
				$"HarvestAllPlaceholder: {HarvestAllPlaceholder}";
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
