using System;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Core
{
    public enum ReadyState
    {
        NotStarted,
        Running,
        Ready,
        Failed
    }

    /// <summary>
    /// Puerta de "listo" de una inicialización que solo debe hacerse una vez y a la que varios módulos
    /// independientes quieren esperar (la comprobación de dependencias de Firebase, por ejemplo).
    /// Quien llega primero la arranca con <see cref="TryStart"/>; todos se apuntan con <see cref="WhenReady"/>,
    /// que ejecuta el callback en el acto si la puerta ya está abierta o lo encola si no.
    /// No es thread-safe: se usa desde el hilo principal.
    /// </summary>
    public sealed class ReadyGate
    {
        private readonly struct Waiter
        {
            public readonly Action OnReady;
            public readonly Action<string> OnFailed;

            public Waiter(Action onReady, Action<string> onFailed)
            {
                OnReady = onReady;
                OnFailed = onFailed;
            }
        }

        private readonly List<Waiter> _waiters = new List<Waiter>();
        private readonly string _name;

        public ReadyState State { get; private set; } = ReadyState.NotStarted;

        public bool IsReady => State == ReadyState.Ready;

        public bool HasFailed => State == ReadyState.Failed;

        /// <summary>Motivo del fallo, o null si no ha fallado.</summary>
        public string FailureReason { get; private set; }

        /// <summary>Callbacks pendientes de que la puerta se abra o falle.</summary>
        public int PendingCount => _waiters.Count;

        public ReadyGate(string name)
        {
            _name = string.IsNullOrEmpty(name) ? "gate" : name;
        }

        /// <summary>
        /// true si esta llamada es la que arranca la inicialización (la primera). false si ya estaba
        /// arrancada o terminada: quien lo recibe no debe inicializar nada, solo esperar con <see cref="WhenReady"/>.
        /// </summary>
        public bool TryStart()
        {
            if (State != ReadyState.NotStarted) return false;
            State = ReadyState.Running;
            return true;
        }

        /// <summary>Abre la puerta: ejecuta, en orden de suscripción, los callbacks pendientes.</summary>
        public void SetReady()
        {
            if (!Finish(ReadyState.Ready, null)) return;

            foreach (var waiter in TakeWaiters())
            {
                Invoke(waiter.OnReady);
            }
        }

        /// <summary>Cierra la puerta con un fallo: ejecuta, en orden de suscripción, los callbacks de fallo pendientes.</summary>
        public void SetFailed(string reason)
        {
            if (!Finish(ReadyState.Failed, reason ?? "unknown error")) return;

            foreach (var waiter in TakeWaiters())
            {
                Invoke(waiter.OnFailed, FailureReason);
            }
        }

        /// <summary>
        /// onReady corre cuando la puerta se abre (en el acto si ya lo estaba); onFailed cuando falla.
        /// Cada callback se invoca como mucho una vez. Una excepción en un callback no afecta al resto.
        /// </summary>
        public void WhenReady(Action onReady, Action<string> onFailed = null)
        {
            switch (State)
            {
                case ReadyState.Ready:
                    Invoke(onReady);
                    break;
                case ReadyState.Failed:
                    Invoke(onFailed, FailureReason);
                    break;
                default:
                    _waiters.Add(new Waiter(onReady, onFailed));
                    break;
            }
        }

        private bool Finish(ReadyState finalState, string reason)
        {
            if (State == ReadyState.Ready || State == ReadyState.Failed)
            {
                Debug.LogWarning($"[Viva] {_name}: already {State}, ignoring {finalState}.");
                return false;
            }

            State = finalState;
            FailureReason = reason;
            return true;
        }

        private List<Waiter> TakeWaiters()
        {
            var waiters = new List<Waiter>(_waiters);
            _waiters.Clear();
            return waiters;
        }

        private void Invoke(Action callback)
        {
            if (callback == null) return;
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Viva] {_name}: a ready callback failed: {e}");
            }
        }

        private void Invoke(Action<string> callback, string reason)
        {
            if (callback == null) return;
            try
            {
                callback(reason);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Viva] {_name}: a failure callback failed: {e}");
            }
        }
    }
}
