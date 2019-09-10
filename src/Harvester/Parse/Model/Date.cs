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
		public string __type = "Date";

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
				_utcTime = DateTime.ParseExact(value, kDateFormat, System.Globalization.CultureInfo.InvariantCulture);
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

		public string ToJson()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
