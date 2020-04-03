using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BloomHarvester.Parse.Model
{
	/// <summary>
	/// An abstract class that represents a Parse object which can update its value in the database
	/// The object can be modified in memory, but updates will not be written to the DB until FlushUpdateToDatabase is called.
	/// </summary>
	public abstract class WriteableParseObject : ParseObject
	{
		/// <summary>
		/// Stores a copy of the object which matches the state of the row in the database
		/// </summary>
		private WriteableParseObject DatabaseVersion { get; set; }

		/// <summary>
		/// Create a deep copy of the current object
		/// </summary>
		public abstract WriteableParseObject Clone();

		#region Updating the Parse DB code
		/// <summary>
		/// For safety, we have an opt-in mechanism where the derived class needs to specifically list out the members which it is allowing updates for
		/// This isn't really necessary, but is just to provide a safety mechanism against something an unintended column being accidentally modified
		/// The strings should be the names of the fields/properties as they are in the C# code (as opposed to their names when serialized to JSON)
		/// </summary>
		/// <returns></returns>
		protected abstract HashSet<string> GetWriteableMembers();

		/// <summary>
		/// Registers that the current version of this object is what the database currently stores
		//  Makes a deep copy of this object and saves it for future reference
		//  This should be called whenever we set this object to a Read from the database, or Write this object to the database 
		/// </summary>
		public void MarkAsDatabaseVersion()
		{
			DatabaseVersion = this.Clone();
		}

		/// <summary>
		///  Writes any pending operation to the database
		/// </summary>
		/// <param name="database"></param>
		/// <param name="isReadOnly"></param>
		public void FlushUpdateToDatabase(IParseClient database, bool isReadOnly = false)
		{
			// ENHANCE: I suppose if desired, we could try to re-read the row in the database and make sure we compare against the most recent veresion.
			// Dunno if that makes life any better when 2 sources are trying to update it.
			// Maybe the best thing to do is to re-read the row, check if it's the same as our current one, and abort the processing if we don't have the right version?

			var pendingUpdates = this.GetPendingUpdates();
			if (!pendingUpdates._updatedFieldValues.Any())
			{
				return;
			}

			var pendingUpdatesJson = pendingUpdates.ToJson();
			if (isReadOnly)
			{
				Console.Out.WriteLine("SKIPPING WRITE BECAUSE READ ONLY: " + pendingUpdatesJson);
			}
			else
			{
				database.UpdateObject(this.GetParseClassName(), this.ObjectId, pendingUpdatesJson);
			}

			MarkAsDatabaseVersion();
		}

		/// <summary>
		/// Checks against the old database version to see which fields/properties have been updated and need to be written to the database
		/// Note: Any dynamic objects are probably going to get updated every time. It's hard to compare them.
		/// </summary>
		/// <returns>UpdateOperation with the necessary updates</returns>
		internal virtual UpdateOperation GetPendingUpdates()
		{
			var pendingUpdates = new UpdateOperation();
			var type = this.GetType();
			var writeableMemberNames = this.GetWriteableMembers();

			List<MemberInfo> fieldsAndProperties =
				// First collect all the fields and properties
				type.GetFields().Cast<MemberInfo>()
				.Concat(type.GetProperties().Cast<MemberInfo>())
				// Only include those columns which are explicitly marked as writeable
				.Where(member => writeableMemberNames.Contains(member.Name))
				// Only include the ones which are serialized to JSON
				.Where(member => member.CustomAttributes.Any(attr => attr.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute"))
				.ToList();

			// Iterate over and process each automatically handled field/property
			// to see if its value has been modified since the last time we read/wrote to the database
			foreach (var memberInfo in fieldsAndProperties)
			{
				object oldValue;
				object newValue;

				if (memberInfo is FieldInfo)
				{
					var fieldInfo = (FieldInfo)memberInfo;
					oldValue = fieldInfo.GetValue(this.DatabaseVersion);
					newValue = fieldInfo.GetValue(this);
				}
				else
				{
					// We know that everything here is supposed to be either a FieldInfo or PropertyInfo,
					// so if it's not FieldInfo, it should be a propertyInfo
					var propertyInfo = (PropertyInfo)memberInfo;

					if (this.DatabaseVersion == null)
						oldValue = null;
					else
						oldValue = propertyInfo.GetValue(this.DatabaseVersion);

					newValue = propertyInfo.GetValue(this);
				}

				// Record an update if the value has been modified
				if (!AreObjectsEqual(oldValue, newValue))
				{
					string propertyName = GetMemberJsonName(memberInfo);
					pendingUpdates.UpdateFieldWithObject(propertyName, newValue);
				}
			}

			return pendingUpdates;
		}


		/// <summary>
		/// Checks if two objects are the same (by comparing their JSON). Handles nulls, arrays, lists, and dynamic objects in addition to normal scalars.
		/// </summary>
		internal static bool AreObjectsEqual(object obj1, object obj2)
		{
			// First, get the null cases out of the way
			if (obj1 == null)
			{
				return obj2 == null;
			}
			else if (obj2 == null)
			{
				// At this point, we know that obj1 was non-null, so if obj2 is null, we know they're different;
				return false;

				// For code below here, we know that obj1 and obj2 are both non-null
			}
			else
			{
				// Determine if they are equal by looking at their JSON representations.
				// This is moderately helpful for checking if arrays/lists are equal,
				// but especially helpful for checking that dynamic objects are equal, which would otherwise be a pain to check
				string json1 = JsonConvert.SerializeObject(obj1);
				string json2 = JsonConvert.SerializeObject(obj2);
				return json1 == json2;
			}
		}

		/// <summary>
		/// Finds the name of a field/property that is used when it is serialized to JSON
		/// (AKA the name that is given to JsonPropertyAttribute)
		/// </summary>
		/// <param name="memberInfo"></param>
		/// <returns></returns>
		private string GetMemberJsonName(MemberInfo memberInfo)
		{
			string name = memberInfo.Name;
			var jsonAttr = memberInfo.CustomAttributes
				?.FirstOrDefault(attr => attr.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute"
					&& attr.ConstructorArguments?.Count > 0);

			if (jsonAttr != null)
			{
				string jsonMemberName = jsonAttr.ConstructorArguments[0].Value as String;
				if (!String.IsNullOrWhiteSpace(jsonMemberName))
				{
					name = jsonMemberName;
				}
			}

			return name;
		}
		#endregion
	}
}
