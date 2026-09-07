using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Ejecuta operaciones del Package Manager (instalar, actualizar, quitar) de una en una.
    /// La cola se guarda en SessionState para sobrevivir a la recarga de dominio que provoca cada instalación.
    /// </summary>
    [InitializeOnLoad]
    public static class VivaPackageOperations
    {
        private const string QUEUE_KEY = "Viva.Core.PackageOperations.Queue";
        private const string MESSAGE_KEY = "Viva.Core.PackageOperations.LastMessage";
        private const string ADD_PREFIX = "add ";
        private const string REMOVE_PREFIX = "remove ";
        private const double QUIET_SECONDS = 1.5;

        private static Request _current;
        private static string _currentLabel;
        private static double _quietSince;

        /// <summary>Se dispara cuando empieza o termina una operación, para que las ventanas se repinten.</summary>
        public static event Action Changed;

        /// <summary>true mientras haya una operación en curso o pendiente.</summary>
        public static bool IsBusy => _current != null || LoadQueue().Count > 0;

        /// <summary>Descripción de la operación en curso, o null si no hay ninguna.</summary>
        public static string CurrentLabel => _currentLabel;

        /// <summary>Resultado de la última operación terminada.</summary>
        public static string LastMessage
        {
            get => SessionState.GetString(MESSAGE_KEY, string.Empty);
            private set => SessionState.SetString(MESSAGE_KEY, value ?? string.Empty);
        }

        static VivaPackageOperations()
        {
            // Tras una recarga de dominio se continúa con lo que quedara pendiente en la cola.
            EditorApplication.delayCall += TryRunNext;
        }

        /// <summary>Instala el módulo en el tag indicado. Si ya está instalado, lo sustituye por esa versión.</summary>
        public static void Install(VivaModule module, string tag)
        {
            Enqueue(ADD_PREFIX + VivaRepository.BuildPackageUrl(module.FolderName, tag));
        }

        /// <summary>Quita el módulo del proyecto.</summary>
        public static void Remove(VivaModule module)
        {
            Enqueue(REMOVE_PREFIX + module.PackageName);
        }

        /// <summary>
        /// Instala o actualiza varios módulos al mismo tag. El core va el último para que la recarga
        /// del propio instalador no interrumpa al resto.
        /// </summary>
        public static void InstallAll(IEnumerable<VivaModule> modules, string tag)
        {
            VivaModule core = null;
            foreach (var module in modules)
            {
                if (module.IsCore) core = module;
                else Install(module, tag);
            }
            if (core != null) Install(core, tag);
        }

        public static void ClearLastMessage()
        {
            LastMessage = string.Empty;
        }

        private static void Enqueue(string operation)
        {
            var queue = LoadQueue();
            if (!queue.Contains(operation)) queue.Add(operation);
            SaveQueue(queue);
            TryRunNext();
        }

        private static void TryRunNext()
        {
            if (_current != null) return;

            var queue = LoadQueue();
            if (queue.Count == 0) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _quietSince = EditorApplication.timeSinceStartup;
                EditorApplication.update -= WaitAndRunNext;
                EditorApplication.update += WaitAndRunNext;
                return;
            }

            var operation = queue[0];
            queue.RemoveAt(0);
            SaveQueue(queue);

            if (operation.StartsWith(ADD_PREFIX, StringComparison.Ordinal))
            {
                var url = operation.Substring(ADD_PREFIX.Length);
                _currentLabel = $"Installing {url}";
                _current = Client.Add(url);
            }
            else
            {
                var packageName = operation.Substring(REMOVE_PREFIX.Length);
                _currentLabel = $"Removing {packageName}";
                _current = Client.Remove(packageName);
            }

            LastMessage = _currentLabel + "...";
            Debug.Log("[Viva] " + LastMessage);
            EditorApplication.update += Poll;
            Changed?.Invoke();
        }

        private static void Poll()
        {
            if (_current == null)
            {
                EditorApplication.update -= Poll;
                return;
            }
            if (!_current.IsCompleted) return;
            EditorApplication.update -= Poll;

            if (_current.Status == StatusCode.Success)
            {
                LastMessage = $"{_currentLabel}: done.";
                Debug.Log("[Viva] " + LastMessage);
            }
            else
            {
                LastMessage = $"{_currentLabel}: FAILED. {_current.Error?.message}";
                Debug.LogError("[Viva] " + LastMessage);
                SaveQueue(new List<string>()); // si algo falla no seguimos con el resto de la cola
            }

            _current = null;
            _currentLabel = null;
            Changed?.Invoke();

            // Espera a que Unity termine de importar y compilar antes de la siguiente operación.
            _quietSince = EditorApplication.timeSinceStartup;
            EditorApplication.update -= WaitAndRunNext;
            EditorApplication.update += WaitAndRunNext;
        }

        private static void WaitAndRunNext()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _quietSince = EditorApplication.timeSinceStartup;
                return;
            }
            if (EditorApplication.timeSinceStartup - _quietSince < QUIET_SECONDS) return;

            EditorApplication.update -= WaitAndRunNext;
            TryRunNext();
        }

        private static List<string> LoadQueue()
        {
            var queue = new List<string>();
            var raw = SessionState.GetString(QUEUE_KEY, string.Empty);
            if (string.IsNullOrEmpty(raw)) return queue;

            foreach (var item in raw.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(item)) queue.Add(item.Trim());
            }
            return queue;
        }

        private static void SaveQueue(List<string> queue)
        {
            SessionState.SetString(QUEUE_KEY, string.Join("\n", queue));
        }
    }
}
