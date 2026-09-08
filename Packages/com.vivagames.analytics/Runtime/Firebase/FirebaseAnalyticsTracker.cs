using System;
using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;
using Viva.Services.Analytics.Consent;
#if VIVA_FIREBASE
using Viva.Core;
#else
using Firebase.Extensions;
#endif

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Tracker de Firebase Analytics. No hace falta editarlo: los parámetros comunes se registran
    /// con <see cref="AnalyticsService.RegisterCommonParameter"/> desde el AnalyticsInit del proyecto.
    /// Espera a que VivaFirebase (Viva Core 2.1) resuelva las dependencias de Firebase, una sola vez
    /// para todos los módulos Viva, y guarda en cola los eventos que lleguen antes de que Firebase esté listo.
    /// Sin VIVA_FIREBASE (core anterior a 2.1, o antes de que su detector defina el símbolo) comprueba
    /// las dependencias por su cuenta, como en la 2.0.0, para que el proyecto compile en cualquier combinación.
    /// </summary>
    public class FirebaseAnalyticsTracker : AAnalyticsTracker
    {
        private const int MAX_PENDING_EVENTS = 200;

        private bool _isInitialized;
        private bool _firebaseReady;
        private bool _firebaseFailed;
        private readonly Queue<PendingEvent> _pending = new Queue<PendingEvent>();
        private readonly Dictionary<string, string> _pendingUserProperties = new Dictionary<string, string>();
        private string _pendingUserId;
        private bool _hasPendingUserId;
        private Dictionary<ConsentType, ConsentStatus> _pendingConsent;

        /// <summary>
        /// Si es true, cada evento se escribe también en la consola de Unity.
        /// </summary>
        public bool LogToConsole { get; set; } = true;

        /// <summary>
        /// true cuando Firebase ha resuelto sus dependencias y los eventos se envían de verdad.
        /// </summary>
        public bool IsFirebaseReady => _firebaseReady;

        /// <summary>
        /// Se dispara en el hilo principal cuando Firebase ha resuelto sus dependencias y ya se ha vaciado la cola.
        /// Es el sitio para lo que necesita Firebase listo: Crashlytics, consentimiento, otros productos de Firebase...
        /// </summary>
        public event Action FirebaseReady;

        protected override void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

#if VIVA_FIREBASE
            // La comprobación de dependencias de Firebase (Google Play Services) la hace VivaFirebase una sola
            // vez para todos los módulos Viva: el primero que la pide la arranca y el resto espera. El SDK no
            // admite dos comprobaciones a la vez, así que ningún módulo llama a CheckAndFixDependenciesAsync.
            VivaFirebase.EnsureInitialized("Viva Analytics");
            VivaFirebase.WhenReady(OnFirebaseAvailable, OnFirebaseUnavailable);
#else
            CheckDependenciesOnOurOwn();
#endif
        }

#if !VIVA_FIREBASE
        /// <summary>
        /// Comprobación propia, como en la 2.0.0. Solo se compila sin Viva Core 2.1: con otros módulos Viva
        /// que usen Firebase hace falta el core 2.1, para que la comprobación sea una sola.
        /// </summary>
        private void CheckDependenciesOnOurOwn()
        {
            Debug.Log("[Analytics] Checking Firebase dependencies (Viva Core 2.1 not detected, checking on our own)...");
            global::Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    OnFirebaseUnavailable("Firebase dependency check failed: " + task.Exception);
                    return;
                }

                var status = task.Result;
                if (status == global::Firebase.DependencyStatus.Available)
                    OnFirebaseAvailable();
                else
                    OnFirebaseUnavailable($"Could not resolve all Firebase dependencies: {status}.");
            });
        }
