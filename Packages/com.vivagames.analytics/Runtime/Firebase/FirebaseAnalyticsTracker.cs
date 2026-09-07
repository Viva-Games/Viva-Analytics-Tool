using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Tracker de Firebase Analytics. No hace falta editarlo: los parámetros comunes se registran
    /// con <see cref="AnalyticsService.RegisterCommonParameter"/> desde el AnalyticsInit del proyecto.
    /// Comprueba las dependencias de Firebase al inicializarse y guarda en cola los eventos que
    /// lleguen antes de que Firebase esté listo.
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

            // Comprueba las dependencias de Google Play Services y deja Firebase listo.
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    _firebaseFailed = true;
                    _pending.Clear();
                    Debug.LogError("[Analytics] Firebase dependency check failed: " + task.Exception);
                    return;
                }

                DependencyStatus status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    _firebaseReady = true;
                    Debug.Log("[Analytics] Firebase ready to use.");
                    FlushPendingUserData();
                    FlushPending();
                    RaiseFirebaseReady();
                }
                else
                {
                    _firebaseFailed = true;
                    _pending.Clear();
                    Debug.LogError($"[Analytics] Could not resolve all Firebase dependencies: {status}. Events will not be sent.");
                }
            });
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

        private void FlushPending()
        {
            while (_pending.Count > 0)
            {
                PendingEvent pending = _pending.Dequeue();
                global::Firebase.Analytics.FirebaseAnalytics.LogEvent(pending.EventKey, pending.Parameters);
            }
        }

        // Las propiedades de usuario van antes que los eventos en cola para que estos ya las lleven.
        private void FlushPendingUserData()
        {
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
