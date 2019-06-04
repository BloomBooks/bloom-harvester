using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BloomHarvester.Parse
{
	internal class UpdateOperation
	{
		private Dictionary<string, string> _updatedFieldValues = new Dictionary<string, string>();

		internal UpdateOperation()
		{
			_updatedFieldValues["updateSource"] = "\"bloomHarvester\"";
		}

		internal void Clear()
		{
			_updatedFieldValues.Clear();
		}

		/// <summary>
		/// Registers that a field was updated
		/// </summary>
		/// <param name="fieldName"></param>
		/// <param name="fieldValueJson">If the value represents a string, it should be passed in quoted. If the value represents an object, it should not have quotes surrounding it... just start with a brace.</param>
		internal void UpdateField(string fieldName, string fieldValueJson)
		{
			string trimmedValue = fieldValueJson.TrimStart();
			bool isArray = trimmedValue.StartsWith("[");
			bool isObject = trimmedValue.StartsWith("{");
			bool isWellDefinedString = trimmedValue.StartsWith("\"");
			bool isUnquotedString = !isArray && !isObject && !isWellDefinedString;
			if (isUnquotedString)
			{
				Debug.Assert(!fieldValueJson.StartsWith(" "), "Invalid JSON passed into UpdateField: {0}", fieldValueJson); // Too complicated to fix, just Assert instead.

				fieldValueJson = '"' + fieldValueJson + '"';
			}

			_updatedFieldValues[fieldName] = fieldValueJson;
		}

		// Gets the JSON string to use in the Parse request to tell it all the fields to update
		internal string ToJson()
		{
			if (!_updatedFieldValues.Any())
			{
				return "{}";
			}

			var tupleJsons = _updatedFieldValues.Select(kvp => $"\"{kvp.Key}\":{kvp.Value}");
			string json = "{" + String.Join(",", tupleJsons) + "}";

			return json;
		}
	}
}
