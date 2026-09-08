using System;
using UnityEditor;
using UnityEngine;

namespace Viva.Services.Ads.Editor
{
    /// <summary>
    /// Ventana Viva > Ads > Setup: estado del proyecto (JSON, clase generada, plugin de MAX, Integration
    /// Manager, AdsInit, analíticas), ajustes de rutas y configuración inicial.
    /// </summary>
    public class AdsSetupWindow : EditorWindow
    {
        private static readonly Color OkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.4f);

        private string _adUnitsPathDraft;
        private string _generatedPathDraft;
        private string _initPathDraft;
        private Vector2 _scroll;

        [MenuItem("Viva/Ads/Setup")]
        public static void Open()
        {
            var window = GetWindow<AdsSetupWindow>("Ads Setup");
            window.minSize = new Vector2(620, 520);
        }

        [InitializeOnLoadMethod]
        private static void OpenOnFirstRun()
        {
            if (Application.isBatchMode) return;
            if (AdsEditorSettings.Instance.setupShown) return;

            EditorApplication.delayCall += () =>
            {
                AdsEditorSettings.Instance.setupShown = true;
                AdsEditorSettings.Instance.Save();
                OfferInitialSetup();
                Open();
            };
        }

        private static void OfferInitialSetup()
        {
            var message = "Viva Ads is installed. The initial setup will:\n\n";
            message += "1. Create " + AdsEditorSettings.AdUnitsFilePath + ", where you declare the formats, ad units and placements.\n";
            message += "2. Generate " + AdsEditorSettings.GeneratedScriptPath + ", the AdPlacements enums the game uses.\n";
            message += "3. Generate " + AdsEditorSettings.InitScriptPath + ": add the AdsInit component to the first scene.\n";
            message += "\nThe AppLovin MAX plugin and its Integration Manager (SDK key, adapters, consent flow) are set up by you; Setup checks them.";
            message += "\n\nYou can also run it later from Viva > Ads > Setup.";

            if (EditorUtility.DisplayDialog("Set up Viva Ads", message, "Run setup", "Later"))
                RunFullSetup();
        }

        private void OnEnable()
        {
            _adUnitsPathDraft = AdsEditorSettings.AdUnitsFilePath;
            _generatedPathDraft = AdsEditorSettings.GeneratedScriptPath;
            _initPathDraft = AdsEditorSettings.InitScriptPath;
        }

        private void OnFocus()
        {
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"Viva Ads {AdsPackage.Version}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawProjectSection();
            DrawSdkSection();
            DrawAnalyticsSection();
            DrawSettingsSection();
            DrawHelpSection();

            EditorGUILayout.EndScrollView();
        }

        #region Sections

        private void DrawProjectSection()
        {
            GUILayout.Label("Project status", EditorStyles.boldLabel);

            bool fileExists = AdsProjectFiles.AdUnitsFileExists;
            DrawRow(fileExists, "Ad units file", fileExists ? AdsEditorSettings.AdUnitsFilePath : "missing",
                fileExists ? "Open" : "Create", () =>
                {
                    if (fileExists) AdUnitsWindow.Open();
                    else AdsProjectFiles.CreateAdUnitsFileIfMissing();
                });

            var state = AdsProjectFiles.GetGeneratedState();
            string detail;
            switch (state)
            {
                case AdsGeneratedState.UpToDate: detail = AdsEditorSettings.GeneratedScriptPath; break;
                case AdsGeneratedState.Outdated: detail = "outdated: the JSON changed after the last generation"; break;
                default: detail = "missing"; break;
            }
            DrawRow(state == AdsGeneratedState.UpToDate, "AdPlacements.cs", detail,
                state == AdsGeneratedState.UpToDate ? null : "Generate", () => AdsProjectFiles.RegenerateScript());

            bool initExists = AdsProjectFiles.InitScriptExists;
            DrawRow(initExists, "AdsInit.cs",
                initExists ? AdsEditorSettings.InitScriptPath + ". Add the AdsInit component to the first scene." : "missing",
                initExists ? null : "Generate", () => AdsProjectFiles.GenerateInitScript());

            EditorGUILayout.Space();
            if (GUILayout.Button("Run full setup", GUILayout.Height(28)))
                RunFullSetup();
            EditorGUILayout.Space();
        }

