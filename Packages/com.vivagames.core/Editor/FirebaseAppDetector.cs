using UnityEditor;
using UnityEngine;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Detecta si el SDK base de Firebase (Firebase.App.dll) está en el proyecto y mantiene el símbolo
    /// VIVA_FIREBASE, que activa el assembly VivaGames.Core.Firebase con la comprobación de dependencias
    /// compartida (VivaFirebase). Los módulos que usan Firebase la esperan a través de ella.
    /// </summary>
    [InitializeOnLoad]
    public static class FirebaseAppDetector
    {
        public const string DEFINE = "VIVA_FIREBASE";
        private const string FIREBASE_APP_DLL = "Firebase.App.dll";

        static FirebaseAppDetector()
        {
            if (Application.isBatchMode)
            {
                // En builds por línea de comandos (CI) delayCall puede no llegar a ejecutarse antes de la build:
                // se sincroniza el define en el acto para que la plataforma que se compila lo tenga.
                Refresh();
            }
            else
            {
                // En el editor se pospone para no tocar PlayerSettings en mitad de la carga del dominio.
                EditorApplication.delayCall += () => Refresh();
            }
        }

        /// <summary>true si Firebase.App.dll está entre los assemblies precompilados del proyecto.</summary>
        public static bool IsFirebaseAppPresent()
        {
            return SdkDetection.IsPrecompiledAssemblyPresent(FIREBASE_APP_DLL);
        }

        public static bool IsDefineEnabled()
        {
            return ScriptingDefines.IsDefined(DEFINE);
        }

        /// <summary>Sincroniza el símbolo con la presencia del SDK. Devuelve true si ha cambiado algo.</summary>
        public static bool Refresh()
        {
            return SdkDetection.SyncDefine(DEFINE, FIREBASE_APP_DLL, "[Viva]");
        }
    }
}
