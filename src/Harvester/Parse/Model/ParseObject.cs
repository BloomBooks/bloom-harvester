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

		public override int GetHashCode()
		{
			// Derived from https://stackoverflow.com/a/5060059
			int hashCode = 37;
			hashCode *= 397;
			hashCode += ObjectId?.GetHashCode() ?? 0;
			return hashCode;
		}

		// Returns the class name (like a table name) of the class on the Parse server that this object corresponds to
		internal abstract string GetParseClassName();
	}
}
