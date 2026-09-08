using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Viva.Services.Ads.Editor
{
    /// <summary>
    /// Desplegable en el inspector para los campos marcados con [PlacementName]: ofrece los placements del
    /// formato declarados en Viva > Ads > Ad Units. Si el valor actual no está en la lista, se muestra como
    /// texto con aviso, para no perderlo.
    /// </summary>
    [CustomPropertyDrawer(typeof(PlacementNameAttribute))]
    public class PlacementNameDrawer : PropertyDrawer
    {
        private static AdUnitsDefinition _cached;
        private static DateTime _cachedWriteTime;
        private static string _cachedPath;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var format = ((PlacementNameAttribute)attribute).Format;
            var placements = PlacementsOf(format);
            string current = property.stringValue ?? string.Empty;

            if (placements.Count == 0)
            {
                EditorGUI.PropertyField(position, property, new GUIContent(label.text, $"No {format} placements declared in Viva > Ads > Ad Units."));
                return;
            }

            int index = placements.IndexOf(current);
            var options = new List<string>(placements);
            if (index < 0 && current.Length > 0)
            {
                options.Add(current + " (not declared)");
                index = options.Count - 1;
            }
            if (index < 0) index = 0;

            EditorGUI.BeginProperty(position, label, property);
            int selected = EditorGUI.Popup(position, label.text, index, options.ToArray());
            if (selected != index || current.Length == 0)
            {
                property.stringValue = selected < placements.Count ? placements[selected] : current;
            }
            EditorGUI.EndProperty();
        }

        private static List<string> PlacementsOf(AdFormat format)
        {
            string path = AdsEditorSettings.AdUnitsFilePath;
            try
            {
                var writeTime = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                if (_cached == null || path != _cachedPath || writeTime != _cachedWriteTime)
                {
                    _cached = AdUnitsFile.Load(path);
                    _cachedPath = path;
                    _cachedWriteTime = writeTime;
                }
            }
            catch (Exception)
            {
                _cached = _cached ?? new AdUnitsDefinition();
            }

            var section = _cached.Get(format);
            return section != null && section.enabled ? new List<string>(section.placements) : new List<string>();
        }
    }
}
