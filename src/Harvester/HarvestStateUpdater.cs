using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloomHarvester
{
	internal class HarvestStateUpdater
	{
		/// <summary>
		/// This function is here to allow setting the harvesterState to specific values to aid in setting up specific ad-hoc testing states.
		/// In the Parse database, there are some rules that automatically set the harvestState to "Updated" when the book is republished.
		/// Unfortunately, this rule also kicks in when a book is modified in the Parse dashboard or via the API Console (if no updateSource is set) :(
		/// 
		/// But executing this function allows you to set it to a value other than "Updated"
		/// </summary>
		internal static void UpdateState(EnvironmentSetting parseDbEnvironment, string objectId, Parse.Model.HarvestState newState)
		{
			var updateOp = BookModel.GetNewBookUpdateOperation();
			updateOp.UpdateFieldWithString(BookModel.kHarvestStateField, newState.ToString());

			EnvironmentSetting environment = EnvironmentUtils.GetEnvOrFallback(parseDbEnvironment, EnvironmentSetting.Default);
			var parseClient = new ParseClient(environment, null);
			parseClient.UpdateObject(BookModel.GetStaticParseClassName(), objectId, updateOp.ToJson());

			Console.Out.WriteLine($"Environment={parseDbEnvironment}: Sent request to update object \"{objectId}\" with harvestState={newState}");
		}
	}
}
