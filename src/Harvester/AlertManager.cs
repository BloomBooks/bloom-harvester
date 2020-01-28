using BloomHarvester.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloomHarvester
{
	/// <summary>
	/// This class keeps track of alerts that are reported and whether alerts should be silenced for being too "noisy" (frequent)	///
	/// Use the TryReportAlerts(out bool isSilenced) function to do this
	/// </summary>
	public class AlertManager
	{
		internal const int kMaxAlertCount = 5;
		const int lookbackWindowInHours = 24;

		private AlertManager()
		{
			alertTimes = new LinkedList<DateTime>();
		}

		// Singleton access
		private static AlertManager _instance = null;
		public static AlertManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new AlertManager();
				}

				return _instance;
			}
		}

		// Other fields/properties
		private LinkedList<DateTime> alertTimes;	// This list should be maintained in ascending order
		internal IMonitorLogger Logger { get; set; }

		// Methods

		/// <summary>
		/// Use to report that an alert is about to take place.
		/// </summary>
		/// <param name="isSilenced">outparam - Returns whether the current alert should be silenced.</param>
		public void TryReportAlert(out bool isSilenced)
		{
			// Returns isSilenced as an out parameter to make it more obvious to the caller that they should check its value.
			//   (Return values are easier to overlook)

			TryReportAlert(DateTime.Now, out isSilenced);
		}

		/// <summary>
		/// Use to report that an alert is about to take place.
		/// </summary>
		/// <param name="isSilenced">outparam - Returns whether the current alert should be silenced.</param>
		public void TryReportAlert(DateTime alertTime, out bool isSilenced)
		{
			alertTimes.AddLast(alertTime);

			isSilenced = this.IsSilenced();
			if (isSilenced && Logger != null)
			{
				Logger.TrackEvent("AlertManager: An alert was silenced (too many alerts).");
			}
		}

		/// <summary>
		/// Resets the history of tracked alerts back to empty.
		/// </summary>
		public void Reset()
		{
			alertTimes.Clear();
		}

		/// <summary>
		/// Returns whether alerts should currently be silenced
		/// </summary>
		/// <returns>Returns true for silenced, false for not silenced</returns>
		private bool IsSilenced()
		{
			// Determine how many alerts have been fired since the start time of the lookback period
			PruneList();

			// Current model has the same (well, inverted) condition for entering and exiting the Silenced state.
			// Another model could use unrelated conditions for entering vs. exiting the Silenced state
			return alertTimes.Count > kMaxAlertCount;
		}

		/// <summary>
		/// // Prunes the list of fired alerts such that it only contains the timestamps within the lookback period.
		/// </summary>
		private void PruneList()
		{
			DateTime startTime = DateTime.Now.Subtract(TimeSpan.FromHours(lookbackWindowInHours));

			// Precondition: This list must be in sorted order
			while (alertTimes.Any() && alertTimes.First.Value < startTime)
			{
				alertTimes.RemoveFirst();
			}
		}
	}
}
