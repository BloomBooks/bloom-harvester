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
		/// Stores the list of member names (e.g. field or property names) that are
		/// 1) Have been updated, and
		/// 2) Require manual tracking of whether they've been updated, instead using our automatic .Equals() mechanims
		/// </summary>
		protected List<string> _updatedMembers = new List<string>();

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
			// This list stored the fields that are different than the last database copy
			// Since we're about to change the database copy, it's time to clear this out.
			_updatedMembers.Clear();

			DatabaseVersion = this.Clone();
		}

		/// <summary>
		///  Writes any pending operation to the database
		/// </summary>
		/// <param name="database"></param>
		/// <param name="isReadOnly"></param>
		public void FlushUpdateToDatabase(ParseClient database, bool isReadOnly = false)
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
				// Remove any which are manually tracked
				.Where(member => member.Name != "Show")
				// Only include the ones which are serialized to JSON
				.Where(member => member.CustomAttributes.Any())
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

			// Now we handle ones that are manually tracked using _updatedMembers.
			// This is designed for dynamic objects, for which the default Equals() function will probably not do what we want (since it's just a ref comparison)
			foreach (var updatedMemberName in _updatedMembers ?? Enumerable.Empty<string>())
			{
				FieldInfo fieldInfo = type.GetField(updatedMemberName);
				if (fieldInfo != null)
				{
					string memberName = GetMemberJsonName(fieldInfo);
					object newValue = fieldInfo.GetValue(this);
					pendingUpdates.UpdateFieldWithObject(memberName, newValue);
				}
				else
				{
					PropertyInfo propertyInfo = type.GetProperty(updatedMemberName);
					if (propertyInfo != null)
					{
						string memberName = GetMemberJsonName(propertyInfo);
						object newValue = propertyInfo.GetValue(this);
						pendingUpdates.UpdateFieldWithObject(memberName, newValue);
					}
				}
			}

			return pendingUpdates;
		}

		/// <summary>
		/// Checks if two objects are the same (using .Equals()). Handles nulls, arrays, and lists in addition to scalars.
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
			else if (obj1.GetType().IsArray)
			{
				// Default .Equals() on an array checks if they're the same reference, not if their contents are equal
				// So we'll walk through the array and check if each element is equal to the corresponding one in the other array

				var array1 = (object[])obj1;
				var array2 = (object[])obj2;

				if (array1.Length != array2.Length)
				{
					// Different lengths... definitely not equal
					return false;
				}
				else
				{
					// Now we know the lengths are the same. That makes checking this array for equality simpler.
					for (int i = 0; i < array1.Length; ++i)
					{
						if (!array1[i].Equals(array2[i]))
						{
							return false;
						}
					}

					return true;
				}
			}
			else if (obj1 is IList && obj2 is IList)
			{
				// Similar issue for lists as arrays. Default .Equals() only checks for the same reference
				var list1 = (IList)obj1;
				var list2 = (IList)obj2;

				if (list1.Count != list2.Count)
				{
					// Different lengths... definitely not equal
					return false;
				}
				else
				{
					// Now we know the lengths are the same. That makes checking this array for equality simpler.
					for (int i = 0; i < list1.Count; ++i)
					{
						if (!list1[i].Equals(list2[i]))
						{
							return false;
						}
					}

					return true;
				}
			}
			else
			{
				// Simple scalars. We can just call .Equals() to check equality.
				return obj1.Equals(obj2);
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
			if (memberInfo.CustomAttributes?.FirstOrDefault()?.ConstructorArguments?.Any() == true)
			{
				string jsonMemberName = memberInfo.CustomAttributes.First().ConstructorArguments[0].Value as String;
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
