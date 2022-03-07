using System;

namespace BloomHarvester.LogEntries
{
	enum LogType
	{
		ArtifactSuitability,
		BloomCLIError,
		GetFontsError,
		MissingBaseUrl,
		MissingBloomDigitalIndex,
		MissingFont,
		InvalidFont,
		PHashError,
		ProcessBookError,
		TimeoutError
	}
}
