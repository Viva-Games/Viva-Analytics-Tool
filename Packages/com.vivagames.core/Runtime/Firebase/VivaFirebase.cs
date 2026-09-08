using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace Viva.Core
{
    /// <summary>
    /// Comprobación de dependencias de Firebase compartida por todos los módulos Viva.
    /// Se hace una sola vez por proceso: el SDK de Firebase para Unity no admite dos
    /// CheckAndFixDependenciesAsync a la vez (lanza "Don't call Firebase functions before
    /// CheckDependencies has finished" desde el segundo hilo). El primer módulo que llama a
    /// <see cref="EnsureInitialized"/> la arranca; todos esperan con <see cref="WhenReady"/>,
    /// que corre en el hilo principal. Los módulos nunca llaman a CheckAndFixDependenciesAsync.
    /// Compilado solo con VIVA_FIREBASE, que el core define cuando encuentra Firebase.App.dll.
    /// </summary>
    public static class VivaFirebase
    {
        private static readonly ReadyGate Gate = new ReadyGate("Firebase");

        public static ReadyState State => Gate.State;

        /// <summary>true cuando Firebase ha resuelto sus dependencias y se puede usar.</summary>
        public static bool IsReady => Gate.IsReady;

        public static bool HasFailed => Gate.HasFailed;

        /// <summary>Motivo del fallo de la comprobación, o null.</summary>
        public static string FailureReason => Gate.FailureReason;

        /// <summary>
        /// Arranca la comprobación de dependencias la primera vez que se llama; las siguientes llamadas
        /// no hacen nada. Llamar siempre desde el hilo principal, antes de usar cualquier API de Firebase.
        /// </summary>
        /// <param name="requestedBy">Nombre del módulo que la pide, solo para el log.</param>
        public static void EnsureInitialized(string requestedBy = null)
        {
            if (!Gate.TryStart()) return;

            string by = string.IsNullOrEmpty(requestedBy) ? string.Empty : $" (requested by {requestedBy})";
            Debug.Log($"[Viva] Checking Firebase dependencies{by}...");

            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(OnDependenciesChecked);
            }
            catch (Exception e)
            {
                Fail("Firebase dependency check could not start: " + e);
            }
        }

        /// <summary>
        /// onReady corre en el hilo principal cuando Firebase está listo (en el acto si ya lo estaba);
        /// onFailed cuando la comprobación ha fallado. Cada callback se invoca como mucho una vez,
        /// en orden de suscripción.
        /// </summary>
        public static void WhenReady(Action onReady, Action<string> onFailed = null)
        {
            Gate.WhenReady(onReady, onFailed);
        }

        private static void OnDependenciesChecked(Task<DependencyStatus> task)
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Fail("Firebase dependency check failed: " + (task.Exception != null ? task.Exception.ToString() : "cancelled"));
                return;
            }

            DependencyStatus status = task.Result;
            if (status == DependencyStatus.Available)
            {
                Debug.Log("[Viva] Firebase ready to use.");
                Gate.SetReady();
            }
            else
            {
                Fail($"Could not resolve all Firebase dependencies: {status}.");
            }
        }

        private static void Fail(string reason)
        {
            Debug.LogError("[Viva] " + reason);
            Gate.SetFailed(reason);
        }
    }
}
