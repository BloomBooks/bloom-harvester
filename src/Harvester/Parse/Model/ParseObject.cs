using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloomHarvester.Parse.Model
{
	// Contains common fields to every object in a Parse class
	[JsonObject]
	public abstract class ParseObject
	{
		[JsonProperty("objectId")]
		public string ObjectId { get; set;  }
		//Date createdAt
		//Date updatedAt
		//ACL  ACL

		public override bool Equals(object other)
		{
			return (other is ParseObject) && this.ObjectId == ((ParseObject)other).ObjectId;
		}

		// Returns the class name (like a table name) of the class on the Parse server that this object corresponds to
		internal abstract string GetParseClassName();
	}
}
