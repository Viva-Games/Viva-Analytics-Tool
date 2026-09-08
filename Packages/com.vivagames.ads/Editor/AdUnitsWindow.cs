using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Viva.Services.Ads.Editor
{
    /// <summary>
    /// Ventana Viva > Ads > Ad Units: se añaden los formatos que usa el juego (rewarded, interstitial, banner),
    /// con su ad unit de Android y de iOS y sus placements. Save escribe el JSON y regenera AdPlacements.cs.
    /// </summary>
    public class AdUnitsWindow : EditorWindow
    {
        private static readonly Color OkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.4f);

        private AdUnitsDefinition _definition = new AdUnitsDefinition();
        private string _savedSnapshot = string.Empty;
        private AdsValidationResult _validation = new AdsValidationResult();
        private AdsGeneratedState _generatedState;
        private readonly Dictionary<AdFormat, ReorderableList> _placementLists = new Dictionary<AdFormat, ReorderableList>();
        private Vector2 _scroll;

        [MenuItem("Viva/Ads/Ad Units")]
        public static void Open()
        {
            var window = GetWindow<AdUnitsWindow>("Ad Units");
            window.minSize = new Vector2(560, 420);
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnFocus()
        {
            if (!IsDirty) Reload();
            else RefreshGeneratedState();
        }

        private void OnDestroy()
        {
            if (!IsDirty) return;
            if (EditorUtility.DisplayDialog("Unsaved ad units", "You have unsaved changes in the ad units. Save them?", "Save", "Discard"))
            {
                if (!TrySave())
                {
                    var pending = _definition.Clone();
                    EditorApplication.delayCall += () =>
                    {
                        var window = GetWindow<AdUnitsWindow>("Ad Units");
                        window._definition = pending;
                        window.RebuildLists();
                        window.Validate();
                    };
                }
            }
        }

        #region GUI

        private void OnGUI()
        {
            DrawToolbar();
            DrawStatus();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            DrawFormat(AdFormat.Rewarded, "Rewarded", "Video the player chooses to watch for a reward (hint, continue, refill...).");
            DrawFormat(AdFormat.Interstitial, "Interstitial", "Full screen ad between levels or menus, shown by the game with TryShowInterstitial.");
            DrawFormat(AdFormat.Banner, "Banner", "Persistent strip at the top or bottom of the screen, shown and hidden by the game.");
            if (EditorGUI.EndChangeCheck()) Validate();
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool dirty = IsDirty;
            using (new EditorGUI.DisabledScope(!dirty || !_validation.IsValid))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    TrySave();
                    GUI.FocusControl(null);
                }
            }
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    Reload();
                    GUI.FocusControl(null);
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(dirty ? "unsaved changes" : "saved", EditorStyles.miniLabel);
            if (GUILayout.Button("Setup", EditorStyles.toolbarButton, GUILayout.Width(60)))
                AdsSetupWindow.Open();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatus()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(AdsEditorSettings.AdUnitsFilePath, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            var previous = GUI.color;
            switch (_generatedState)
            {
                case AdsGeneratedState.UpToDate:
                    GUI.color = OkColor;
                    GUILayout.Label("AdPlacements.cs up to date", EditorStyles.miniLabel);
                    break;
                case AdsGeneratedState.Outdated:
                    GUI.color = WarningColor;
                    GUILayout.Label("AdPlacements.cs outdated", EditorStyles.miniLabel);
                    break;
                default:
                    GUI.color = WarningColor;
                    GUILayout.Label("AdPlacements.cs missing", EditorStyles.miniLabel);
                    break;
            }
            GUI.color = previous;
            if (_generatedState != AdsGeneratedState.UpToDate && GUILayout.Button("Regenerate", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                AdsProjectFiles.RegenerateScript();
                RefreshGeneratedState();
            }
            EditorGUILayout.EndHorizontal();

            foreach (var warning in _validation.Warnings)
            {
                if (!warning.Format.HasValue) EditorGUILayout.HelpBox(warning.Message, MessageType.Warning);
            }
            EditorGUILayout.Space(4);
        }

        private void DrawFormat(AdFormat format, string title, string help)
        {
            var section = _definition.Get(format);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (!section.enabled)
            {
                if (GUILayout.Button($"Add {title.ToLowerInvariant()}", GUILayout.Width(140)))
                {
                    section.enabled = true;
                    if (section.placements.Count == 0) section.placements.Add(DefaultPlacement(format));
                    GUI.changed = true;
                }
            }
            else if (GUILayout.Button("Remove", GUILayout.Width(80)) &&
                     EditorUtility.DisplayDialog($"Remove {title}", $"Disable the {title.ToLowerInvariant()} format? Its ad units and placements stay in the file and come back if you add it again.", "Remove", "Cancel"))
            {
                section.enabled = false;
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();

            if (!section.enabled)
            {
                GUILayout.Label(help, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            section.androidAdUnitId = EditorGUILayout.TextField("Android ad unit id", section.androidAdUnitId);
            section.iosAdUnitId = EditorGUILayout.TextField("iOS ad unit id", section.iosAdUnitId);

            if (format == AdFormat.Banner) DrawBannerFields((BannerDefinition)section);

            GUILayout.Label($"Placements ({AdsNaming.EnumName(format)} enum)", EditorStyles.miniBoldLabel);
            if (_placementLists.TryGetValue(format, out var list)) list.DoLayoutList();

            foreach (var error in _validation.ErrorsOf(format)) EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            foreach (var warning in _validation.WarningsOf(format)) EditorGUILayout.HelpBox(warning.Message, MessageType.Warning);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private static void DrawBannerFields(BannerDefinition banner)
        {
            if (!Enum.TryParse(banner.position, out BannerPosition position)) position = BannerPosition.BottomCenter;
            var newPosition = (BannerPosition)EditorGUILayout.EnumPopup("Position", position);
            if (newPosition != position) banner.position = newPosition.ToString();

            banner.adaptive = EditorGUILayout.Toggle(new GUIContent("Adaptive", "Adaptive banners take the height MAX recommends for the device instead of the fixed 50 pt."), banner.adaptive);

            if (!ColorUtility.TryParseHtmlString(banner.backgroundColor, out Color color)) color = Color.black;
            var newColor = EditorGUILayout.ColorField(new GUIContent("Background color", "Shown behind the banner while it loads or if the creative is smaller."), color);
            if (newColor != color) banner.backgroundColor = "#" + ColorUtility.ToHtmlStringRGBA(newColor);
        }

        private void DrawFooter()
        {
            if (!_validation.IsValid)
                EditorGUILayout.HelpBox($"{_validation.Errors.Count} error(s). Fix them to save.", MessageType.Error);
            EditorGUILayout.HelpBox(
                "Ad units come from the MAX dashboard, one per format and platform. Placements are the names reported to MAX and " +
                "the members of the generated enums: the game calls AdsService.ShowRewarded(RewardedPlacement.Hint, ...). " +
                "Save writes the JSON and regenerates AdPlacements.cs.",
                MessageType.None);
        }

        #endregion

        #region Data

        private void Reload()
        {
            _definition = AdsProjectFiles.LoadDefinition();
            _savedSnapshot = AdUnitsFile.Serialize(_definition);
            RebuildLists();
            Validate();
            RefreshGeneratedState();
        }

        private void RebuildLists()
        {
            _placementLists.Clear();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                var section = _definition.Get(format);
                var captured = format;
                var list = new ReorderableList(section.placements, typeof(string), true, false, true, true)
                {
                    headerHeight = 0,
                    drawElementCallback = (rect, index, active, focused) =>
                    {
                        rect.y += 2;
                        rect.height = EditorGUIUtility.singleLineHeight;
                        var placements = _definition.Get(captured).placements;
                        if (index >= placements.Count) return;
                        float half = rect.width * 0.5f;
                        placements[index] = EditorGUI.TextField(new Rect(rect.x, rect.y, half - 4, rect.height), placements[index] ?? string.Empty);
                        GUI.Label(new Rect(rect.x + half, rect.y, half, rect.height), $"{AdsNaming.EnumName(captured)}.{AdsNaming.ToIdentifier(placements[index])}", EditorStyles.miniLabel);
                    },
                    onAddCallback = l =>
                    {
                        _definition.Get(captured).placements.Add("new_placement");
                        Validate();
                    },
                    onRemoveCallback = l =>
                    {
                        _definition.Get(captured).placements.RemoveAt(l.index);
                        Validate();
                    },
                    onReorderCallback = l => Validate()
                };
                _placementLists[format] = list;
            }
        }

        private void RefreshGeneratedState()
        {
            _generatedState = AdsProjectFiles.GetGeneratedState();
        }

        private void Validate()
        {
            _validation = AdsValidator.Validate(_definition);
        }

        private bool IsDirty => AdUnitsFile.Serialize(_definition) != _savedSnapshot;

        private bool TrySave()
        {
            Validate();
            if (!_validation.IsValid)
            {
                EditorUtility.DisplayDialog("Ad Units", $"Fix the {_validation.Errors.Count} error(s) before saving.", "OK");
                return false;
            }
            if (!AdsProjectFiles.SaveAndGenerate(_definition)) return false;
            _savedSnapshot = AdUnitsFile.Serialize(_definition);
            RefreshGeneratedState();
            return true;
        }

        private static string DefaultPlacement(AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Rewarded: return "reward";
                case AdFormat.Interstitial: return "level_start";
                default: return "main_menu";
            }
        }

        #endregion
    }
}
