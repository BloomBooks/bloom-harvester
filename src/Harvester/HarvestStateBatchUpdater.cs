using BloomHarvester.Parse;
using BloomHarvester.Parse.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloomHarvester
{
	internal class HarvestStateBatchUpdater : Harvester
	{
		private HarvestState NewState { get; set; }
		private bool IsDryRun { get; set; }

		private HarvestStateBatchUpdater(BatchUpdateStateInParseOptions options)
			: base(new HarvesterOptions()
			{
				QueryWhere = options.QueryWhere,
				ParseDBEnvironment = options.ParseDBEnvironment,
				Environment = EnvironmentSetting.Local,
				SuppressLogs = true
			})
		{
			NewState = options.NewState;
			IsDryRun = options.DryRun;
		}

		internal static void RunBatchUpdateStates(BatchUpdateStateInParseOptions options)
		{
			using (HarvestStateBatchUpdater updater = new HarvestStateBatchUpdater(options))
			{
				updater.BatchUpdateStates();
			}
		}

		/// <summary>
		/// This function is here to allow setting the harvesterState to specific values for all rows matching the queryWhere condition
		/// </summary>
		private void BatchUpdateStates()
		{
			IEnumerable<Book> bookList = _parseClient.GetBooks(out bool didExitPrematurely, _options.QueryWhere);

			if (didExitPrematurely)
			{
				_logger.LogError("GetBooks() encountered an error and did not return all results. Aborting.");
				return;
			}

			foreach (var book in bookList)
			{
				_logger.LogInfo($"Updating objectId {book.ObjectId} from {book.HarvestState} to {NewState}");
				
				string newStateStr = "";
				if (NewState != HarvestState.Unknown)
				{
					newStateStr = NewState.ToString();
				}

				if (!IsDryRun)
				{
					book.UpdateOp.Clear();
					book.HarvestState = newStateStr;
					_parseClient.UpdateObject(book.GetParseClassName(), book.ObjectId, book.UpdateOp.ToJson());
				}
			}

			_logger.LogInfo($"{bookList.Count()} objects processed.");
			if (!IsDryRun)
			{
				_parseClient.FlushBatchableOperations();
			}
			else
			{
				_logger.LogInfo("Dry run done. (No updates were actually made)");
			}
		}
	}
}
