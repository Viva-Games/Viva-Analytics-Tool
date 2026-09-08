using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Viva.Core;

namespace Viva.Services.Ads.Tests
{
    /// <summary>
    /// Máquina de estados de AdsService con un proveedor falso y un reloj manual: inicialización, estados,
    /// reintentos, rewarded en todos sus caminos, política de interstitials, banner y consentimiento.
    /// </summary>
    public class AdsServiceTests
    {
        private enum TestRewarded { Hint, Continue }
        private enum TestInterstitial { LevelStart }
        private enum TestBanner { Menu }
        private enum Other { X }

        private FakeAdsProvider _provider;
        private ManualScheduler _clock;
        private bool _internet = true;
        private AdsOptions _options;

        [SetUp]
        public void SetUp()
        {
            AdsService.Reset();
            VivaConsent.Reset();
            _provider = new FakeAdsProvider();
            _clock = new ManualScheduler();
            _internet = true;
            _options = new AdsOptions { LogToConsole = false };
            AdsService.ConfigureForTests(_clock, () => _internet);
            AdsService.ProviderFactory = () => _provider;
        }

        [TearDown]
        public void TearDown()
        {
            AdsService.Reset();
            VivaConsent.Reset();
        }

        private static AdUnitConfiguration Configuration(bool banner = false) => new AdUnitConfiguration
        {
            Rewarded = new AdUnitSetup("rew-android", "rew-ios", typeof(TestRewarded), new[] { "hint", "continue" }),
            Interstitial = new AdUnitSetup("int-android", "", typeof(TestInterstitial), new[] { "level_start" }),
            Banner = banner ? new BannerSetup("ban-android", "", typeof(TestBanner), new[] { "menu" }, BannerPosition.BottomCenter, true, Color.black) : null,
        };

        private void InitializeReady(bool banner = false)
        {
            AdsService.Initialize(Configuration(banner), _options);
            _provider.SdkReady();
        }

        private static AdsConsentInfo GdprConsent(bool all) => new AdsConsentInfo
        {
            IsGdpr = true, Geography = "Gdpr", Purpose1 = all, Purpose3 = all, Purpose4 = all, Purpose7 = all, GoogleVendor = all, AppTrackingStatus = "Authorized"
        };

        #region Initialization

        [Test]
        public void Initialize_SetsFormatsAndAsksTheProvider()
        {
            AdsService.Initialize(Configuration(), _options);

            Assert.AreEqual(AdsState.Initializing, AdsService.State);
            Assert.AreEqual(AdStatus.NotInitialized, AdsService.RewardedStatus);
            Assert.AreEqual(AdStatus.NotInitialized, AdsService.InterstitialStatus);
            Assert.AreEqual(AdStatus.Disabled, AdsService.BannerStatus);
            CollectionAssert.AreEqual(new[] { "Initialize" }, _provider.Calls);
            Assert.AreSame(_options, _provider.Options);
            Assert.IsTrue(AdsService.IsInitialized);
            Assert.IsFalse(AdsService.IsReady);
        }

        [Test]
        public void SdkReady_StateReady_OnInitializedOnce_LoadsRequested()
        {
            int initialized = 0;
            AdsService.OnInitialized += () => initialized++;
            AdsService.Initialize(Configuration(), _options);
            Assert.AreEqual(0, initialized);

            _provider.SdkReady();

            Assert.AreEqual(AdsState.Ready, AdsService.State);
            Assert.AreEqual(1, initialized);
            Assert.AreEqual(1, _provider.CallCount("LoadRewarded"));
            Assert.AreEqual(1, _provider.CallCount("LoadInterstitial"));
            Assert.AreEqual(0, _provider.CallCount("CreateBanner"));
            Assert.AreEqual(AdStatus.Loading, AdsService.RewardedStatus);

            bool late = false;
            AdsService.WhenInitialized(() => late = true);
            Assert.IsTrue(late, "WhenInitialized runs at once after the fact");
            Assert.AreEqual(0, _clock.PendingCount, "the timeout was cancelled");
        }

