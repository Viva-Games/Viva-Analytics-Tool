using System;
using System.Collections;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// MonoBehaviour oculto para lo que un tracker estático no tiene: OnApplicationPause (Facebook pide
    /// ActivateApp al reanudar) y esperas por corrutina (Singular avisa de su inicialización con un flag).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class AnalyticsRuntime : MonoBehaviour
    {
        private static AnalyticsRuntime _instance;

        /// <summary>true al pausar, false al reanudar.</summary>
        public event Action<bool> ApplicationPaused;

        public static AnalyticsRuntime GetOrCreate()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[Viva Analytics]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AnalyticsRuntime>();
            return _instance;
        }

        /// <summary>Ejecuta la acción cuando la condición sea cierta, comprobándola cada intervalo (en tiempo real).</summary>
        public Coroutine WaitUntil(Func<bool> condition, Action then, float intervalSeconds = 0.5f)
        {
            return StartCoroutine(WaitUntilRoutine(condition, then, intervalSeconds));
        }

        private static IEnumerator WaitUntilRoutine(Func<bool> condition, Action then, float intervalSeconds)
        {
            while (!condition())
            {
                if (intervalSeconds > 0f) yield return new WaitForSecondsRealtime(intervalSeconds);
                else yield return null;
            }
            then?.Invoke();
        }

        private void OnApplicationPause(bool paused)
        {
            var handlers = ApplicationPaused;
            if (handlers == null) return;
            try
            {
                handlers(paused);
            }
            catch (Exception e)
            {
                Debug.LogError("[Analytics] ApplicationPaused handler failed: " + e);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
