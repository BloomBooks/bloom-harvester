using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Parse
{
	/// <summary>
	/// A class that represents a single operation that will be sent to Parse in batch.
	/// </summary>
	public class BatchableOperation
	{
		public RestSharp.Method Method { get; set; }

		private string _path;
		public string Path
		{
			get
			{
				return this._path;
			}
			set
			{
				if (value != null && !value.StartsWith('/'))
				{
					this._path = '/' + value;
				}
				else
				{
					this._path = value;
				}
			}
		}

		public string BodyJson { get; set; }
						
		public BatchableOperation(RestSharp.Method method, string path, string bodyJson)
		{
			this.Method = method;
			this.Path = path;
			this.BodyJson = bodyJson;
		}

		/// <summary>
		/// Gets the JSON representation in a format suitable for Parse's batch commands.
		/// </summary>
		/// Default serialization doesn't work. The Method enum int vs. string poses some problems, although that can be trivially solved.
		/// The more annoying part is in that by default body's value will be in quotes, but Parse expects the object directly
		/// <returns></returns>
		public string GetJson()
		{
			string json =
				"{\"method\":\"" + this.Method.ToString() + "\"," +
				"\"path\":\"" + this.Path + "\"," +
				"\"body\":" + this.BodyJson +	// Notice that Parse does not expect quotes around the value of body
				"}";

			return json;
		}
	}
}
