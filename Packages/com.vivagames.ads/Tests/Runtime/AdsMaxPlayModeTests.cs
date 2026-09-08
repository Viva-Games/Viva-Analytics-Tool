using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Viva.Core;

namespace Viva.Services.Ads.Tests
{
    /// <summary>
    /// Flujo completo en Play Mode con el plugin de MAX real: en el editor el plugin simula el SDK (inicializa,
    /// carga al segundo y muestra anuncios de prueba con botones), así que se recorre inicialización,
    /// consentimiento publicado, carga, rewarded con recompensa, política de interstitial, interstitial y banner.
    /// Solo se compila con VIVA_APPLOVIN_MAX (plugin en el proyecto).
    /// </summary>
    public class AdsMaxPlayModeTests
    {
        private enum TestRewarded { Hint }
        private enum TestInterstitial { LevelStart }
        private enum TestBanner { Menu }

        [UnityTest]
        public IEnumerator FullFlow_WithTheMaxEditorStub()
        {
            AdsService.Reset();
            VivaConsent.Reset();
            AdsService.ProviderFactory = () => new MaxAdsProvider();

            var configuration = new AdUnitConfiguration
            {
                Rewarded = new AdUnitSetup("viva_test_rewarded", "viva_test_rewarded", typeof(TestRewarded), new[] { "hint" }),
                Interstitial = new AdUnitSetup("viva_test_interstitial", "viva_test_interstitial", typeof(TestInterstitial), new[] { "level_start" }),
                Banner = new BannerSetup("viva_test_banner", "viva_test_banner", typeof(TestBanner), new[] { "menu" }, BannerPosition.BottomCenter, true, Color.black),
            };
            int initialized = 0;
            AdsService.OnInitialized += () => initialized++;
            var statuses = new List<string>();
            AdsService.OnStatusChanged += (format, status) => statuses.Add($"{format}:{status}");

            try
            {
                AdsService.Initialize(configuration, new AdsOptions { InitializationTimeoutSeconds = 10f });
                Assert.AreEqual(AdsState.Initializing, AdsService.State);

                yield return WaitUntil(() => AdsService.State != AdsState.Initializing, 10f);
                Assert.AreEqual(AdsState.Ready, AdsService.State, "the MAX editor stub initializes in a fraction of a second");
                Assert.AreEqual(1, initialized);
                Assert.IsTrue(VivaConsent.IsResolved, "the consent is published even when the editor gives no TCF data");
                Assert.AreEqual("AppLovin MAX", VivaConsent.Current.Source);

                yield return WaitUntil(() => AdsService.RewardedStatus == AdStatus.Ready, 5f);
                yield return WaitUntil(() => AdsService.InterstitialStatus == AdStatus.Ready, 5f);
                Assert.AreEqual(AdStatus.Ready, AdsService.RewardedStatus, "rewarded loaded by the stub");
                Assert.AreEqual(AdStatus.Ready, AdsService.InterstitialStatus, "interstitial loaded by the stub");
                Assert.IsTrue(AdsService.IsRewardedReady);

                // Rewarded: el stub muestra un prefab con botones Reward y Close.
                RewardedResult? result = null;
                AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
                Assert.IsTrue(AdsService.IsShowingAd);
                Assert.AreEqual(AdStatus.Showing, AdsService.RewardedStatus);
                yield return null;
                Click("MaxRewardButton");
                Click("MaxRewardedCloseButton");
                yield return WaitUntil(() => result.HasValue, 3f);
                Assert.AreEqual(RewardedResult.Rewarded, result);
                Assert.IsFalse(AdsService.IsShowingAd);

                // Política: justo después de un rewarded completado, el primer TryShowInterstitial no muestra.
                Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart), "skipped after a completed rewarded");

                int closed = 0;
                Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart, () => closed++));
                Assert.IsTrue(AdsService.IsShowingAd);
                yield return null;
                Click("MaxInterstitialCloseButton");
                yield return WaitUntil(() => closed == 1, 3f);
                Assert.AreEqual(1, closed);
                Assert.IsFalse(AdsService.IsShowingAd);

                // Banner: creado al inicializar; mostrar y ocultar no lanzan.
                AdsService.ShowBanner(TestBanner.Menu);
                Assert.IsTrue(AdsService.IsBannerVisible);
                yield return null;
                AdsService.HideBanner();
                Assert.IsFalse(AdsService.IsBannerVisible);

                // Tras mostrarse, los formatos se recargan solos.
                yield return WaitUntil(() => AdsService.RewardedStatus == AdStatus.Ready && AdsService.InterstitialStatus == AdStatus.Ready, 5f);
                Assert.AreEqual(AdStatus.Ready, AdsService.RewardedStatus, "rewarded reloaded after being shown");
                Assert.AreEqual(AdStatus.Ready, AdsService.InterstitialStatus, "interstitial reloaded after being shown");

                CollectionAssert.Contains(statuses, "Rewarded:Showing");
                CollectionAssert.Contains(statuses, "Interstitial:Showing");
            }
            finally
            {
                AdsService.Reset();
                VivaConsent.Reset();
            }
        }

        private static void Click(string buttonObjectName)
        {
            var go = GameObject.Find(buttonObjectName);
            Assert.IsNotNull(go, $"the MAX editor stub should have shown '{buttonObjectName}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
