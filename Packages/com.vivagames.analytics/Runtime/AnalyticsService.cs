using System;
using System.Collections.Generic;
using UnityEngine;
using Viva.Core;
using Viva.Services.Analytics.Consent;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Punto de entrada del servicio de analíticas.
    /// Reparte cada evento entre los trackers registrados (Firebase, consola y, en el futuro, Singular, Facebook...)
    /// añadiendo antes los parámetros comunes registrados con <see cref="RegisterCommonParameter"/>.
    /// </summary>
    public static class AnalyticsService
    {
        private const string FTUE_PREF_KEY = "viva_analytics_last_ftue_tracked";
        private const string DEFAULT_FTUE_EVENT_NAME = "ftue_landmark";
        private const string DEFAULT_FTUE_STEP_PARAMETER_NAME = "ftue_step_number";

        private static readonly List<AAnalyticsTracker> _trackers = new List<AAnalyticsTracker>();
        private static readonly Dictionary<string, Func<object>> _commonParameters = new Dictionary<string, Func<object>>();
        private static readonly Dictionary<string, string> _userProperties = new Dictionary<string, string>();
        private static string _userId;
        private static bool _hasUserId;
        private static Dictionary<ConsentSignal, bool> _consent;

        // Se crea una sola vez para no generar basura en cada paso del FTUE.
        private static readonly EV_FTUE _ftueEvent = new EV_FTUE();

        internal static AnalyticsConfig Config;

        /// <summary>
        /// true si hay al menos un tracker inicializado.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                for (int i = 0; i < _trackers.Count; i++)
                {
                    if (_trackers[i].IsInitialized()) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Trackers registrados actualmente.
        /// </summary>
        public static IReadOnlyList<AAnalyticsTracker> Trackers => _trackers;

        /// <summary>
        /// Claves de los parámetros comunes registrados.
        /// </summary>
        public static IEnumerable<string> CommonParameterKeys => _commonParameters.Keys;

        /// <summary>
        /// Inicializa el servicio con uno o varios trackers y la configuración de FTUE por defecto.
        /// Cada evento se enviará a todos los trackers.
        /// </summary>
        public static void Initialize(params AAnalyticsTracker[] trackers)
        {
            Initialize(DefaultConfig(), trackers);
        }

        /// <summary>
        /// Inicializa el servicio con la configuración dada, usando <see cref="AnalyticsConfig.EventTracker"/> como tracker.
        /// </summary>
        public static void Initialize(AnalyticsConfig config)
        {
            if (config.EventTracker == null)
            {
                throw new ArgumentNullException(nameof(config.EventTracker), "[Analytics] The Analytics tracker cannot be null.");
            }

            Initialize(config, config.EventTracker);
        }

        /// <summary>
        /// Inicializa el servicio con la configuración dada y uno o varios trackers.
        /// </summary>
        public static void Initialize(AnalyticsConfig config, params AAnalyticsTracker[] trackers)
        {
            if (trackers == null || trackers.Length == 0)
            {
                throw new ArgumentException("[Analytics] At least one tracker is required.", nameof(trackers));
            }

            if (string.IsNullOrWhiteSpace(config.FtueEventName) || string.IsNullOrWhiteSpace(config.FtueStepParameterName))
            {
                Debug.LogWarning("[Analytics] FTUE event name and step parameter name not properly configured. FTUE steps won't be tracked.");
            }

            Config = config;
            Config.EventTracker = trackers[0];

            for (int i = 0; i < trackers.Length; i++)
            {
                AddTracker(trackers[i]);
            }
        }

        /// <summary>
        /// Añade un tracker más. Se inicializa en el momento si aún no lo estaba.
        /// </summary>
        public static void AddTracker(AAnalyticsTracker tracker)
        {
            if (tracker == null)
            {
                throw new ArgumentNullException(nameof(tracker), "[Analytics] The Analytics tracker cannot be null.");
            }

            if (_trackers.Contains(tracker)) return;

            // Antes de añadirlo: si el consentimiento compartido ya está resuelto, la suscripción lo aplica a los
            // trackers existentes y el nuevo lo recibe justo después como "tracker que llega tarde".
            EnsureSharedConsentSubscription();

            _trackers.Add(tracker);
            if (!tracker.IsInitialized())
            {
                tracker.Initialize();
            }

            // Los trackers que llegan tarde reciben el consentimiento y las propiedades de usuario ya registradas.
            if (_consent != null)
            {
                tracker.SetConsent(_consent);
            }
            foreach (var pair in _userProperties)
            {
                tracker.SetUserProperty(pair.Key, pair.Value);
            }
            if (_hasUserId)
            {
                tracker.SetUserId(_userId);
            }
        }

        /// <summary>
        /// Primer tracker registrado del tipo indicado, o null. Útil para engancharse a algo específico de un SDK,
        /// por ejemplo el evento FirebaseReady del tracker de Firebase.
        /// </summary>
        public static T GetTracker<T>() where T : AAnalyticsTracker
        {
            for (int i = 0; i < _trackers.Count; i++)
            {
                if (_trackers[i] is T typed) return typed;
            }
            return null;
        }

        /// <summary>
        /// Señales de Google Consent Mode, normalmente el resultado de <see cref="ConsentModeMapper.Map"/> cuando el CMP
        /// termina. Se envían a todos los trackers, también a los que se añadan más tarde; cada uno las aplica en cuanto
        /// su SDK está listo.
        /// </summary>
        /// <summary>
        /// Consentimiento compartido por el core (VivaConsent): lo publica el módulo que tiene el CMP (Viva Ads
        /// tras el flujo de AppLovin MAX) y aquí se traduce a Google Consent Mode sin código del proyecto.
        /// Un proyecto con su propio CMP puede seguir llamando a SetConsent: gana la última llamada.
        /// Subscribe deduplica por callback, así que llamarlo en cada AddTracker no suscribe dos veces.
        /// </summary>
        private static void EnsureSharedConsentSubscription()
        {
            VivaConsent.Subscribe(OnSharedConsent);
        }

        private static void OnSharedConsent(ConsentState state)
        {
            var tcf = new TcfConsent(state.IsGdpr, state.Purpose1, state.Purpose3, state.Purpose4, state.Purpose7, state.GoogleVendor);
            SetConsent(ConsentModeMapper.Map(tcf));
            Debug.Log($"[Analytics] Consent Mode applied from {state.Source ?? "VivaConsent"}.");
        }

        public static void SetConsent(IReadOnlyDictionary<ConsentSignal, bool> signals)
        {
            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            _consent = new Dictionary<ConsentSignal, bool>();
            foreach (var pair in signals)
            {
                _consent[pair.Key] = pair.Value;
            }

            for (int i = 0; i < _trackers.Count; i++)
            {
                _trackers[i].SetConsent(_consent);
            }
        }

        /// <summary>
        /// Propiedad de usuario (segmento, país, tipo de jugador...). Se envía a todos los trackers,
        /// también a los que se añadan más tarde. En Firebase equivale a SetUserProperty.
        /// </summary>
        public static void SetUserProperty(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("[Analytics] The user property name cannot be empty.", nameof(name));
            }

            _userProperties[name] = value;
            for (int i = 0; i < _trackers.Count; i++)
            {
                _trackers[i].SetUserProperty(name, value);
            }
        }

        /// <summary>
        /// Identificador del usuario. Se envía a todos los trackers, también a los que se añadan más tarde.
        /// </summary>
        public static void SetUserId(string userId)
        {
            _userId = userId;
            _hasUserId = true;
            for (int i = 0; i < _trackers.Count; i++)
            {
                _trackers[i].SetUserId(userId);
            }
        }

        /// <summary>
        /// Quita un tracker. Devuelve false si no estaba registrado.
        /// </summary>
        public static bool RemoveTracker(AAnalyticsTracker tracker)
        {
            return _trackers.Remove(tracker);
        }

        /// <summary>
        /// Registra un parámetro que viajará con todos los eventos. El proveedor se evalúa en cada evento,
        /// así que puede apuntar a datos que cambian durante la partida (nivel del jugador, horas jugadas...).
        /// Si un evento define un parámetro con la misma clave, gana el valor del evento.
        /// </summary>
        public static void RegisterCommonParameter(string key, Func<object> valueProvider)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("[Analytics] The common parameter key cannot be empty.", nameof(key));
            }

            if (valueProvider == null)
            {
                throw new ArgumentNullException(nameof(valueProvider));
            }

            _commonParameters[key] = valueProvider;
        }

        /// <summary>
        /// Registra un parámetro común con un valor fijo (versión de la build, plataforma...).
        /// </summary>
        public static void SetCommonParameter(string key, object value)
        {
            RegisterCommonParameter(key, () => value);
        }

        /// <summary>
        /// Elimina un parámetro común. Devuelve false si no existía.
        /// </summary>
        public static bool RemoveCommonParameter(string key)
        {
            return _commonParameters.Remove(key);
        }

        public static void ClearCommonParameters()
        {
            _commonParameters.Clear();
        }

        /// <summary>
        /// Envía el evento a todos los trackers inicializados, añadiendo los parámetros comunes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Si el servicio no está inicializado.</exception>
        public static void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            if (eventToTrack == null)
            {
                throw new ArgumentNullException(nameof(eventToTrack));
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException("[Analytics] Service not initialized. Cannot track events.");
            }

            IAnalyticsEvent enriched = _commonParameters.Count == 0
                ? eventToTrack
                : new EnrichedAnalyticsEvent(eventToTrack, _commonParameters);

            for (int i = 0; i < _trackers.Count; i++)
            {
                var tracker = _trackers[i];
                if (!tracker.IsInitialized()) continue;

                try
                {
                    tracker.TrackEvent(enriched);
                }
                catch (Exception e)
                {
                    // Un tracker que falle no debe impedir que el resto reciba el evento ni romper el juego.
                    Debug.LogError($"[Analytics] Tracker {tracker.GetType().Name} failed to track '{enriched.GetEventKey()}': {e}");
                }
            }
        }

        /// <summary>
        /// Envía el evento preconfigurado de paso de FTUE.
        /// Recuerda cuál fue el último paso enviado y no hace nada si se intenta enviar uno anterior.
        /// </summary>
        /// <param name="step">Número de paso.</param>
        /// <exception cref="InvalidOperationException">
        /// Si la configuración de FTUE no es válida. No se lanza al intentar enviar un paso anterior al último (solo se avisa).
        /// </exception>
        public static void TrackFTUE(int step)
        {
            if (string.IsNullOrWhiteSpace(Config.FtueEventName) || string.IsNullOrWhiteSpace(Config.FtueStepParameterName))
            {
                throw new InvalidOperationException("[Analytics] Cannot track FTUE steps: Event name and step parameter name not properly configured.");
            }

            int lastStepTracked = PlayerPrefs.GetInt(FTUE_PREF_KEY);

            if (lastStepTracked < step)
            {
                _ftueEvent.SetStep(step);
                TrackEvent(_ftueEvent);
                PlayerPrefs.SetInt(FTUE_PREF_KEY, step);
            }
            else
            {
                Debug.LogWarning($"[Analytics] Trying to track FTUE Step {step}, but last step tracked is {lastStepTracked}. Skipping.");
            }
        }

        private static AnalyticsConfig DefaultConfig()
        {
            return new AnalyticsConfig
            {
                FtueEventName = DEFAULT_FTUE_EVENT_NAME,
                FtueStepParameterName = DEFAULT_FTUE_STEP_PARAMETER_NAME
            };
        }

        /// <summary>
        /// Evento envuelto con los parámetros comunes ya resueltos. Es lo que reciben los trackers.
        /// </summary>
        private sealed class EnrichedAnalyticsEvent : IAnalyticsEvent
        {
            private readonly string _eventKey;
            private readonly Dictionary<string, object> _fields;

            public EnrichedAnalyticsEvent(IAnalyticsEvent source, Dictionary<string, Func<object>> commonParameters)
            {
                _eventKey = source.GetEventKey();
                Dictionary<string, object> sourceFields = source.GetTrackingFields();
                _fields = new Dictionary<string, object>(commonParameters.Count + (sourceFields?.Count ?? 0));

                foreach (var pair in commonParameters)
                {
                    try
                    {
                        _fields[pair.Key] = pair.Value();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Analytics] Common parameter '{pair.Key}' threw an exception and was skipped: {e.Message}");
                    }
                }

                if (sourceFields != null)
                {
                    foreach (var pair in sourceFields)
                    {
                        _fields[pair.Key] = pair.Value;
                    }
                }
            }

            public string GetEventKey() => _eventKey;

            public Dictionary<string, object> GetTrackingFields() => _fields;
        }
    }
}
