using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Services.Utils;

namespace Viva.Services.Analytics
{
    [Serializable]
    public class CatalogParameter
    {
        public string name;
        public string type;
    }

    /// <summary>
    /// Un evento estándar del estudio tal y como está definido en Templates~/EventCatalog.json.
    /// </summary>
    [Serializable]
    public class CatalogEvent
    {
        public string name;
        public string description;
        public CatalogParameter[] parameters;

        /// <summary>Destinos además de Firebase ("facebook", "singular"). Opcional.</summary>
        public string[] targets;

        public string ClassName => StringUtils.ToUpperCamelCase(name);

        public AnalyticsTargets ToTargets() => EventTargetsCodec.FromNames(targets);

        public string FilePath => $"{AnalyticsEditorSettings.EventsFolder}/{ClassName}.cs";

        public List<EventParameter> ToEventParameters()
        {
            var list = new List<EventParameter>();
            if (parameters == null) return list;
            foreach (var parameter in parameters)
            {
                list.Add(new EventParameter(parameter.name, parameter.type));
            }
            return list;
        }
    }

    [Serializable]
    internal class CatalogData
    {
        public CatalogEvent[] events;
    }

    /// <summary>
    /// Catálogo de eventos estándar del estudio. Vive dentro del paquete y se importa al proyecto
    /// como ficheros normales, que cada juego puede editar, restaurar o borrar sin tocar el paquete.
    /// </summary>
    public static class AnalyticsEventCatalog
    {
        private static CatalogEvent[] _events;

        public static IReadOnlyList<CatalogEvent> Events
        {
            get
            {
                if (_events == null) Reload();
                return _events;
            }
        }

        public static void Reload()
        {
            _events = Array.Empty<CatalogEvent>();

            var path = AnalyticsPackage.EventCatalogPath;
            if (path == null || !File.Exists(path))
            {
                Debug.LogWarning($"[Analytics] Event catalog not found at {path ?? "(package path unresolved)"}.");
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<CatalogData>(File.ReadAllText(path));
                if (data?.events != null) _events = data.events;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Analytics] Could not read the event catalog: {e.Message}");
            }
        }

        public static CatalogEvent Find(string snakeCaseName)
        {
            foreach (var catalogEvent in Events)
            {
                if (catalogEvent.name == snakeCaseName) return catalogEvent;
            }
            return null;
        }

        public static string GenerateSource(CatalogEvent catalogEvent)
        {
            return AnalyticsEventCodeGenerator.Generate(catalogEvent.ClassName, catalogEvent.ToEventParameters(), catalogEvent.ToTargets());
        }

        public static bool IsInProject(CatalogEvent catalogEvent)
        {
            return File.Exists(catalogEvent.FilePath);
        }

        /// <summary>true si el fichero del proyecto ya no coincide con lo que generaría el catálogo.</summary>
        public static bool IsModified(CatalogEvent catalogEvent)
        {
            if (!IsInProject(catalogEvent)) return false;
            try
            {
                var current = StringUtils.Normalize(File.ReadAllText(catalogEvent.FilePath));
                return current != StringUtils.Normalize(GenerateSource(catalogEvent));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static List<CatalogEvent> Missing()
        {
            var result = new List<CatalogEvent>();
            foreach (var catalogEvent in Events)
            {
                if (!IsInProject(catalogEvent)) result.Add(catalogEvent);
            }
            return result;
        }

        /// <summary>Escribe el evento en la carpeta de eventos del proyecto. No refresca el AssetDatabase.</summary>
        public static void Write(CatalogEvent catalogEvent)
        {
            var folder = AnalyticsEditorSettings.EventsFolder;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(catalogEvent.FilePath, GenerateSource(catalogEvent));
        }

        /// <summary>Importa (o restaura) un evento y recompila.</summary>
        public static void Import(CatalogEvent catalogEvent)
        {
            Write(catalogEvent);
            RefreshAndCompile();
        }

        /// <summary>Importa todos los eventos que falten. Devuelve cuántos ha escrito.</summary>
        public static int ImportMissing()
        {
            var missing = Missing();
            foreach (var catalogEvent in missing)
            {
                Write(catalogEvent);
            }
            if (missing.Count > 0) RefreshAndCompile();
            return missing.Count;
        }

        private static void RefreshAndCompile()
        {
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}
