using RestSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace BloomHarvester.Parse
{
	class ParseException : Exception
	{
		public ParseException(): base()
		{
		}

		public ParseException(string message) : base(message)
		{
		}

		public ParseException(string message, Exception e) : base(message, e)
		{
		}

		public ParseException(IRestResponse response, string message)
			: base(message + GetPrettyPrintResponse(response))
		{
		}

		public ParseException(IRestResponse response, string messageFormat, params object[] args)
			: base(String.Format(messageFormat, args) + GetPrettyPrintResponse(response))
		{
		}

		private static string GetPrettyPrintResponse(IRestResponse response)
		{
			if (response == null)
			{
				return "";
			}

			string uriForMessage = response.ResponseUri?.ToString();
			int passwordIndex = uriForMessage?.IndexOf("password=") ?? -1;

			if (passwordIndex >= 0)
			{
				uriForMessage = uriForMessage.Substring(0, passwordIndex) + "password=****[...]";
			}

			return
				$"Response.Code: {response.StatusCode}\n" +
				$"Response.Uri: {uriForMessage}\n" +
				$"Response.Description: {response.StatusDescription}\n" +
				$"Response.Content: {response.Content}\n";
		}
	}
}
