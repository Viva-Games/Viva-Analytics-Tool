using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Añade o quita símbolos de compilación (scripting defines) en las plataformas habituales.
    /// Los módulos lo usan para activar su código solo cuando detectan el SDK del que dependen.
    /// </summary>
    public static class ScriptingDefines
    {
        private static readonly NamedBuildTarget[] DefaultTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        /// <summary>true si el símbolo está definido para la plataforma activa.</summary>
        public static bool IsDefined(string define)
        {
            try
            {
                return Split(PlayerSettings.GetScriptingDefineSymbols(ActiveTarget())).Contains(define);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Define o elimina el símbolo en todas las plataformas relevantes.
        /// Devuelve true si ha cambiado algo (en ese caso Unity recompila).
        /// </summary>
        public static bool Set(string define, bool enabled)
        {
            bool changed = false;
            foreach (var target in Targets())
            {
                try
                {
                    changed |= Apply(target, define, enabled);
                }
                catch (Exception)
                {
                    // Plataforma no disponible en esta instalación de Unity: se ignora.
                }
            }
            return changed;
        }

        private static IEnumerable<NamedBuildTarget> Targets()
        {
            var targets = new List<NamedBuildTarget>(DefaultTargets);
            try
            {
                var active = ActiveTarget();
                if (!targets.Contains(active)) targets.Add(active);
            }
            catch (Exception)
            {
                // Grupo de build desconocido: nos quedamos con los de la lista.
            }
            return targets;
        }

        private static NamedBuildTarget ActiveTarget()
        {
            return NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        }

        private static bool Apply(NamedBuildTarget target, string define, bool enabled)
        {
            var defines = Split(PlayerSettings.GetScriptingDefineSymbols(target));
            bool isDefined = defines.Contains(define);
            if (isDefined == enabled) return false;

            if (enabled) defines.Add(define);
            else defines.RemoveAll(d => d == define);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            return true;
        }

        private static List<string> Split(string defines)
        {
            return (defines ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => d.Length > 0)
                .ToList();
        }
    }
}
