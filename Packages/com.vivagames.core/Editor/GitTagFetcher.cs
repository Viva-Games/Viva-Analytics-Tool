using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEditor;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Consulta los tags de un repositorio con "git ls-remote". Usa el mismo git que necesita el
    /// Package Manager para instalar paquetes por URL, así que no depende de la API de GitHub ni de sus límites.
    /// </summary>
    public static class GitTagFetcher
    {
        private const int TIMEOUT_MS = 20000;
        private const string TAGS_PREFIX = "refs/tags/";

        /// <summary>
        /// Lanza la consulta en un hilo aparte. El callback se ejecuta en el hilo principal del editor
        /// con la lista de tags, o con un mensaje de error si algo ha fallado.
        /// </summary>
        public static void FetchTags(string gitUrl, Action<List<string>, string> onCompleted)
        {
            List<string> tags = null;
            string error = null;
            bool done = false;

            var thread = new Thread(() =>
            {
                try
                {
                    tags = Run(gitUrl);
                }
                catch (Win32Exception)
                {
                    error = "Git is not installed or is not in the PATH. The Package Manager needs it too.";
                }
                catch (Exception e)
                {
                    error = e.Message;
                }
                finally
                {
                    done = true;
                }
            }) { IsBackground = true };
            thread.Start();

            void Poll()
            {
                if (!done) return;
                EditorApplication.update -= Poll;
                onCompleted?.Invoke(tags, error);
            }

            EditorApplication.update += Poll;
        }

        private static List<string> Run(string gitUrl)
        {
            var startInfo = new ProcessStartInfo("git", $"ls-remote --tags --refs \"{gitUrl}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // Si el repositorio pidiera credenciales, mejor fallar que dejar el proceso colgado esperando.
            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(TIMEOUT_MS))
                {
                    try { process.Kill(); } catch (Exception) { /* ya había terminado */ }
                    throw new TimeoutException("git ls-remote did not answer in time. Check your connection.");
                }
                process.WaitForExit(); // asegura que se han recibido las últimas líneas de salida

                if (process.ExitCode != 0)
                {
                    var message = stderr.ToString().Trim();
                    throw new Exception(string.IsNullOrEmpty(message)
                        ? $"git ls-remote failed (exit code {process.ExitCode})."
                        : message);
                }
            }

            var tags = new List<string>();
            foreach (var line in stdout.ToString().Split('\n'))
            {
                int index = line.IndexOf(TAGS_PREFIX, StringComparison.Ordinal);
                if (index < 0) continue;
                var tag = line.Substring(index + TAGS_PREFIX.Length).Trim();
                if (tag.Length > 0) tags.Add(tag);
            }
            return tags;
        }
    }
}
