using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Tipos que admite la ventana de parámetros. Los cuatro numéricos son NUMBER en la consola de Firebase;
    /// el tipo C# decide qué conversión se aplica al leer y qué campo muestra la ventana.
    /// </summary>
    public static class RemoteConfigParameterTypes
    {
        public const string INT = "int";
        public const string LONG = "long";
        public const string FLOAT = "float";
        public const string DOUBLE = "double";
        public const string BOOL = "bool";
        public const string STRING = "string";
        public const string JSON = "json";

        public static readonly string[] All = { INT, LONG, FLOAT, DOUBLE, BOOL, STRING, JSON };

        public static bool IsKnown(string type)
        {
            return Array.IndexOf(All, type) >= 0;
        }

        public static bool IsNumeric(string type)
        {
            return type == INT || type == LONG || type == FLOAT || type == DOUBLE;
        }

        /// <summary>Tipo de valor con el que aparece el parámetro en la consola de Firebase.</summary>
        public static string ToFirebaseValueType(string type)
        {
            switch (type)
            {
                case INT:
                case LONG:
                case FLOAT:
                case DOUBLE:
                    return "NUMBER";
                case BOOL:
                    return "BOOLEAN";
                case JSON:
                    return "JSON";
                default:
                    return "STRING";
            }
        }
    }

    /// <summary>
    /// Un parámetro tal y como está en el JSON del proyecto. defaultValue se guarda siempre como cadena,
    /// igual que lo hace Firebase, y se valida según type al guardar.
    /// </summary>
    [Serializable]
    public class RemoteConfigParameterDefinition
    {
        public string key = string.Empty;
        public string type = RemoteConfigParameterTypes.INT;
        public string defaultValue = string.Empty;
        public string description = string.Empty;

        public RemoteConfigParameterDefinition()
        {
        }

        public RemoteConfigParameterDefinition(string key, string type, string defaultValue, string description = "")
        {
            this.key = key ?? string.Empty;
            this.type = type ?? RemoteConfigParameterTypes.INT;
            this.defaultValue = defaultValue ?? string.Empty;
            this.description = description ?? string.Empty;
        }

        public RemoteConfigParameterDefinition Clone()
        {
            return new RemoteConfigParameterDefinition(key, type, defaultValue, description);
        }
    }

    [Serializable]
    internal class RemoteConfigParameterFileData
    {
        public RemoteConfigParameterDefinition[] parameters = new RemoteConfigParameterDefinition[0];
    }

    /// <summary>
    /// Lectura y escritura del JSON de parámetros (la fuente de verdad que edita la ventana).
    /// </summary>
    public static class RemoteConfigParameterFile
    {
        /// <summary>Lista vacía si el fichero no existe. Deja un error en la consola si no se puede leer.</summary>
        public static List<RemoteConfigParameterDefinition> Load(string path)
        {
            var result = new List<RemoteConfigParameterDefinition>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;

            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"{RemoteConfigPackage.LOG_PREFIX} Could not read {path}: {e.Message}");
                return result;
            }
        }

        public static List<RemoteConfigParameterDefinition> Parse(string json)
        {
            var result = new List<RemoteConfigParameterDefinition>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            var data = JsonUtility.FromJson<RemoteConfigParameterFileData>(json);
            if (data?.parameters == null) return result;

            foreach (var parameter in data.parameters)
            {
                if (parameter == null) continue;
                result.Add(new RemoteConfigParameterDefinition(parameter.key, parameter.type, parameter.defaultValue, parameter.description));
            }
            return result;
        }

        public static string Serialize(IReadOnlyList<RemoteConfigParameterDefinition> parameters)
        {
            var data = new RemoteConfigParameterFileData { parameters = new RemoteConfigParameterDefinition[parameters.Count] };
            for (int i = 0; i < parameters.Count; i++)
            {
                data.parameters[i] = parameters[i].Clone();
            }
            return JsonUtility.ToJson(data, true) + "\n";
        }

        /// <summary>Escribe el fichero creando la carpeta si hace falta. Devuelve false (y un error en consola) si no puede.</summary>
        public static bool Save(string path, IReadOnlyList<RemoteConfigParameterDefinition> parameters)
        {
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(path, Serialize(parameters));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{RemoteConfigPackage.LOG_PREFIX} Could not write {path}: {e.Message}");
                return false;
            }
        }
    }
}
