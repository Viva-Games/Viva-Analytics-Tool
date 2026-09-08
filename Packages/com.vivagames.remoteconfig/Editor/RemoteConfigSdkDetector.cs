using UnityEditor;
using UnityEngine;
using Viva.Core.Editor;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Detecta si el SDK de Firebase Remote Config está en el proyecto y mantiene el símbolo
    /// VIVA_FIREBASE_REMOTE_CONFIG, que activa el assembly VivaGames.RemoteConfig.Firebase.
    /// Ese assembly exige además VIVA_FIREBASE (Firebase.App.dll, detectado por Viva Core).
    /// </summary>
    [InitializeOnLoad]
    public static class RemoteConfigSdkDetector
    {
        public const string DEFINE = "VIVA_FIREBASE_REMOTE_CONFIG";
        private const string FIREBASE_REMOTE_CONFIG_DLL = "Firebase.RemoteConfig.dll";

        static RemoteConfigSdkDetector()
        {
            if (Application.isBatchMode)
            {
                // En builds por línea de comandos (CI) delayCall puede no llegar a ejecutarse antes de la build.
                Refresh();
            }
            else
            {
                // En el editor se pospone para no tocar PlayerSettings en mitad de la carga del dominio.
                EditorApplication.delayCall += () => Refresh();
            }
        }

        /// <summary>true si Firebase.RemoteConfig.dll está entre los assemblies precompilados del proyecto.</summary>
        public static bool IsSdkPresent()
        {
            return SdkDetection.IsPrecompiledAssemblyPresent(FIREBASE_REMOTE_CONFIG_DLL);
        }

        public static bool IsDefineEnabled()
        {
            return ScriptingDefines.IsDefined(DEFINE);
        }

        /// <summary>true si el core ha detectado Firebase.App.dll: sin él no hay VivaFirebase ni proveedor de Firebase.</summary>
        public static bool IsFirebaseAppDefineEnabled()
        {
            return ScriptingDefines.IsDefined(FirebaseAppDetector.DEFINE);
        }

        /// <summary>Sincroniza el símbolo con la presencia del SDK. Devuelve true si ha cambiado algo.</summary>
        public static bool Refresh()
        {
            return SdkDetection.SyncDefine(DEFINE, FIREBASE_REMOTE_CONFIG_DLL, RemoteConfigPackage.LOG_PREFIX);
        }
    }
}
