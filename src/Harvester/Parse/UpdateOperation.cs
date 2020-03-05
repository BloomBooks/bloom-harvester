using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BloomHarvester.Parse
{
	/// <summary>
	/// Tracks the updates that need to be written to a row in Parse
	/// Contains logic for being able to get the JSON body to send in the REST call to update the row object
	/// </summary>
	internal class UpdateOperation
	{
		internal Dictionary<string, string> _updatedFieldValues = new Dictionary<string, string>();

		internal UpdateOperation()
		{
		}

		/// <summary>
		/// Registers that a field was updated
		/// </summary>
		/// <param name="fieldName">The name of the field that was updated.</param>
		/// <param name="fieldValueJson">the JSON representing of an object. If the value represents a string, it should be passed in quoted. If the value represents an object, it should not have quotes surrounding it... just start with a brace. Numbers should be passed in unquoted.</param>
		internal void UpdateFieldWithJson(string fieldName, string fieldValueJson)
		{
			_updatedFieldValues[fieldName] = fieldValueJson;
		}

		/// <summary>
		/// Registers that a field was updated.
		/// </summary>
		/// <param name="fieldName"></param>
		/// <param name="str">The string representing the field value. This is the raw string, it should not be escaped with quotes or converted to JSON or anything.</param>
		internal void UpdateFieldWithString(string fieldName, string fieldValue)
		{
			string jsonRepresentation = fieldValue != null ? $"\"{fieldValue}\"" : "null";
			UpdateFieldWithJson(fieldName, jsonRepresentation);
		}

		/// <summary>
		/// Registers that a field was updated.
		/// </summary>
		/// <param name="fieldName">The name of the field that was updated.</param>
		/// <param name="numericFieldValue">Nominally should be some kind of int, double, etc.</param>
		internal void UpdateFieldWithNumber(string fieldName, object numericFieldValue)
		{
			string jsonRepresentation = numericFieldValue.ToString();   // No need for escaping with quotes (or braces or brackets or anything)
			UpdateFieldWithJson(fieldName, jsonRepresentation);
		}

		/// <summary>
		/// Registers that a field was updated
		/// </summary>
		/// <param name="fieldName">The name of the field that was updated.</param>
		/// <param name="obj">The object's value. It will be serialized to JSON immediately in order to record the update</param>
		internal void UpdateFieldWithObject(string fieldName, object obj)
		{
			string jsonRepresentation = JsonConvert.SerializeObject(obj);
			UpdateFieldWithJson(fieldName, jsonRepresentation);
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
