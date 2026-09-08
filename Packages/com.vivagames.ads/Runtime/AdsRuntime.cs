using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Viva.Services.Ads
{
    /// <summary>
    /// MonoBehaviour oculto que da al servicio lo que un estático no tiene: corrutinas en tiempo real,
    /// OnApplicationPause, y el bloqueo de input y audio mientras hay un anuncio. Lo crea AdsService.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class AdsRuntime : MonoBehaviour, IAdsScheduler
    {
        private static AdsRuntime _instance;
        private EventSystem _blockedEventSystem;
        private bool _audioPaused;

        public static AdsRuntime GetOrCreate()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[Viva Ads]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AdsRuntime>();
            return _instance;
        }

        public float Now => Time.realtimeSinceStartup;

        public object Delay(float seconds, Action action)
        {
            return StartCoroutine(DelayRoutine(seconds, action));
        }

        public void Cancel(object handle)
        {
            if (handle is Coroutine coroutine && this != null) StopCoroutine(coroutine);
        }

        private static IEnumerator DelayRoutine(float seconds, Action action)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            else yield return null;
            action?.Invoke();
        }

        /// <summary>
        /// Desactiva el EventSystem mientras hay un anuncio, para que ningún botón responda en la ventana en la que
        /// el overlay nativo aún no cubre la pantalla o justo tras cerrarse. Se guarda la referencia porque al
        /// desactivarlo EventSystem.current pasa a null.
        /// </summary>
        public void SetUiInputBlocked(bool blocked)
        {
            if (blocked)
            {
                if (_blockedEventSystem == null) _blockedEventSystem = EventSystem.current;
                if (_blockedEventSystem != null) _blockedEventSystem.enabled = false;
            }
            else if (_blockedEventSystem != null)
            {
                _blockedEventSystem.enabled = true;
                _blockedEventSystem = null;
            }
        }

        public void SetAudioPaused(bool paused)
        {
            if (paused == _audioPaused) return;
            _audioPaused = paused;
            AudioListener.pause = paused;
        }

        private void OnApplicationPause(bool paused)
        {
            AdsService.HandleApplicationPause(paused);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
