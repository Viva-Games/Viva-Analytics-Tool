using UnityEditor;
using UnityEngine;
using Viva.Core.Editor;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Detecta el SDK de Facebook (Facebook.Unity.dll) y mantiene el símbolo VIVA_FACEBOOK, que activa el
    /// assembly VivaGames.Analytics.Facebook con el tracker de Meta App Events.
    /// </summary>
    [InitializeOnLoad]
    public static class FacebookSdkDetector
    {
        public const string DEFINE = "VIVA_FACEBOOK";
        private const string FACEBOOK_DLL = "Facebook.Unity.dll";

        static FacebookSdkDetector()
        {
            if (Application.isBatchMode) Refresh();
            else EditorApplication.delayCall += () => Refresh();
        }

        public static bool IsSdkPresent() => SdkDetection.IsPrecompiledAssemblyPresent(FACEBOOK_DLL);

        public static bool IsDefineEnabled() => ScriptingDefines.IsDefined(DEFINE);

        public static bool Refresh() => SdkDetection.SyncDefine(DEFINE, FACEBOOK_DLL, "[Analytics]");
    }
}
