using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BloomHarvester.Parse.Model
{
	/// <summary>
	/// Represents the "Date" type defined by Parse.
	/// </summary>
	[JsonObject]
	public class ParseDate
	{
		private const string kDateFormat = "yyyy-MM-ddTHH:mm:ss.fffK";

		// Constructor
		public ParseDate(DateTime dateTime)
		{
			this.UtcTime = dateTime;
		}

		// Fields and Properties
		public readonly string __type = "Date";

		private string _iso;
		[JsonProperty("iso")]
		public string Iso
		{
			get
			{
				return _iso;
			}

			set
			{
				_iso = value;
				_utcTime = DateTime.ParseExact(value, kDateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);
			}
		}

		private DateTime _utcTime;
		[JsonIgnore]
		public DateTime UtcTime
		{
			get
			{
				return _utcTime;
			}

			set
			{
				_utcTime = value.ToUniversalTime();

				_iso = _utcTime.ToString(kDateFormat);
			}
		}

		public override bool Equals(object obj)
		{
			if (!(obj is ParseDate))
				return false;

			ParseDate other = (ParseDate)obj;
			// No need to compare UtcTime. 1) It's not actually serialized, and 2) is more error-prone to trivial differences showing up
			return this.Iso == other.Iso;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				// Derived from https://stackoverflow.com/a/5060059
				int hashCode = 37;
				hashCode *= 397;
				hashCode += Iso?.GetHashCode() ?? 0;
				return hashCode;
			}
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
