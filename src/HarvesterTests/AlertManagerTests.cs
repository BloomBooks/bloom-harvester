using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloomHarvester;
using BloomHarvester.Parse.Model;
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
			bool result = (bool)invoker.Invoke("IsSilenced", new Object[] {null});
			Assert.That(result, Is.False);
		}

		[Test]
		public void ReportOneAlert_NotSilenced()
		{
			bool isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.False);
		}

		[Test]
		public void ReportManyAlerts_Silenced()
		{
			bool isSilenced = false;
			for (int i = 0; i < 100; ++i)
				isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.True);
		}

		[Test]
		public void ReportManyOldAlerts_NotSilenced()
		{
			var alertTimes = new LinkedList<AlertManager.AlertTimeStamp>();
			for (int i = 0; i < 100; ++i)
				alertTimes.AddLast(new AlertManager.AlertTimeStamp { TimeStamp = DateTime.Now.AddDays(-3) });

			var invoker = new VSUnitTesting.PrivateObject(AlertManager.Instance);
			invoker.SetField("_alertTimes", alertTimes);

			bool isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced();
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
				isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced();
				Assert.That(isSilenced, Is.False, $"Iteration {i}");
			}

			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced();
			Assert.That(isSilenced, Is.True);
		}

		[Test]
		public void NewBookErrorsGetReportedOncePerBookOrMaxPerUploader()
		{
			// Fill in the maximum number of allowed daily alerts with default data.
			for (int i = 0; i < AlertManager.kMaxAlertCount; ++i)
			{
				AlertManager.Instance.RecordAlertAndCheckIfSilenced();
			}
			var model1 = new BookModel("first book url", "First Book", userId: "uploader1") {HarvestState = "New"};
			var model2 = new BookModel("second book url", "Second Book", userId: "uploader1") {HarvestState = "New"};
			var model3 = new BookModel("third book url", "Third Book", userId: "uploader1") {HarvestState = "New"};
			var model4 = new BookModel("fourth book url", "Fourth Book", userId: "uploader1") {HarvestState = "New"};
			var model5 = new BookModel("fifth book url", "Fifth Book", userId: "uploader1") {HarvestState = "New"};
			var model6 = new BookModel("sixth book url", "Sixth Book", userId: "uploader1") {HarvestState = "New"};
			var model7 = new BookModel("seventh book url", "Seventh Book", userId: "uploader2") {HarvestState = "New"};
			var model8 = new BookModel("eighth book url", "Eighth Book", userId: "uploader2") {HarvestState = "New"};

			var isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model1);
			Assert.That(isSilenced, Is.False, "first book");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model2);
			Assert.That(isSilenced, Is.False, "second book");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model3);
			Assert.That(isSilenced, Is.False, "third book");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model4);
			Assert.That(isSilenced, Is.False, "fourth book");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model5);
			Assert.That(isSilenced, Is.False, "fifth book");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model6);
			Assert.That(isSilenced, Is.True, "sixth book: too many by same uploader");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model7);
			Assert.That(isSilenced, Is.False, "seventh book: new uploader");
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model8);
			Assert.That(isSilenced, Is.False, "eighth book");

			model7.HarvestState = "Updated";
			isSilenced = AlertManager.Instance.RecordAlertAndCheckIfSilenced(model7);
			Assert.That(isSilenced, Is.True, "seventh book updated: already reported once today");
		}
	}
}
