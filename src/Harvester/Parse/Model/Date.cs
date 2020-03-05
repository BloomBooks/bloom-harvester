using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BloomHarvester.Parse.Model
{
	[JsonObject]
	public class Date
	{
		private const string kDateFormat = "yyyy-MM-ddTHH:mm:ss.fffK";

		// Constructor
		public Date(DateTime dateTime)
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
			if (!(obj is Date))
				return false;

			Date other = (Date)obj;
			// No need to compare UtcTime. 1) It's not actually serialized, and 2) is more error-prone to trivial differences showing up
			return this.Iso == other.Iso;
		}

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
