﻿using BloomHarvester.Logger;
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
		const int kLookbackWindowInHours = 24;

		private AlertManager()
		{
			_alertTimes = new LinkedList<DateTime>();
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
		private LinkedList<DateTime> _alertTimes;	// This list should be maintained in ascending order
		internal IMonitorLogger Logger { get; set; }

		// Methods

		/// <summary>
		/// Tells the AlertManager to record that an alert is taking place. Also checks if the alert should be silenced or not
		/// </summary>
		/// <returns>Returns true if the current alert should be silenced, false otherwise</returns>
		public bool RecordAlertAndCheckIfSilenced()
		{
			_alertTimes.AddLast(DateTime.Now);

			bool isSilenced = this.IsSilenced();
			if (isSilenced && Logger != null)
			{
				Logger.TrackEvent("AlertManager: An alert was silenced (too many alerts).");
			}

			return isSilenced;
		}

		/// <summary>
		/// Resets the history of tracked alerts back to empty.
		/// </summary>
		public void Reset()
		{
			_alertTimes.Clear();
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
			return _alertTimes.Count > kMaxAlertCount;
		}

		/// <summary>
		/// // Prunes the list of fired alerts such that it only contains the timestamps within the lookback period.
		/// </summary>
		private void PruneList()
		{
			DateTime startTime = DateTime.Now.Subtract(TimeSpan.FromHours(kLookbackWindowInHours));

			// Precondition: This list must be in sorted order
			while (_alertTimes.Any() && _alertTimes.First.Value < startTime)
			{
				_alertTimes.RemoveFirst();
			}
		}
	}
}
