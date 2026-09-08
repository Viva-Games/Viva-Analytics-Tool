using System;
using System.Collections.Generic;
using Facebook.Unity;
using UnityEngine;
using Viva.Core;
using Viva.Services.Analytics.Consent;
using Viva.Services.Analytics.Facebook;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Tracker de Meta (Facebook App Events). Recibe solo los eventos marcados para Facebook en el Event
    /// Manager. Inicializa el SDK si el proyecto no lo ha hecho, llama a ActivateApp al arrancar y al
    /// reanudar, mantiene las banderas de tracking a false hasta que el consentimiento (VivaConsent o
    /// AnalyticsService.SetConsent) las permite, y encola lo que llegue antes de que el SDK esté listo.
    /// Compilado solo con VIVA_FACEBOOK (Facebook.Unity.dll en el proyecto).
    /// </summary>
    public class FacebookAnalyticsTracker : AAnalyticsTracker
    {
        private const int MAX_PENDING_EVENTS = 200;

        private bool _isInitialized;
        private bool _ready;
        private bool? _trackingAllowed;
        private readonly Queue<IAnalyticsEvent> _pending = new Queue<IAnalyticsEvent>();
        private string _pendingUserId;
        private bool _hasPendingUserId;
        private readonly HashSet<string> _warned = new HashSet<string>();

        /// <summary>Si es true, cada evento enviado se escribe también en la consola.</summary>
        public bool LogToConsole { get; set; }

        public bool IsReady => _ready;

        public override AnalyticsTargets Targets => AnalyticsTargets.Facebook;

        protected override void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            AnalyticsRuntime.GetOrCreate().ApplicationPaused += OnApplicationPaused;
            VivaConsent.Subscribe(OnSharedConsent);

            if (FB.IsInitialized)
            {
                OnFacebookReady();
                return;
            }

            try
            {
                FB.Init(OnFacebookReady);
            }
            catch (Exception e)
            {
                // Sin App ID en FacebookSettings, FB.Init lanza. El tracker se queda sin listo: los eventos se
                // encolan y se descartan al llenarse la cola; el resto de trackers no se ven afectados.
                Debug.LogError("[Analytics] Facebook could not initialize: " + e.Message + " Set the App ID in Facebook > Edit Settings.");
            }
        }

        protected override bool IsInitialized() => _isInitialized;

        private void OnFacebookReady()
        {
            _ready = true;
            // Las banderas quedan a false hasta que hay consentimiento (obligatorio en el EEE).
            ApplyTracking(_trackingAllowed ?? false);
            FB.ActivateApp();
            Debug.Log($"[Analytics] Facebook ready: sending {_pending.Count} queued event(s).");

            if (_hasPendingUserId)
            {
                SetMobileUserId(_pendingUserId);
                _hasPendingUserId = false;
                _pendingUserId = null;
            }
            while (_pending.Count > 0) Send(_pending.Dequeue());
        }

        private void OnApplicationPaused(bool paused)
        {
            if (!paused && FB.IsInitialized) FB.ActivateApp();
        }

        public override void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            if (!_ready)
            {
                if (_pending.Count >= MAX_PENDING_EVENTS) _pending.Dequeue();
                _pending.Enqueue(eventToTrack);
                return;
            }
            Send(eventToTrack);
        }

        private void Send(IAnalyticsEvent eventToTrack)
        {
            string name = eventToTrack.GetEventKey();
            string nameError = FacebookEventConverter.ValidateName(name);
            if (nameError != null)
            {
                WarnOnce(name, "not sent to Facebook: " + nameError);
                return;
            }

            var parameters = FacebookEventConverter.ToParameters(eventToTrack.GetTrackingFields(), out var dropped);
            if (dropped.Count > 0)
                WarnOnce(name + "#dropped", $"Facebook accepts {FacebookEventConverter.MAX_PARAMETERS} parameters: dropped {string.Join(", ", dropped)} from '{name}'.");

            FB.LogAppEvent(name, null, parameters);
            if (LogToConsole) Debug.Log("FACEBOOK " + ConsoleAnalyticsTracker.Format(eventToTrack));
        }

        public override void SetUserId(string userId)
        {
            if (_ready)
            {
                SetMobileUserId(userId);
            }
            else
            {
                _pendingUserId = userId;
                _hasPendingUserId = true;
            }
        }

        private void SetMobileUserId(string userId)
        {
            try
            {
                FB.Mobile.UserID = userId;
            }
            catch (InvalidOperationException)
            {
                // FB.Mobile solo existe en Android, iOS y el editor; en otras plataformas el SDK lanza.
                WarnOnce("#mobile", "FB.Mobile is not available on this platform: user id and tracking flags are skipped.");
            }
        }

        /// <summary>Consentimiento dado a mano al servicio (proyectos con su propio CMP): la señal AdStorage manda.</summary>
        public override void SetConsent(IReadOnlyDictionary<ConsentSignal, bool> signals)
        {
            if (signals != null && signals.TryGetValue(ConsentSignal.AdStorage, out bool adStorage))
            {
                _trackingAllowed = adStorage;
                if (_ready) ApplyTracking(adStorage);
            }
        }

        /// <summary>Consentimiento compartido por el core (Viva Ads tras el CMP de MAX): fuera de GDPR, o purpose 1 concedido.</summary>
        private void OnSharedConsent(ConsentState state)
        {
            _trackingAllowed = !state.IsGdpr || state.Purpose1 == true;
            if (_ready) ApplyTracking(_trackingAllowed.Value);
        }

        private void ApplyTracking(bool allowed)
        {
            try
            {
                FB.Mobile.SetAutoLogAppEventsEnabled(allowed);
                FB.Mobile.SetAdvertiserIDCollectionEnabled(allowed);
                FB.Mobile.SetAdvertiserTrackingEnabled(allowed);
                Debug.Log($"[Analytics] Facebook tracking flags: {(allowed ? "enabled" : "disabled")}.");
            }
            catch (InvalidOperationException)
            {
                WarnOnce("#mobile", "FB.Mobile is not available on this platform: user id and tracking flags are skipped.");
            }
        }

        private void WarnOnce(string key, string message)
        {
            if (_warned.Add(key)) Debug.LogWarning("[Analytics] " + message);
        }
    }
}
