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
        public static void Main(string[] args)
        {
            // See https://github.com/commandlineparser/commandline for documenation about CommandLine.Parser
            var parsedArgs = CommandLine.Parser.Default.ParseArguments<HarvestAllOptions, HarvestHighPriorityOptions, HarvestLowPriorityOptions>(args)
                .WithParsed<HarvestAllOptions>(options =>
                {
                    Harvester.RunHarvestAll(options);
                })
                .WithParsed<HarvestHighPriorityOptions>(options => { throw new NotImplementedException("HarvestHighPriority"); })
                .WithParsed<HarvestLowPriorityOptions>(options => { throw new NotImplementedException("HarvestLowPrioirity"); })
                .WithNotParsed(errors =>
                {
                    // Not implemented yet.
                    Console.Out.WriteLine("Error parsing command line arguments.");
                });
        }
    }

    [Verb("harvestAll", HelpText = "Run Harvester on all books.")]
    public class HarvestAllOptions
    {
        [Option("debug", Required = false, Default = true, HelpText = "Set parameters to debug/develop instances of Parse, AzureMonitor, etc.")]
        public bool IsDebug { get; set; }
        [Option("release", Required = false, Default = false, HelpText = "Set parameters to release instances of Parse, AzureMonitor, etc.")]
        public bool IsRelease { get; set; }
    }

    [Verb("harvestHighPriority", HelpText = "Run Harvester on high-priority items.")]
    public class HarvestHighPriorityOptions
    {
        [Option('d', "debug", Required = false, Default = true, HelpText = "Set parameters to debug/develop instances of Parse, AzureMonitor, etc.")]
        public bool IsDebug { get; set; }
    }

    [Verb("harvestLowPriority", HelpText = "Run Harvester on low-priority items.")]
    public class HarvestLowPriorityOptions
    {
        [Option('d', "debug", Required = false, Default = true, HelpText = "Set parameters to debug/develop instances of Parse, AzureMonitor, etc.")]
        public bool IsDebug { get; set; }
    }
}
