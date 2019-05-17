using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BloomHarvester
{
	public enum EnvironmentSetting
	{
		Default,
		Dev,
		Test,
		Prod
	}

	public class EnvironmentUtils
	{
		/// <summary>
		/// Returns a non-zero (aka non-Default) value of Environment to use, based on the higher-precedence value and the fallback value.
		/// </summary>
		/// <param name="resourceEnv">The value with higher precedence</param>
		/// <param name="fallbackEnv">The value to use if the higher precedence is not set to a non-Default value</param>
		/// <returns></returns>
		public static EnvironmentSetting GetEnvOrFallback(EnvironmentSetting resourceEnv, EnvironmentSetting fallbackEnv)
		{
			EnvironmentSetting parsedEnv = resourceEnv;

			if (resourceEnv != EnvironmentSetting.Default)
			{
				// Individual resource's environment was specified. Lets use it directly.
				parsedEnv = resourceEnv;
			}
			else if (fallbackEnv != EnvironmentSetting.Default)
			{
				// Resource environment not specified, but general environment parameter was.
				// Fallback to general environment parameter
				parsedEnv = fallbackEnv;
			}
			else
			{
				// Neither one specified. Set it based on whether running in Debug or Release mode.
#if DEBUG
				// In Debug Mode, initialize to Dev by default
				parsedEnv = EnvironmentSetting.Dev;
#else
				parsedEnv = EnvironmentSetting.Prod;
#endif
			}

			// Verify Postcondition: Should not return Environment.Default
			Debug.Assert(parsedEnv != EnvironmentSetting.Default, "GetEnvironment should determine a specific, non-default value of Enviornment");

			return parsedEnv;
		}
	}
}
