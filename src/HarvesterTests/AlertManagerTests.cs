using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloomHarvester;
using NUnit.Framework;
using VSUnitTesting = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BloomHarvesterTests
{
	[TestFixture]
	public class AlertManagerTests
	{
		[SetUp]
		public void SetupBeforeEachTest()
		{
			AlertManager.Instance.Reset();
		}

		[Test]
		public void NoAlerts_NotSilenced()
		{
			var invoker = new VSUnitTesting.PrivateObject(AlertManager.Instance);
			bool result = (bool)invoker.Invoke("IsSilenced");
			Assert.That(result, Is.False);
		}

		[Test]
		public void ReportOneAlert_NotSilenced()
		{
			bool isSilenced = AlertManager.Instance.ReportAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.False);
		}

		[Test]
		public void ReportManyAlerts_Silenced()
		{
			bool isSilenced = false;
			for (int i = 0; i < 100; ++i)
				isSilenced = AlertManager.Instance.ReportAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.True);
		}

		[Test]
		public void ReportManyOldAlerts_NotSilenced()
		{
			var alertTimes = new LinkedList<DateTime>();
			for (int i = 0; i < 100; ++i)
				alertTimes.AddLast(DateTime.Now.AddDays(-3));

			var invoker = new VSUnitTesting.PrivateObject(AlertManager.Instance);
			invoker.SetField("_alertTimes", alertTimes);

			bool isSilenced = AlertManager.Instance.ReportAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.False);
		}

		[Test]
		public void ReportNPlus1Alerts_OnlyLastSilenced()
		{
			// N alerts should go through.
			// THe N+1th alert is the first one to be silenced (not the nth)
			bool isSilenced = false;

			for (int i = 0; i < AlertManager.kMaxAlertCount; ++i)
			{
				isSilenced = AlertManager.Instance.ReportAlertAndCheckIfSilenced();
				Assert.That(isSilenced, Is.False, $"Iteration {i}");
			}

			isSilenced = AlertManager.Instance.ReportAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.True);
		}
	}
}
