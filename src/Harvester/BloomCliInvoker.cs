using BloomHarvester.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BloomHarvester
{
	interface IBloomCliInvoker
	{
		bool StartAndWaitForBloomCli(string arguments, int timeoutMilliseconds, out int exitCode, out string standardOutput, out string standardError);
	}

	internal class BloomCliInvoker : IBloomCliInvoker
	{
		internal BloomCliInvoker()
		{
			this.Logger = new NullLogger();
		}

		internal BloomCliInvoker(IMonitorLogger logger)
		{
			this.Logger = logger;
		}

		internal IMonitorLogger Logger;

		/// <summary>
		/// Starts a Bloom instance in a new process and waits up to the specified amount of time for it to finish.
		/// </summary>
		/// <param name="arguments">The arguments to pass to Bloom. (Don't include the name of the executable.)</param>
		/// <param name="timeoutMilliseconds">After this amount of time, the process will be killed if it's still running</param>
		/// <param name="exitCode">Out parameter. The exit code of the process.</param>
		/// <param name="standardOutput">Out parameter. The standard output of the process.</param>
		/// <param name="standardError">Out parameter. The standard error of the process.</param>
		/// <returns>Returns true if the process ended by itself without timeout. Returns false if the process was forcibly terminated.</returns>
		public bool StartAndWaitForBloomCli(string arguments, int timeoutMilliseconds, out int exitCode, out string standardOutput, out string standardError)
		{
			Logger.LogVerbose("Starting Bloom CLI process");
			Logger.LogVerbose("Bloom CLI arguments: " + arguments);
			var process = new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					FileName = "Bloom.exe",
					Arguments = arguments,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				}
			};

			StringBuilder processOutputBuffer = new StringBuilder();
			process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => { processOutputBuffer.Append(e.Data); });

			StringBuilder processErrorBuffer = new StringBuilder();
			process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => { processErrorBuffer.Append(e.Data); });

			process.Start();

			// These ReadToEnd() calls are filled with deadlock potential if you write them naively.
			// See this official documentation for details on proper usage:
			// https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput?redirectedfrom=MSDN&view=netframework-4.8#System_Diagnostics_ProcessStartInfo_RedirectStandardOutput
			//
			// Highlights: You shouldn't have WaitForExit() followed by ReadToEnd(). It will deadlock if the new process writes enough to fill the buffer.
			//             You shouldn't have ReadToEnd() of both stdout and stderr. It will deadlock if the new process writes enough to fill the buffer.
			//             Calling ReadToEnd() will deadlock if the new process crashes.
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			// Block and wait for it to finish
			bool hasExited = process.WaitForExit(timeoutMilliseconds);
			standardOutput = processOutputBuffer.ToString();
			standardError = processErrorBuffer.ToString();

			bool isExitedNormally = true;
			if (!hasExited)
			{
				try
				{
					process.Kill();
					isExitedNormally = false;
				}
				catch
				{
					// Just make a best effort to kill the process, but no need to throw exception if it didn't work
				}
			}

			exitCode = process.ExitCode;

			if (!String.IsNullOrWhiteSpace(standardOutput))
				Console.Out.WriteLine("Standard out: " + standardOutput);
			if (!String.IsNullOrWhiteSpace(standardError))
				Console.Out.WriteLine("Standard error: " + standardError);

			return isExitedNormally;
		}
	}
}
