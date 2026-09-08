using System.Collections.Generic;
using Singular;
using UnityEngine;
using Viva.Core;
using Viva.Services.Analytics.Singular;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Tracker de Singular. Recibe solo los eventos marcados para Singular en el Event Manager. No inicializa
    /// el SDK (lo hace el componente SingularSDK de la escena, con la API key): espera a
    /// SingularSDK.Initialized y encola hasta entonces. El user id llega como custom user id y las
    /// propiedades de usuario como propiedades globales. Con AttributeAdRevenue, cada impresión publicada
    /// por Viva Ads en VivaAdRevenue se atribuye en Singular sin código del proyecto.
    /// Compilado solo con VIVA_SINGULAR (assembly SingularSDK en el proyecto).
    /// </summary>
    public class SingularAnalyticsTracker : AAnalyticsTracker
    {
        private const int MAX_PENDING = 200;

        private bool _isInitialized;
        private bool _ready;
        private readonly Queue<IAnalyticsEvent> _pendingEvents = new Queue<IAnalyticsEvent>();
        private readonly Queue<AdRevenueEvent> _pendingRevenue = new Queue<AdRevenueEvent>();
        private readonly Dictionary<string, string> _pendingProperties = new Dictionary<string, string>();
        private string _pendingUserId;
        private bool _hasPendingUserId;
        private readonly HashSet<string> _warned = new HashSet<string>();

        /// <summary>
        /// Atribuye en Singular los ingresos de anuncios que publica Viva Ads (VivaAdRevenue). Toma el valor del
        /// toggle de Viva > Analytics > Setup (asset VivaAnalyticsSettings, true por defecto); el código puede
        /// cambiarlo antes de añadir el tracker.
        /// </summary>
        public bool AttributeAdRevenue { get; set; } = AnalyticsSettings.AttributeAdRevenueInSingular;

        /// <summary>Si es true, cada evento enviado se escribe también en la consola.</summary>
        public bool LogToConsole { get; set; }

        public bool IsReady => _ready;

        public override AnalyticsTargets Targets => AnalyticsTargets.Singular;

        protected override void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (AttributeAdRevenue) VivaAdRevenue.Subscribe(OnAdRevenue);

            if (Application.isEditor)
            {
                // En el editor el SDK de Singular no se inicializa nunca y todas sus llamadas son no-ops: el tracker
                // se da por listo para que el flujo sea el del dispositivo y LogToConsole enseñe lo que enviaría.
                Debug.Log("[Analytics] Singular SDK does not run in the editor: Singular events and ad revenue are only logged (LogToConsole).");
                OnSingularReady();
                return;
            }

            if (SingularSDK.Initialized) OnSingularReady();
            else AnalyticsRuntime.GetOrCreate().WaitUntil(() => SingularSDK.Initialized, OnSingularReady, 0.5f);
        }

        protected override bool IsInitialized() => _isInitialized;

        private void OnSingularReady()
        {
            _ready = true;
            Debug.Log($"[Analytics] Singular ready: sending {_pendingEvents.Count} queued event(s) and {_pendingRevenue.Count} queued impression(s).");

            if (_hasPendingUserId)
            {
                SingularSDK.SetCustomUserId(_pendingUserId);
                _hasPendingUserId = false;
                _pendingUserId = null;
            }
            foreach (var pair in _pendingProperties) SingularSDK.SetGlobalProperty(pair.Key, pair.Value, true);
            _pendingProperties.Clear();
            while (_pendingEvents.Count > 0) Send(_pendingEvents.Dequeue());
            while (_pendingRevenue.Count > 0) SendRevenue(_pendingRevenue.Dequeue());
        }

        public override void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            if (!_ready)
            {
                if (_pendingEvents.Count >= MAX_PENDING) _pendingEvents.Dequeue();
                _pendingEvents.Enqueue(eventToTrack);
                return;
            }
            Send(eventToTrack);
        }

        private void Send(IAnalyticsEvent eventToTrack)
        {
            string name = eventToTrack.GetEventKey();
            string nameError = SingularEventConverter.ValidateName(name);
            if (nameError != null) WarnOnce(name, "Singular may reject '" + name + "': " + nameError);

            SingularSDK.Event(SingularEventConverter.ToAttributes(eventToTrack.GetTrackingFields()), name);
            if (LogToConsole) Debug.Log("SINGULAR " + ConsoleAnalyticsTracker.Format(eventToTrack));
        }

        public override void SetUserId(string userId)
        {
            if (_ready)
            {
                SingularSDK.SetCustomUserId(userId);
            }
            else
            {
                _pendingUserId = userId;
                _hasPendingUserId = true;
            }
        }

        public override void SetUserProperty(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (_ready) SingularSDK.SetGlobalProperty(name, value ?? string.Empty, true);
            else _pendingProperties[name] = value ?? string.Empty;
        }

        private void OnAdRevenue(AdRevenueEvent revenue)
        {
            if (!_ready)
            {
                if (_pendingRevenue.Count >= MAX_PENDING) _pendingRevenue.Dequeue();
                _pendingRevenue.Enqueue(revenue);
                return;
            }
            SendRevenue(revenue);
        }

        private void SendRevenue(AdRevenueEvent revenue)
        {
            var data = new SingularAdData(revenue.Platform ?? "AppLovin", revenue.Currency ?? "USD", revenue.Revenue)
                .WithAdType(revenue.Format)
                .WithAdUnitId(revenue.AdUnitId)
                .WithNetworkName(revenue.NetworkName)
                .WithAdPlacmentName(revenue.Placement)
                .WithPrecision(revenue.RevenuePrecision);
            SingularSDK.AdRevenue(data);
            if (LogToConsole) Debug.Log("SINGULAR AD REVENUE " + revenue);
        }

        private void WarnOnce(string key, string message)
        {
            if (_warned.Add(key)) Debug.LogWarning("[Analytics] " + message);
        }
    }
}
