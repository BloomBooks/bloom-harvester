using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloomHarvester.Parse.Model
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public class Language : ParseObject
	{
		[JsonProperty("isoCode")]
		public string IsoCode { get; set; }

		[JsonProperty("ethnologueCode")]
		public string EthnologueCode { get; set; }

		[JsonProperty("name")]
		public string Name { get; set; }

		public override bool Equals(object obj)
		{
			if (!(obj is Language))
				return false;

			var other = (Language)obj;
			return base.Equals(other)
				&& this.IsoCode == other.IsoCode
				&& this.EthnologueCode == other.EthnologueCode
				&& this.Name == other.Name;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				// Derived from https://stackoverflow.com/a/5060059
				int hashCode = base.GetHashCode();
				hashCode *= 397;
				hashCode += IsoCode?.GetHashCode() ?? 0;
				hashCode *= 397;
				hashCode += EthnologueCode?.GetHashCode() ?? 0;
				hashCode *= 397;
				hashCode += Name?.GetHashCode() ?? 0;
				return hashCode;
			}
		}

		internal override string GetParseClassName()
		{
			return "language";
		}
	}
}
