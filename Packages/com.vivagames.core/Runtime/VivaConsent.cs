using System;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Core
{
    /// <summary>
    /// Estado de consentimiento leído del TCF (IAB Transparency and Consent Framework) por el módulo que
    /// tiene el CMP, Viva Ads con el de AppLovin MAX, y consumido por los que lo necesitan, como Viva Analytics
    /// para Google Consent Mode. Cada purpose es nullable: null = sin dato TCF (usuario fuera del EEE, o CMP
    /// que no escribe TCF).
    /// </summary>
    public sealed class ConsentState
    {
        /// <summary>true si al usuario le aplica el RGPD (región GDPR). Fuera del EEE todo se considera concedido.</summary>
        public bool IsGdpr;

        /// <summary>Purpose 1: almacenar y acceder a información en el dispositivo.</summary>
        public bool? Purpose1;

        /// <summary>Purpose 3: crear perfiles para publicidad personalizada.</summary>
        public bool? Purpose3;

        /// <summary>Purpose 4: usar perfiles para seleccionar publicidad personalizada.</summary>
        public bool? Purpose4;

        /// <summary>Purpose 7: medir el rendimiento de los anuncios.</summary>
        public bool? Purpose7;

        /// <summary>Consentimiento al vendor de Google (IAB vendor 755).</summary>
        public bool? GoogleVendor;

        /// <summary>Quién lo ha publicado ("AppLovin MAX"), solo para el log.</summary>
        public string Source;

        public override string ToString()
        {
            return $"gdpr={IsGdpr} p1={Text(Purpose1)} p3={Text(Purpose3)} p4={Text(Purpose4)} p7={Text(Purpose7)} google={Text(GoogleVendor)} source={Source ?? "?"}";
        }

        private static string Text(bool? value) => value.HasValue ? (value.Value ? "yes" : "no") : "null";
    }

    /// <summary>
    /// Puente de consentimiento entre módulos Viva, que no se referencian entre sí. El módulo con el CMP publica
    /// con <see cref="Set"/>; los que lo consumen se apuntan con <see cref="Subscribe"/>, que entrega el estado
    /// actual en el acto si ya existe y cada cambio posterior (re-consentimiento). Da igual quién se inicializa
    /// antes. Hilo principal.
    /// </summary>
    public static class VivaConsent
    {
        private static readonly List<Action<ConsentState>> _subscribers = new List<Action<ConsentState>>();

        /// <summary>Último estado publicado, o null hasta que alguien publique.</summary>
        public static ConsentState Current { get; private set; }

        public static bool IsResolved => Current != null;

        /// <summary>
        /// Apunta el callback. Si ya hay un estado, lo recibe ahora mismo; después recibe cada cambio.
        /// Cada callback se apunta una sola vez.
        /// </summary>
        public static void Subscribe(Action<ConsentState> callback)
        {
            if (callback == null) return;
            if (!_subscribers.Contains(callback)) _subscribers.Add(callback);
            if (Current != null) Invoke(callback, Current);
        }

        public static void Unsubscribe(Action<ConsentState> callback)
        {
            if (callback == null) return;
            _subscribers.Remove(callback);
        }

        /// <summary>
        /// Publica un consentimiento nuevo y avisa a los suscritos en orden de suscripción. Se puede llamar
        /// más de una vez (el usuario cambia su elección): siempre gana la última.
        /// </summary>
        public static void Set(ConsentState state)
        {
            if (state == null)
            {
                Debug.LogWarning("[Viva] VivaConsent.Set ignored a null state.");
                return;
            }

            Current = state;
            Debug.Log($"[Viva] Consent resolved: {state}");

            foreach (var subscriber in _subscribers.ToArray())
            {
                Invoke(subscriber, state);
            }
        }

        private static void Invoke(Action<ConsentState> callback, ConsentState state)
        {
            try
            {
                callback(state);
            }
            catch (Exception e)
            {
                Debug.LogError("[Viva] A consent subscriber failed: " + e);
            }
        }

        /// <summary>Deja el puente como recién cargado. Solo para tests.</summary>
        internal static void Reset()
        {
            Current = null;
            _subscribers.Clear();
        }
    }
}