#endif

        // Hilo principal, una sola vez, cuando Firebase ha resuelto sus dependencias.
        private void OnFirebaseAvailable()
        {
            _firebaseReady = true;
            Debug.Log($"[Analytics] Firebase ready: sending {_pending.Count} queued event(s).");
            FlushPendingUserData();
            FlushPending();
            RaiseFirebaseReady();
        }

        private void OnFirebaseUnavailable(string reason)
        {
            _firebaseFailed = true;
            _pending.Clear();
            Debug.LogError("[Analytics] Firebase is not available, events will not be sent. " + reason);
        }

        protected override bool IsInitialized() => _isInitialized;

        public override void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            string eventKey = eventToTrack.GetEventKey();
            Parameter[] parameters = BuildParameters(eventToTrack.GetTrackingFields());

            if (LogToConsole)
            {
                Debug.Log(ConsoleAnalyticsTracker.Format(eventToTrack));
            }

            if (_firebaseReady)
            {
                global::Firebase.Analytics.FirebaseAnalytics.LogEvent(eventKey, parameters);
            }
            else if (!_firebaseFailed)
            {
                // Firebase todavía se está inicializando: el evento se envía en cuanto esté listo.
                if (_pending.Count >= MAX_PENDING_EVENTS) _pending.Dequeue();
                _pending.Enqueue(new PendingEvent(eventKey, parameters));
            }
        }

        public override void SetUserProperty(string name, string value)
        {
            if (_firebaseReady)
            {
                global::Firebase.Analytics.FirebaseAnalytics.SetUserProperty(name, value);
            }
            else if (!_firebaseFailed)
            {
                _pendingUserProperties[name] = value;
            }
        }

        public override void SetUserId(string userId)
        {
            if (_firebaseReady)
            {
                global::Firebase.Analytics.FirebaseAnalytics.SetUserId(userId);
            }
            else if (!_firebaseFailed)
            {
                _pendingUserId = userId;
                _hasPendingUserId = true;
            }
        }

        /// <summary>
        /// Google Consent Mode. Se traduce a FirebaseAnalytics.SetConsent y, si Firebase aún no está listo,
        /// se aplica en cuanto lo esté, antes que las propiedades de usuario y los eventos en cola.
        /// </summary>
        public override void SetConsent(IReadOnlyDictionary<ConsentSignal, bool> signals)
        {
            var consent = ToFirebaseConsent(signals);
            if (_firebaseReady)
            {
                global::Firebase.Analytics.FirebaseAnalytics.SetConsent(consent);
            }
            else if (!_firebaseFailed)
            {
                _pendingConsent = consent;
            }
        }

        private static Dictionary<ConsentType, ConsentStatus> ToFirebaseConsent(IReadOnlyDictionary<ConsentSignal, bool> signals)
        {
            var consent = new Dictionary<ConsentType, ConsentStatus>();
            foreach (var pair in signals)
            {
                ConsentType type;
                switch (pair.Key)
                {
                    case ConsentSignal.AnalyticsStorage: type = ConsentType.AnalyticsStorage; break;
                    case ConsentSignal.AdStorage: type = ConsentType.AdStorage; break;
                    case ConsentSignal.AdUserData: type = ConsentType.AdUserData; break;
                    case ConsentSignal.AdPersonalization: type = ConsentType.AdPersonalization; break;
                    default: continue;
                }
                consent[type] = pair.Value ? ConsentStatus.Granted : ConsentStatus.Denied;
            }
            return consent;
        }

        private void FlushPending()
        {
            while (_pending.Count > 0)
            {
                PendingEvent pending = _pending.Dequeue();
                global::Firebase.Analytics.FirebaseAnalytics.LogEvent(pending.EventKey, pending.Parameters);
            }
        }

        // Consentimiento y propiedades de usuario van antes que los eventos en cola para que estos ya los lleven.
        private void FlushPendingUserData()
        {
            if (_pendingConsent != null)
            {
                global::Firebase.Analytics.FirebaseAnalytics.SetConsent(_pendingConsent);
                _pendingConsent = null;
            }

            foreach (var pair in _pendingUserProperties)
            {
                global::Firebase.Analytics.FirebaseAnalytics.SetUserProperty(pair.Key, pair.Value);
            }
            _pendingUserProperties.Clear();

            if (_hasPendingUserId)
            {
                global::Firebase.Analytics.FirebaseAnalytics.SetUserId(_pendingUserId);
                _hasPendingUserId = false;
                _pendingUserId = null;
            }
        }

        private void RaiseFirebaseReady()
        {
            try
            {
                FirebaseReady?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError("[Analytics] FirebaseReady handler failed: " + e);
            }
        }

        private static Parameter[] BuildParameters(Dictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0) return Array.Empty<Parameter>();

            var parameters = new Parameter[fields.Count];
            int index = 0;
            foreach (var pair in fields)
            {
                parameters[index++] = ToParameter(pair.Key, pair.Value);
            }
            return parameters;
        }

        /// <summary>
        /// Firebase solo admite long, double y string: el resto de tipos se convierten.
        /// </summary>
        private static Parameter ToParameter(string key, object value)
        {
            switch (value)
            {
                case null:
                    return new Parameter(key, string.Empty);
                case int intValue:
                    return new Parameter(key, (long)intValue);
                case long longValue:
                    return new Parameter(key, longValue);
                case float floatValue:
                    return new Parameter(key, (double)floatValue);
                case double doubleValue:
                    return new Parameter(key, doubleValue);
                case bool boolValue:
                    return new Parameter(key, boolValue ? 1L : 0L);
                case string stringValue:
                    return new Parameter(key, stringValue);
                default:
                    return new Parameter(key, value.ToString());
            }
        }

        private readonly struct PendingEvent
        {
            public readonly string EventKey;
            public readonly Parameter[] Parameters;

            public PendingEvent(string eventKey, Parameter[] parameters)
            {
                EventKey = eventKey;
                Parameters = parameters;
            }
        }
    }
}