        private void DrawSdkSection()
        {
            GUILayout.Label("AppLovin MAX", EditorStyles.boldLabel);

            bool present = MaxSdkDetector.IsSdkPresent();
            bool defineEnabled = MaxSdkDetector.IsDefineEnabled();
            string sdkDetail;
            if (MaxSdkDetector.IsLegacyWithoutAssembly())
                sdkDetail = "found in Assets/MaxSdk but without MaxSdk.Scripts.asmdef: update the plugin (8.x ships it).";
            else if (present && defineEnabled)
                sdkDetail = $"{MaxSdkDetector.Describe()} ({MaxSdkDetector.DEFINE} enabled)";
            else if (present)
                sdkDetail = $"{MaxSdkDetector.Describe()}, enabling {MaxSdkDetector.DEFINE}...";
            else
                sdkDetail = "not found. Install the MAX Unity plugin (Package Manager registry or .unitypackage); until then ads are disabled.";
            DrawRow(present && defineEnabled && !MaxSdkDetector.IsLegacyWithoutAssembly(), "Plugin", sdkDetail, "Re-check", () => MaxSdkDetector.Refresh());

            if (!present)
            {
                EditorGUILayout.Space();
                return;
            }

            string sdkKey = MaxIntegrationChecks.ReadSdkKey();
            if (sdkKey == null)
                DrawRow(false, "SDK key", "AppLovin > Integration Manager has not been saved yet (" + MaxIntegrationChecks.SETTINGS_ASSET_PATH + " missing).", null, null);
            else
                DrawRow(sdkKey.Length > 0, "SDK key", sdkKey.Length > 0 ? "configured" : "empty: enter it in AppLovin > Integration Manager > SDK Settings.", null, null);

            bool hasConsentSettings = MaxIntegrationChecks.ReadConsentFlow(out bool consentEnabled, out string privacyUrl);
            bool consentOk = hasConsentSettings && consentEnabled && !string.IsNullOrWhiteSpace(privacyUrl);
            string consentDetail;
            if (!hasConsentSettings) consentDetail = "not configured: enable the MAX Terms and Privacy Policy Flow in the Integration Manager (Privacy Settings).";
            else if (!consentEnabled) consentDetail = "disabled: enable the MAX Terms and Privacy Policy Flow (Google UMP for GDPR regions, ATT on iOS).";
            else if (string.IsNullOrWhiteSpace(privacyUrl)) consentDetail = "enabled but without a privacy policy URL, which MAX requires.";
            else consentDetail = "enabled, " + privacyUrl;
            DrawRow(consentOk, "Consent flow", consentDetail, null, null);

            int adapters = MaxIntegrationChecks.CountAdapters();
            DrawRow(adapters > 0, "Mediation adapters", adapters > 0 ? $"{adapters} installed" : "none installed: add networks in the Integration Manager (AdMob, Unity Ads...).", null, null);

            EditorGUILayout.Space();
        }

        private void DrawAnalyticsSection()
        {
            GUILayout.Label("Viva Analytics", EditorStyles.boldLabel);
            bool installed = AdsProjectFiles.IsAnalyticsInstalled();
            if (!installed)
            {
                DrawRow(true, "Not installed", "Consent and ad revenue can still be forwarded from AdsInit.cs to your own SDKs.", null, null);
                EditorGUILayout.Space();
                return;
            }

            DrawRow(true, "Consent", "automatic: the CMP result reaches Firebase Consent Mode through Viva Core.", null, null);
            bool hasEvent = AdsProjectFiles.HasAdImpressionEvent();
            DrawRow(hasEvent, "ad_impression",
                hasEvent ? "event in the project; AdsInit.cs generated now tracks every impression." : "standard event not imported: add it in Viva > Analytics > Event Manager, then generate AdsInit.cs (or add the call by hand).",
                null, null);
            EditorGUILayout.Space();
        }