        [Test]
        public void Timeout_MarksUnavailable_StillLoads_AndRecoversWhenTheSdkArrives()
        {
            int initialized = 0;
            AdsService.OnInitialized += () => initialized++;
            _options.InitializationTimeoutSeconds = 30f;
            AdsService.Initialize(Configuration(), _options);

            LogAssert.Expect(LogType.Warning, new Regex("did not finish initializing"));
            _clock.Advance(30f);

            Assert.AreEqual(AdsState.Unavailable, AdsService.State);
            Assert.AreEqual(1, initialized);
            Assert.AreEqual(1, _provider.CallCount("LoadRewarded"), "loads are requested anyway, they retry when the SDK is ready");

            _provider.SdkReady();
            Assert.AreEqual(AdsState.Ready, AdsService.State);
            Assert.AreEqual(1, initialized, "OnInitialized fires once");
            Assert.AreEqual(1, _provider.CallCount("LoadRewarded"), "no duplicated loads");
        }

        [Test]
        public void NoInternet_DoesNotWaitForTheSdk()
        {
            _internet = false;
            AdsService.Initialize(Configuration(), _options);
            Assert.AreEqual(AdsState.Initializing, AdsService.State);

            LogAssert.Expect(LogType.Warning, new Regex("did not finish initializing"));
            _clock.RunDue();

            Assert.AreEqual(AdsState.Unavailable, AdsService.State);
        }

        [Test]
        public void ProviderReportsFailure_Unavailable_OnInitializedOnce()
        {
            int initialized = 0;
            AdsService.OnInitialized += () => initialized++;
            AdsService.Initialize(Configuration(), _options);

            _provider.InitializationFailed("no sdk");

            Assert.AreEqual(AdsState.Unavailable, AdsService.State);
            Assert.AreEqual(1, initialized);
            Assert.AreEqual(0, _provider.CallCount("LoadRewarded"));
        }

        [Test]
        public void ProviderThrowsOnInitialize_Unavailable()
        {
            _provider.ThrowOnInitialize = true;
            LogAssert.Expect(LogType.Error, new Regex("provider failed to initialize"));
            AdsService.Initialize(Configuration(), _options);
            Assert.AreEqual(AdsState.Unavailable, AdsService.State);
        }

        [Test]
        public void Initialize_Twice_IsIgnoredWithAWarning()
        {
            AdsService.Initialize(Configuration(), _options);
            LogAssert.Expect(LogType.Warning, new Regex("Already initialized"));
            AdsService.Initialize(Configuration(), _options);
            Assert.AreEqual(1, _provider.CallCount("Initialize"));
        }

        [Test]
        public void NoProviderFactory_UsesNoAdsProvider()
        {
            AdsService.ProviderFactory = null;
            LogAssert.Expect(LogType.Warning, new Regex("SDK not found"));
            AdsService.Initialize(Configuration(), _options);

            Assert.AreEqual(AdsState.Unavailable, AdsService.State);
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.NotInitialized, result);
        }

        [Test]
        public void Consent_IsPublishedToVivaConsent_AndRaised()
        {
            AdsConsentInfo received = null;
            AdsService.OnConsentResolved += c => received = c;
            AdsService.Initialize(Configuration(), _options);

            _provider.SdkReady(GdprConsent(true));

            Assert.IsNotNull(received);
            Assert.IsTrue(VivaConsent.IsResolved);
            Assert.IsTrue(VivaConsent.Current.IsGdpr);
            Assert.AreEqual(true, VivaConsent.Current.Purpose7);
            Assert.AreEqual("AppLovin MAX", VivaConsent.Current.Source);

            _provider.ConsentChanged(GdprConsent(false));
            Assert.AreEqual(false, VivaConsent.Current.Purpose1);
            Assert.IsFalse(received.AdStorageGranted);
        }

        #endregion

        #region Status and retries

