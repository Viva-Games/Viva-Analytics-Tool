using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Viva.Core.Tests
{
    public class VivaAdRevenueTests
    {
        [SetUp]
        public void SetUp()
        {
            VivaAdRevenue.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            VivaAdRevenue.Reset();
        }

        private static AdRevenueEvent Impression(double revenue) => new AdRevenueEvent
        {
            Format = "REWARDED", Placement = "hint", AdUnitId = "unit", NetworkName = "AdMob", Revenue = revenue, RevenuePrecision = "exact"
        };

        [Test]
        public void Publish_ReachesEverySubscriberInOrder()
        {
            var log = new List<string>();
            VivaAdRevenue.Subscribe(r => log.Add("a:" + r.Placement));
            VivaAdRevenue.Subscribe(r => log.Add("b:" + r.Placement));

            VivaAdRevenue.Publish(Impression(0.01));

            CollectionAssert.AreEqual(new[] { "a:hint", "b:hint" }, log);
            Assert.AreEqual(1, VivaAdRevenue.PublishedCount);
        }

        [Test]
        public void LateSubscriber_OnlyGetsFutureImpressions()
        {
            VivaAdRevenue.Publish(Impression(0.01));
            int received = 0;
            VivaAdRevenue.Subscribe(_ => received++);

            Assert.AreEqual(0, received);
            VivaAdRevenue.Publish(Impression(0.02));
            Assert.AreEqual(1, received);
        }

        [Test]
        public void Unsubscribe_AndDuplicateSubscribe()
        {
            int calls = 0;
            Action<AdRevenueEvent> callback = _ => calls++;
            VivaAdRevenue.Subscribe(callback);
            VivaAdRevenue.Subscribe(callback);
            VivaAdRevenue.Publish(Impression(0.01));
            Assert.AreEqual(1, calls, "registered once");

            VivaAdRevenue.Unsubscribe(callback);
            VivaAdRevenue.Publish(Impression(0.01));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void SubscriberException_DoesNotStopTheOthers()
        {
            bool secondRan = false;
            VivaAdRevenue.Subscribe(_ => throw new InvalidOperationException("subscriber broke"));
            VivaAdRevenue.Subscribe(_ => secondRan = true);

            LogAssert.Expect(LogType.Error, new Regex("ad revenue subscriber failed"));
            VivaAdRevenue.Publish(Impression(0.01));

            Assert.IsTrue(secondRan);
        }

        [Test]
        public void PublishNull_IsIgnoredWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("null event"));
            VivaAdRevenue.Publish(null);
            Assert.AreEqual(0, VivaAdRevenue.PublishedCount);
        }

        [Test]
        public void ToString_IsCultureInvariant()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
                StringAssert.Contains("revenue=0.01 USD", Impression(0.01).ToString());
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
