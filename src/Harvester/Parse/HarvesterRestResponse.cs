using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Parse
{
	[JsonObject]
	class HarvesterRestResponse<T>
	{
		[JsonProperty("count")]
		internal int Count;

		[JsonProperty("results")]
		internal T[] Results;
	}
}