        [Test]
        public void LoadFailed_NoNetwork_StatusNoInternet_RetriesWithExponentialBackoff()
        {
            InitializeReady();
            var expected = new List<float> { 2, 4, 8, 16, 32, 64, 64 };

            foreach (float delay in expected)
            {
                _provider.LoadFailed(AdFormat.Rewarded, AdLoadError.NoNetwork);
                Assert.AreEqual(AdStatus.NoInternet, AdsService.RewardedStatus);
                Assert.AreEqual(delay, _clock.NextDelay, "retry delay");
                int loadsBefore = _provider.CallCount("LoadRewarded");
                _clock.Advance(delay);
                Assert.AreEqual(loadsBefore + 1, _provider.CallCount("LoadRewarded"));
            }

            _provider.Loaded(AdFormat.Rewarded);
            Assert.AreEqual(AdStatus.Ready, AdsService.RewardedStatus);
            _provider.LoadFailed(AdFormat.Rewarded, AdLoadError.NoFill);
            Assert.AreEqual(2f, _clock.NextDelay, "the retry counter resets after a load");
        }

        [Test]
        public void LoadFailed_NoFill_StatusLoading()
        {
            InitializeReady();
            _provider.LoadFailed(AdFormat.Rewarded, AdLoadError.NoFill);
            Assert.AreEqual(AdStatus.Loading, AdsService.RewardedStatus);
        }

        [Test]
        public void LoadFailed_WithoutConnectivity_StatusNoInternetWhateverTheError()
        {
            InitializeReady();
            _internet = false;
            _provider.LoadFailed(AdFormat.Rewarded, AdLoadError.NoFill);
            Assert.AreEqual(AdStatus.NoInternet, AdsService.RewardedStatus);
        }

        [Test]
        public void StatusChanges_AreRaisedInOrder()
        {
            var log = new List<string>();
            AdsService.OnStatusChanged += (format, status) => { if (format == AdFormat.Rewarded) log.Add(status.ToString()); };
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            AdsService.ShowRewarded(TestRewarded.Hint, _ => { });
            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);
            _provider.Loaded(AdFormat.Rewarded);

