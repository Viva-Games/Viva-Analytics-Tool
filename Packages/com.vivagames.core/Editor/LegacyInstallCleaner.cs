using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Detecta y retira los restos de una instalación antigua por .unitypackage antes de instalar el paquete
    /// equivalente. Si conviven los dos, definen los mismos tipos y el proyecto deja de compilar, y en ese
    /// estado Unity no ejecuta ningún script del editor: por eso hay que hacerlo desde el core, antes de instalar.
    /// </summary>
    public static class LegacyInstallCleaner
    {
        private const string BACKUP_ROOT = "Library/VivaLegacyBackup";

        /// <summary>Carpetas antiguas del módulo que siguen en el proyecto.</summary>
        public static List<string> FindLegacyFolders(VivaModule module)
        {
            var result = new List<string>();
            foreach (var folder in module.LegacyFolders)
            {
                if (Directory.Exists(folder)) result.Add(folder);
            }
            return result;
        }

        public static bool HasLegacyInstall(VivaModule module)
        {
            return FindLegacyFolders(module).Count > 0;
        }

        /// <summary>
        /// Si hay restos de la instalación antigua, avisa al usuario y, si acepta, los copia a
        /// Library/VivaLegacyBackup y los borra del proyecto. Devuelve false si el usuario cancela.
        /// </summary>
        public static bool PrepareForInstall(VivaModule module)
        {
            var folders = FindLegacyFolders(module);
            if (folders.Count == 0) return true;

            var message =
                $"An old .unitypackage installation of {module.DisplayName} was found.\n\n" +
                "To install the package, these folders will be removed from the project:\n\n  " +
                string.Join("\n  ", folders) + "\n\n" +
                $"A copy is kept in {BACKUP_ROOT}. Your events and your own scripts are not touched and keep working.\n\n" +
                "Unity will show compile errors for a moment until the package finishes importing.";

            if (!EditorUtility.DisplayDialog("Migrate old installation", message, "Migrate and install", "Cancel"))
                return false;

            RemoveLegacyFolders(module, folders);
            return true;
        }

        /// <summary>Copia las carpetas a Library/VivaLegacyBackup y las borra del proyecto.</summary>
        public static void RemoveLegacyFolders(VivaModule module, List<string> folders)
        {
            var backupFolder = Path.Combine(BACKUP_ROOT, module.PackageName, DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var folder in folders)
                {
                    var name = Path.GetFileName(folder.TrimEnd('/', '\\'));
                    CopyDirectory(folder, Path.Combine(backupFolder, name));

                    var meta = folder + ".meta";
                    if (File.Exists(meta))
                        File.Copy(meta, Path.Combine(backupFolder, name + ".meta"), true);

                    if (!AssetDatabase.DeleteAsset(folder))
                    {
                        // Unity no lo tenía como asset: se borra a mano con su .meta.
                        Directory.Delete(folder, true);
                        if (File.Exists(meta)) File.Delete(meta);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Viva] Legacy files of {module.DisplayName} removed. Backup saved in {backupFolder}");
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}
