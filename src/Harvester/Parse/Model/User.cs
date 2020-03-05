using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BloomHarvester.Parse.Model
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public class User : ParseObject
	{
		//[JsonProperty("username"]
		//public string UserName;

		// Enhance: Add more fields as needed.
		// But, for user information, better to utilize as little as needed

		internal override string GetParseClassName()
		{
			return "User";
		}
	}
}