            CollectionAssert.AreEqual(new[] { "Loading", "Ready", "Showing", "Loading", "Ready" }, log);
        }

        #endregion

        #region Rewarded

        [Test]
        public void ShowRewarded_InvalidPlacement()
        {
            InitializeReady();
            RewardedResult? result = null;

            LogAssert.Expect(LogType.Error, new Regex("not a rewarded placement"));
            AdsService.ShowRewarded(Other.X, r => result = r);
            Assert.AreEqual(RewardedResult.InvalidPlacement, result);

            LogAssert.Expect(LogType.Error, new Regex("not a rewarded placement"));
            AdsService.ShowRewarded(TestInterstitial.LevelStart, r => result = r);
            Assert.AreEqual(RewardedResult.InvalidPlacement, result);

            AdsService.ShowRewarded("", r => result = r);
            Assert.AreEqual(RewardedResult.InvalidPlacement, result);
        }

        [Test]
        public void ShowRewarded_BeforeTheSdkIsReady_NotInitialized()
        {
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.NotInitialized, result, "before Initialize, without console errors");

            AdsService.Initialize(Configuration(), _options);
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.NotInitialized, result);
        }

        [Test]
        public void ShowRewarded_NotLoaded_NotReadyOrNoInternet()
        {
            InitializeReady();
            RewardedResult? result = null;

            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.NotReady, result);

            _provider.LoadFailed(AdFormat.Rewarded, AdLoadError.NoNetwork);
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.NoInternet, result);

            _provider.Loaded(AdFormat.Rewarded);
            _provider.RewardedReady = false; // el SDK dice que no, aunque el estado fuera Ready
            _internet = false;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.NoInternet, result);
        }

        [Test]
        public void ShowRewarded_RewardThenHidden_Rewarded_AndReloads()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            var showing = new List<bool>();
            AdsService.OnShowingAdChanged += s => showing.Add(s);
            RewardedResult? result = null;

            AdsService.ShowRewarded(TestRewarded.Continue, r => result = r);

            Assert.IsTrue(AdsService.IsShowingAd);
            Assert.AreEqual(AdStatus.Showing, AdsService.RewardedStatus);
            CollectionAssert.Contains(_provider.Calls, "ShowRewarded:continue");
            Assert.IsNull(result);

            _provider.Displayed(AdFormat.Rewarded);
            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);

            Assert.AreEqual(RewardedResult.Rewarded, result);
            Assert.IsFalse(AdsService.IsShowingAd);
            Assert.AreEqual(AdStatus.Loading, AdsService.RewardedStatus);
            Assert.AreEqual(2, _provider.CallCount("LoadRewarded"), "reloaded after closing");
            CollectionAssert.AreEqual(new[] { true, false }, showing);
        }

        [Test]
        public void ShowRewarded_HiddenThenLateReward_WithinGrace_Rewarded()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);

            _provider.Hidden(AdFormat.Rewarded);
            Assert.IsNull(result, "waiting the grace period for a late reward");
            _clock.Advance(0.2f);
            _provider.Reward();

            Assert.AreEqual(RewardedResult.Rewarded, result);
        }

        [Test]
        public void ShowRewarded_HiddenWithoutReward_AfterGrace_NotRewarded()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);

            _provider.Hidden(AdFormat.Rewarded);
            _clock.Advance(0.5f);

            Assert.AreEqual(RewardedResult.NotRewarded, result);
        }

        [Test]
        public void ShowRewarded_DisplayFailed()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);

            LogAssert.Expect(LogType.Warning, new Regex("display failed"));
            _provider.DisplayFailed(AdFormat.Rewarded);

            Assert.AreEqual(RewardedResult.DisplayFailed, result);
            Assert.IsFalse(AdsService.IsShowingAd);
            Assert.AreEqual(2, _provider.CallCount("LoadRewarded"));
        }

        [Test]
        public void ShowRewarded_WhileAnotherAdShows_AlreadyShowing()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            AdsService.ShowRewarded(TestRewarded.Hint, _ => { });

            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);
            Assert.AreEqual(RewardedResult.AlreadyShowing, result);
        }

        [Test]
        public void ShowRewarded_ProviderThrows_DisplayFailed_AndNothingStaysBlocked()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            _provider.ThrowOnShowRewarded = true;
            RewardedResult? result = null;

            LogAssert.Expect(LogType.Error, new Regex("ShowRewarded failed"));
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);

            Assert.AreEqual(RewardedResult.DisplayFailed, result);
            Assert.IsFalse(AdsService.IsShowingAd);
        }

        [Test]
        public void ResumeWithoutCloseCallback_WatchdogEndsTheAd()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r);

            AdsService.HandleApplicationPause(true);
            AdsService.HandleApplicationPause(false);
            Assert.IsNull(result);

            LogAssert.Expect(LogType.Warning, new Regex("No close callback"));
            _clock.Advance(1f);

            Assert.AreEqual(RewardedResult.DisplayFailed, result);
            Assert.IsFalse(AdsService.IsShowingAd);
        }

        [Test]
        public void ResumeAndThenTheSdkCloses_NoDoubleResult()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            int results = 0;
            AdsService.ShowRewarded(TestRewarded.Hint, _ => results++);

            AdsService.HandleApplicationPause(true);
            AdsService.HandleApplicationPause(false);
            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);
            _clock.Advance(1f);

            Assert.AreEqual(1, results);
        }

        [Test]
        public void WaitForLoad_ShowsWhenTheAdArrivesInTime()
        {
            InitializeReady();
            var waiting = new List<bool>();
            AdsService.OnWaitingForRewarded += w => waiting.Add(w);
            RewardedResult? result = null;

            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r, waitForLoadSeconds: 5f);
            Assert.IsNull(result);
            CollectionAssert.AreEqual(new[] { true }, waiting);

            _clock.Advance(2f);
            _provider.Loaded(AdFormat.Rewarded);

            CollectionAssert.AreEqual(new[] { true, false }, waiting);
            CollectionAssert.Contains(_provider.Calls, "ShowRewarded:hint");
            Assert.IsTrue(AdsService.IsShowingAd);

            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);
            Assert.AreEqual(RewardedResult.Rewarded, result);
        }

        [Test]
        public void WaitForLoad_Expires_NotReady()
        {
            InitializeReady();
            RewardedResult? result = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r, waitForLoadSeconds: 5f);

            RewardedResult? second = null;
            AdsService.ShowRewarded(TestRewarded.Hint, r => second = r, waitForLoadSeconds: 5f);
            Assert.AreEqual(RewardedResult.AlreadyShowing, second, "a second call while waiting");

            _clock.Advance(5f);
            Assert.AreEqual(RewardedResult.NotReady, result);
        }

        [Test]
        public void WaitForLoad_WithoutInternet_DoesNotWait()
        {
            InitializeReady();
            _provider.LoadFailed(AdFormat.Rewarded, AdLoadError.NoNetwork);
            RewardedResult? result = null;

            AdsService.ShowRewarded(TestRewarded.Hint, r => result = r, waitForLoadSeconds: 5f);

            Assert.AreEqual(RewardedResult.NoInternet, result);
        }

        #endregion

        #region Interstitial

        [Test]
        public void TryShowInterstitial_LaunchesAndCallsOnClosedOnHidden()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Interstitial);
            int closed = 0;

            Assert.IsTrue(AdsService.CanShowInterstitial);
            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart, () => closed++));
            CollectionAssert.Contains(_provider.Calls, "ShowInterstitial:level_start");
            Assert.AreEqual(0, closed);

            _provider.Hidden(AdFormat.Interstitial);

            Assert.AreEqual(1, closed);
            Assert.AreEqual(2, _provider.CallCount("LoadInterstitial"));
        }

        [Test]
        public void TryShowInterstitial_DisplayFailed_StillCallsOnClosed()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Interstitial);
            int closed = 0;
            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart, () => closed++));

            LogAssert.Expect(LogType.Warning, new Regex("display failed"));
            _provider.DisplayFailed(AdFormat.Interstitial);

            Assert.AreEqual(1, closed);
        }

        [Test]
        public void TryShowInterstitial_NotReady_False_WithoutOnClosed()
        {
            InitializeReady();
            int closed = 0;
            Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart, () => closed++));
            Assert.AreEqual(0, closed);
        }

        [Test]
        public void TryShowInterstitial_InvalidPlacement_False()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Interstitial);
            LogAssert.Expect(LogType.Error, new Regex("not an interstitial placement"));
            Assert.IsFalse(AdsService.TryShowInterstitial(TestRewarded.Hint));
        }

        [Test]
        public void Cooldown_CountsFromInitialize_AndRestartsAfterAnInterstitial()
        {
            _options.InterstitialCooldownSeconds = 100f;
            InitializeReady();
            _provider.Loaded(AdFormat.Interstitial);

            Assert.AreEqual(100f, AdsService.InterstitialCooldownRemaining);
            Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));

            _clock.Advance(100f);
            Assert.AreEqual(0f, AdsService.InterstitialCooldownRemaining);
            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));
            _provider.Hidden(AdFormat.Interstitial);
            _provider.Loaded(AdFormat.Interstitial);

            Assert.AreEqual(100f, AdsService.InterstitialCooldownRemaining, "restarted when the interstitial closed");
            Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));
        }

        [Test]
        public void Cooldown_NotStartingAtInitialize_AllowsTheFirstOne()
        {
            _options.InterstitialCooldownSeconds = 100f;
            _options.InterstitialCooldownStartsAtInitialize = false;
            InitializeReady();
            _provider.Loaded(AdFormat.Interstitial);

            Assert.AreEqual(0f, AdsService.InterstitialCooldownRemaining);
            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));
        }

        [Test]
        public void SkipNextInterstitialAfterRewarded_IsConsumedByTheNextCheck()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            _provider.Loaded(AdFormat.Interstitial);

            AdsService.ShowRewarded(TestRewarded.Hint, _ => { });
            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);

            Assert.IsFalse(AdsService.CanShowInterstitial);
            Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart), "skipped once after a rewarded");
            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart), "the mark was consumed");
        }

        [Test]
        public void SkipNextInterstitial_NotSetWhenTheRewardedWasNotCompleted()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            _provider.Loaded(AdFormat.Interstitial);

            AdsService.ShowRewarded(TestRewarded.Hint, _ => { });
            _provider.Hidden(AdFormat.Rewarded);
            _clock.Advance(0.5f);

            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));
        }

        [Test]
        public void SkipNextInterstitial_CanBeDisabled_OrForcedByTheGame()
        {
            _options.SkipNextInterstitialAfterRewarded = false;
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            _provider.Loaded(AdFormat.Interstitial);

            AdsService.ShowRewarded(TestRewarded.Hint, _ => { });
            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);
            Assert.IsTrue(AdsService.CanShowInterstitial);

            AdsService.SkipNextInterstitial();
            Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));
            Assert.IsTrue(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));
        }

        [Test]
        public void InterstitialsDisabled_NeverShowsThroughThePolicy()
        {
            _options.InterstitialsEnabled = false;
            InitializeReady();
            _provider.Loaded(AdFormat.Interstitial);

            Assert.IsFalse(AdsService.TryShowInterstitial(TestInterstitial.LevelStart));

            InterstitialResult? result = null;
            AdsService.ShowInterstitial(TestInterstitial.LevelStart, r => result = r);
            Assert.IsTrue(AdsService.IsShowingAd, "ShowInterstitial ignores the policy");
            _provider.Hidden(AdFormat.Interstitial);
            Assert.AreEqual(InterstitialResult.Closed, result);
        }

        [Test]
        public void ShowInterstitial_Results()
        {
            InterstitialResult? result = null;
            AdsService.Initialize(Configuration(), _options);
            AdsService.ShowInterstitial(TestInterstitial.LevelStart, r => result = r);
            Assert.AreEqual(InterstitialResult.NotInitialized, result);

            _provider.SdkReady();
            AdsService.ShowInterstitial(TestInterstitial.LevelStart, r => result = r);
            Assert.AreEqual(InterstitialResult.NotReady, result);

            _provider.LoadFailed(AdFormat.Interstitial, AdLoadError.NoNetwork);
            AdsService.ShowInterstitial(TestInterstitial.LevelStart, r => result = r);
            Assert.AreEqual(InterstitialResult.NoInternet, result);

            LogAssert.Expect(LogType.Error, new Regex("not an interstitial placement"));
            AdsService.ShowInterstitial(Other.X, r => result = r);
            Assert.AreEqual(InterstitialResult.InvalidPlacement, result);

            _provider.Loaded(AdFormat.Interstitial);
            _provider.Loaded(AdFormat.Rewarded);
            AdsService.ShowRewarded(TestRewarded.Hint, _ => { });
            AdsService.ShowInterstitial(TestInterstitial.LevelStart, r => result = r);
            Assert.AreEqual(InterstitialResult.AlreadyShowing, result);
        }

        #endregion

        #region Banner

        [Test]
        public void Banner_CreatedOnReady_ShowHidePosition()
        {
            InitializeReady(banner: true);

            Assert.AreEqual(1, _provider.CallCount("CreateBanner"));
            Assert.AreEqual(AdStatus.Loading, AdsService.BannerStatus);

            _provider.Loaded(AdFormat.Banner);
            Assert.AreEqual(AdStatus.Ready, AdsService.BannerStatus);

            AdsService.ShowBanner(TestBanner.Menu);
            Assert.IsTrue(AdsService.IsBannerVisible);
            CollectionAssert.Contains(_provider.Calls, "ShowBanner:menu");

            AdsService.SetBannerPosition(BannerPosition.TopCenter);
            CollectionAssert.Contains(_provider.Calls, "SetBannerPosition:TopCenter");

            AdsService.HideBanner();
            Assert.IsFalse(AdsService.IsBannerVisible);
            CollectionAssert.Contains(_provider.Calls, "HideBanner");

            _provider.LoadFailed(AdFormat.Banner, AdLoadError.NoFill);
            Assert.AreEqual(0, _clock.PendingCount, "the SDK refreshes banners on its own; no retry scheduled");
        }

        [Test]
        public void Banner_Disabled_CallsAreIgnored()
        {
            InitializeReady(banner: false);
            AdsService.ShowBanner("menu");
            AdsService.HideBanner();
            Assert.IsFalse(AdsService.IsBannerVisible);
            Assert.AreEqual(0, _provider.CallCount("ShowBanner"));
        }

        #endregion

        #region Consent dialog, revenue, misc

        [Test]
        public void ConsentDialog_OnlyWhenReady()
        {
            bool? completed = null;
            AdsService.ShowConsentDialog(ok => completed = ok);
            Assert.AreEqual(false, completed, "not initialized");

            InitializeReady();
            Assert.IsTrue(AdsService.HasConsentDialog);
            AdsService.ShowConsentDialog(ok => completed = ok);
            Assert.IsNotNull(_provider.PendingConsentDialog);
            _provider.PendingConsentDialog(true);
            Assert.AreEqual(true, completed);
        }

        [Test]
        public void Revenue_IsForwarded()
        {
            AdRevenueInfo received = null;
            AdsService.OnAdRevenue += r => received = r;
            InitializeReady();

            AdRevenueEvent shared = null;
            VivaAdRevenue.Reset();
            VivaAdRevenue.Subscribe(r => shared = r);

            _provider.Revenue(new AdRevenueInfo { Format = AdFormat.Rewarded, FormatName = "REWARDED", Placement = "hint", Revenue = 0.01, NetworkName = "AdMob", RevenuePrecision = "exact" });

            Assert.IsNotNull(received);
            Assert.AreEqual("hint", received.Placement);
            Assert.IsNotNull(shared, "also published through Viva Core for the modules that attribute revenue");
            Assert.AreEqual("REWARDED", shared.Format);
            Assert.AreEqual("AdMob", shared.NetworkName);
            Assert.AreEqual(0.01, shared.Revenue);
            Assert.AreEqual("USD", shared.Currency);
            VivaAdRevenue.Reset();
        }

        [Test]
        public void CallbackException_DoesNotBreakTheService()
        {
            InitializeReady();
            _provider.Loaded(AdFormat.Rewarded);
            AdsService.ShowRewarded(TestRewarded.Hint, _ => throw new InvalidOperationException("game code broke"));

            LogAssert.Expect(LogType.Error, new Regex("callback failed"));
            _provider.Reward();
            _provider.Hidden(AdFormat.Rewarded);

            Assert.IsFalse(AdsService.IsShowingAd);
            Assert.AreEqual(2, _provider.CallCount("LoadRewarded"));
        }

        [Test]
        public void Configuration_ResolvesPlacementsByEnumAndOrdinal()
        {
            var setup = Configuration().Rewarded;
            Assert.IsTrue(setup.TryResolvePlacement(TestRewarded.Continue, out string name));
            Assert.AreEqual("continue", name);
            Assert.IsFalse(setup.TryResolvePlacement(Other.X, out _));
            Assert.IsFalse(setup.TryResolvePlacement(null, out _));
            Assert.AreEqual("rew-android", setup.AdUnitId);
            CollectionAssert.AreEqual(new[] { "rew-android", "int-android" }, Configuration().EnabledAdUnitIds());
        }

        #endregion
    }
}
