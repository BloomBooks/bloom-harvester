using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Parse.Model
{
	// Represents a Parse pointer
	[JsonObject]
	public class Pointer<T> where T : ParseObject
	{
		[JsonProperty(Order=1)]
		public const string __type = "Pointer";

		[JsonProperty("className", Order=2)]
		public string ClassName;

		[JsonProperty("objectId", Order=3)]
		public string ObjectId;

		[JsonIgnore]
		public T Value { get; set; }

		public Pointer(T value)
		{
			this.ClassName = value?.GetParseClassName() ?? "";
			this.ObjectId = value?.ObjectId;
			this.Value = value;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Pointer<T>))
				return false;

			var other = (Pointer<T>)obj;
			return this.ObjectId == other.ObjectId
				&& this.ClassName == other.ClassName;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				// Derived from https://stackoverflow.com/a/5060059
				int hashCode = 37;
				hashCode *= 397;
				hashCode += ObjectId?.GetHashCode() ?? 0;
				hashCode *= 397;
				hashCode += ClassName?.GetHashCode() ?? 0;
				return hashCode;
			}
		}

		internal string ToJson()
		{
			if (this.Value == null)
			{
				return "{}";
			}

			return "{" + $"\"__type\":\"{__type}\",\"className\":\"{ClassName}\",\"objectId\":\"{ObjectId}\"" + "}";
		}
	}
}
