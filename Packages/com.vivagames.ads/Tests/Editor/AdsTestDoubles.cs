using System;
using System.Collections.Generic;

namespace Viva.Services.Ads.Tests
{
    /// <summary>Proveedor controlado desde el test: registra lo que le pide el servicio y simula los avisos del SDK.</summary>
    internal sealed class FakeAdsProvider : IAdsProvider
    {
        public IAdsListener Listener;
        public AdUnitConfiguration Configuration;
        public AdsOptions Options;
        public readonly List<string> Calls = new List<string>();
        public bool RewardedReady;
        public bool InterstitialReady;
        public bool ThrowOnShowRewarded;
        public bool ThrowOnInitialize;
        public bool HasConsentDialog { get; set; } = true;
        public Action<bool> PendingConsentDialog;

        public int CallCount(string call)
        {
            int count = 0;
            foreach (var c in Calls) if (c == call || c.StartsWith(call + ":", StringComparison.Ordinal)) count++;
            return count;
        }

        public void Initialize(AdUnitConfiguration configuration, AdsOptions options, IAdsListener listener)
        {
            Calls.Add("Initialize");
            Configuration = configuration;
            Options = options;
            Listener = listener;
            if (ThrowOnInitialize) throw new InvalidOperationException("provider exploded");
        }

        public void LoadRewarded() => Calls.Add("LoadRewarded");
        public bool IsRewardedReady => RewardedReady;

        public void ShowRewarded(string placement)
        {
            Calls.Add("ShowRewarded:" + placement);
            RewardedReady = false;
            if (ThrowOnShowRewarded) throw new InvalidOperationException("show exploded");
        }

        public void LoadInterstitial() => Calls.Add("LoadInterstitial");
        public bool IsInterstitialReady => InterstitialReady;

        public void ShowInterstitial(string placement)
        {
            Calls.Add("ShowInterstitial:" + placement);
            InterstitialReady = false;
        }

        public void CreateBanner() => Calls.Add("CreateBanner");
        public void ShowBanner(string placement) => Calls.Add("ShowBanner:" + placement);
        public void HideBanner() => Calls.Add("HideBanner");
        public void SetBannerPosition(BannerPosition position) => Calls.Add("SetBannerPosition:" + position);

        public void ShowConsentDialog(Action<bool> onCompleted)
        {
            Calls.Add("ShowConsentDialog");
            PendingConsentDialog = onCompleted;
        }

        public void ShowMediationDebugger() => Calls.Add("ShowMediationDebugger");

        // ---- simulación del SDK ----

        public void SdkReady(AdsConsentInfo consent = null) => Listener.OnSdkInitialized(consent);
        public void InitializationFailed(string reason) => Listener.OnInitializationFailed(reason);

        public void Loaded(AdFormat format)
        {
            if (format == AdFormat.Rewarded) RewardedReady = true;
            if (format == AdFormat.Interstitial) InterstitialReady = true;
            Listener.OnAdLoaded(format);
        }

        public void LoadFailed(AdFormat format, AdLoadError error) => Listener.OnAdLoadFailed(format, error, error.ToString());
        public void Displayed(AdFormat format) => Listener.OnAdDisplayed(format);
        public void DisplayFailed(AdFormat format) => Listener.OnAdDisplayFailed(format, "display failed");
        public void Hidden(AdFormat format) => Listener.OnAdHidden(format);
        public void Reward() => Listener.OnRewardReceived();
        public void Revenue(AdRevenueInfo info) => Listener.OnAdRevenue(info);
        public void ConsentChanged(AdsConsentInfo consent) => Listener.OnConsentChanged(consent);
    }

    /// <summary>Planificador manual: el test decide cuánto tiempo pasa.</summary>
    internal sealed class ManualScheduler : IAdsScheduler
    {
        private sealed class Entry
        {
            public float Due;
            public Action Action;
            public bool Cancelled;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public float Now { get; private set; }

        public int PendingCount
        {
            get
            {
                int count = 0;
                foreach (var entry in _entries) if (!entry.Cancelled) count++;
                return count;
            }
        }

        /// <summary>Segundos que faltan para la próxima acción pendiente, o -1 si no hay.</summary>
        public float NextDelay
        {
            get
            {
                float best = -1f;
                foreach (var entry in _entries)
                {
                    if (entry.Cancelled) continue;
                    float remaining = entry.Due - Now;
                    if (best < 0f || remaining < best) best = remaining;
                }
                return best;
            }
        }

        public object Delay(float seconds, Action action)
        {
            var entry = new Entry { Due = Now + Math.Max(0f, seconds), Action = action };
            _entries.Add(entry);
            return entry;
        }

        public void Cancel(object handle)
        {
            if (handle is Entry entry) entry.Cancelled = true;
        }

        /// <summary>Avanza el reloj y ejecuta lo que venza, en orden.</summary>
        public void Advance(float seconds)
        {
            Now += seconds;
            RunDue();
        }

        /// <summary>Ejecuta lo que ya haya vencido (incluidas las esperas de 0 segundos).</summary>
        public void RunDue()
        {
            while (true)
            {
                Entry next = null;
                foreach (var entry in _entries)
                {
                    if (entry.Cancelled || entry.Due > Now) continue;
                    if (next == null || entry.Due < next.Due) next = entry;
                }
                if (next == null) break;
                _entries.Remove(next);
                next.Action();
            }
            _entries.RemoveAll(e => e.Cancelled);
        }
    }
}
