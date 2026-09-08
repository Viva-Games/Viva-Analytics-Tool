using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Viva.Core.Tests
{
    /// <summary>
    /// Contrato del puente de consentimiento: el que publica y los que consumen no saben quién va primero.
    /// </summary>
    public class VivaConsentTests
    {
        [SetUp]
        public void SetUp()
        {
            VivaConsent.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            VivaConsent.Reset();
        }

        private static ConsentState Gdpr(bool all) => new ConsentState
        {
            IsGdpr = true, Purpose1 = all, Purpose3 = all, Purpose4 = all, Purpose7 = all, GoogleVendor = all, Source = "test"
        };

        [Test]
        public void StartsUnresolved()
        {
            Assert.IsFalse(VivaConsent.IsResolved);
            Assert.IsNull(VivaConsent.Current);
        }

        [Test]
        public void SubscribeBeforeSet_ReceivesTheStateWhenPublished()
        {
            ConsentState received = null;
            VivaConsent.Subscribe(state => received = state);
            Assert.IsNull(received, "nothing until someone publishes");

            var published = Gdpr(true);
            VivaConsent.Set(published);

            Assert.AreSame(published, received);
            Assert.IsTrue(VivaConsent.IsResolved);
            Assert.AreSame(published, VivaConsent.Current);
        }

        [Test]
        public void SubscribeAfterSet_ReceivesTheCurrentStateImmediately()
        {
            var published = Gdpr(false);
            VivaConsent.Set(published);

            ConsentState received = null;
            VivaConsent.Subscribe(state => received = state);

            Assert.AreSame(published, received);
        }

        [Test]
        public void SetAgain_NotifiesEverySubscriber_LastOneWins()
        {
            var log = new List<string>();
            VivaConsent.Subscribe(state => log.Add("a:" + state.Purpose1));
            VivaConsent.Subscribe(state => log.Add("b:" + state.Purpose1));

            VivaConsent.Set(Gdpr(false));
            VivaConsent.Set(Gdpr(true));

            CollectionAssert.AreEqual(new[] { "a:False", "b:False", "a:True", "b:True" }, log);
            Assert.AreEqual(true, VivaConsent.Current.Purpose1);
        }

        [Test]
        public void Unsubscribe_StopsNotifications()
        {
            int calls = 0;
            Action<ConsentState> callback = _ => calls++;
            VivaConsent.Subscribe(callback);
            VivaConsent.Set(Gdpr(true));
            VivaConsent.Unsubscribe(callback);
            VivaConsent.Set(Gdpr(false));

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void SubscribingTwice_RegistersOnce()
        {
            int calls = 0;
            Action<ConsentState> callback = _ => calls++;
            VivaConsent.Subscribe(callback);
            VivaConsent.Subscribe(callback);
            VivaConsent.Set(Gdpr(true));

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void SubscriberException_DoesNotStopTheOthers()
        {
            bool secondRan = false;
            VivaConsent.Subscribe(_ => throw new InvalidOperationException("subscriber broke"));
            VivaConsent.Subscribe(_ => secondRan = true);

            LogAssert.Expect(LogType.Error, new Regex("consent subscriber failed"));
            VivaConsent.Set(Gdpr(true));

            Assert.IsTrue(secondRan);
        }

        [Test]
        public void SetNull_IsIgnoredWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("null state"));
            VivaConsent.Set(null);
            Assert.IsFalse(VivaConsent.IsResolved);
        }

        [Test]
        public void ToString_ShowsEveryField()
        {
            var state = new ConsentState { IsGdpr = true, Purpose1 = true, Purpose3 = null, Purpose4 = false, Purpose7 = true, GoogleVendor = true, Source = "AppLovin MAX" };
            Assert.AreEqual("gdpr=True p1=yes p3=null p4=no p7=yes google=yes source=AppLovin MAX", state.ToString());
        }
    }
}
