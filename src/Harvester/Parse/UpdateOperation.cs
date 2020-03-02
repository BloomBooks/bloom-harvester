using Newtonsoft.Json;
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

		/// <summary>
		/// This dictionary stores the objects which are updated, but whose JSON value should wait to be determined until later (i.e, deferred)
		/// The key is the name of the field that was updated.
		/// The value is the object whose JSON should be determined at a later time
		/// </summary>
		private Dictionary<string, object> _deferredUpdatedFieldObjects = new Dictionary<string, object>();

		internal UpdateOperation()
		{
		}

		virtual internal void Clear()
		{
			_updatedFieldValues.Clear();
			_deferredUpdatedFieldObjects.Clear();
		}

		internal void RemoveUpdate(string fieldName)
		{
			_updatedFieldValues.Remove(fieldName);
			_deferredUpdatedFieldObjects.Remove(fieldName);
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
			string jsonRepresentation = $"\"{fieldValue}\"";
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
		/// Registers a deferred update. Rather than taking the JSON of the object's current state, will take the JSON of the object's state
		/// at the time when this UpdateOp's JSON is taken
		/// What this accomplishes is to allow an object to continue to be modified up until the time that this updateOp is finally processed (which will invoke its ToJson())
		/// and this class will automatically pick up the latest state
		/// </summary>
		/// <param name="fieldName">The name of the field that was updated.</param>
		/// <param name="objectThatMightBeModified">The object whose value is being update</param>
		internal void DeferUpdateOfFieldWithObject(string fieldName, object objectThatMightBeModified)
		{
			_deferredUpdatedFieldObjects[fieldName] = objectThatMightBeModified;

			_updatedFieldValues.Remove(fieldName);
		}

		/// <summary>
		/// Processes all the update operations that were deferred till now, and adds their JSON to the normal collection of updated values
		/// </summary>
		private void ProcessDeferredUpdates()
		{
			foreach (var kvp in _deferredUpdatedFieldObjects)
			{
				string field = kvp.Key;
				object objectThatWasUpdated = kvp.Value;

				string currentJson = JsonConvert.SerializeObject(objectThatWasUpdated);
				UpdateFieldWithJson(field, currentJson);
			}

			_deferredUpdatedFieldObjects.Clear();
		}

		// Gets the JSON string to use in the Parse request to tell it all the fields to update
		internal string ToJson()
		{
			// First flush all deferred updates to join the rest of the updates in _updatedFieldValues
			ProcessDeferredUpdates();

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