        private void DrawSettingsSection()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            _adUnitsPathDraft = EditorGUILayout.TextField("Ad units file", _adUnitsPathDraft);
            _generatedPathDraft = EditorGUILayout.TextField("Generated class path", _generatedPathDraft);
            _initPathDraft = EditorGUILayout.TextField("AdsInit.cs path", _initPathDraft);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool dirty = _adUnitsPathDraft != AdsEditorSettings.AdUnitsFilePath
                         || _generatedPathDraft != AdsEditorSettings.GeneratedScriptPath
                         || _initPathDraft != AdsEditorSettings.InitScriptPath;
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Apply", GUILayout.Width(80))) ApplySettings();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Paths are relative to the project and must be inside Assets. Stored in ProjectSettings/VivaAdsSettings.json. Changing a path does not move the existing files.", MessageType.None);
            EditorGUILayout.Space();
        }

        private static void DrawHelpSection()
        {
            GUILayout.Label("How it works", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Install the MAX plugin and configure the Integration Manager: SDK key, mediation adapters, Terms and Privacy Policy Flow.\n" +
                "2. Declare the formats, ad units and placements in Viva > Ads > Ad Units and Save.\n" +
                "3. Put the AdsInit component in the first scene. With Viva Analytics installed, consent and ad revenue are wired.\n" +
                "4. The game calls AdsService.ShowRewarded(RewardedPlacement.X, result => ...), TryShowInterstitial(InterstitialPlacement.X, onClosed) and ShowBanner(BannerPlacement.X), " +
                "or drops the Rewarded Ad Button component on a button.",
                MessageType.None);
        }

        #endregion

        private static void RunFullSetup()
        {
            AdsProjectFiles.CreateAdUnitsFileIfMissing();
            if (AdsProjectFiles.GetGeneratedState() != AdsGeneratedState.UpToDate) AdsProjectFiles.RegenerateScript();
            AdsProjectFiles.GenerateInitScript();
        }

        private void ApplySettings()
        {
            if (!AdsEditorSettings.IsValidProjectPath(_adUnitsPathDraft)
                || !_adUnitsPathDraft.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || !AdsEditorSettings.IsValidProjectPath(_generatedPathDraft)
                || !_generatedPathDraft.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || !AdsEditorSettings.IsValidProjectPath(_initPathDraft)
                || !_initPathDraft.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid path", "All paths must start with Assets/. The ad units file must end with .json and the scripts with .cs.", "OK");
                return;
            }

            var settings = AdsEditorSettings.Instance;
            settings.adUnitsFilePath = _adUnitsPathDraft;
            settings.generatedScriptPath = _generatedPathDraft;
            settings.initScriptPath = _initPathDraft;
            settings.Save();

            _adUnitsPathDraft = AdsEditorSettings.AdUnitsFilePath;
            _generatedPathDraft = AdsEditorSettings.GeneratedScriptPath;
            _initPathDraft = AdsEditorSettings.InitScriptPath;
        }

        private static void DrawRow(bool ok, string label, string detail, string buttonLabel, Action action)
        {
            EditorGUILayout.BeginHorizontal();
            var previousColor = GUI.color;
            GUI.color = ok ? OkColor : WarningColor;
            GUILayout.Label(ok ? "OK" : "!", EditorStyles.boldLabel, GUILayout.Width(24));
            GUI.color = previousColor;
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label(detail, EditorStyles.wordWrappedLabel);
            if (buttonLabel != null && action != null && GUILayout.Button(buttonLabel, GUILayout.Width(90)))
                action();
            EditorGUILayout.EndHorizontal();
        }
    }
}
