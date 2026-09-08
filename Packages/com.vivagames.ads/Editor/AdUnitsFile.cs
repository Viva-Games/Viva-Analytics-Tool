using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Viva.Services.Ads.Editor
{
    /// <summary>Un formato tal y como está en el JSON: activo o no, ad unit por plataforma y placements.</summary>
    [Serializable]
    public class AdFormatDefinition
    {
        public bool enabled;
        public string androidAdUnitId = string.Empty;
        public string iosAdUnitId = string.Empty;
        public List<string> placements = new List<string>();

        public bool HasAnyAdUnitId => !string.IsNullOrWhiteSpace(androidAdUnitId) || !string.IsNullOrWhiteSpace(iosAdUnitId);
    }

    /// <summary>El banner añade posición y aspecto.</summary>
    [Serializable]
    public class BannerDefinition : AdFormatDefinition
    {
        public string position = BannerPosition.BottomCenter.ToString();
        public bool adaptive = true;
        public string backgroundColor = "#000000";
    }

    /// <summary>Contenido de Assets/VivaAds/AdUnits.json.</summary>
    [Serializable]
    public class AdUnitsDefinition
    {
        public AdFormatDefinition rewarded = new AdFormatDefinition();
        public AdFormatDefinition interstitial = new AdFormatDefinition();
        public BannerDefinition banner = new BannerDefinition();

        public AdFormatDefinition Get(AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Rewarded: return rewarded;
                case AdFormat.Interstitial: return interstitial;
                default: return banner;
            }
        }

        public AdUnitsDefinition Clone()
        {
            return AdUnitsFile.Parse(AdUnitsFile.Serialize(this));
        }
    }

    /// <summary>Lectura y escritura del JSON de ad units (la fuente de verdad que edita la ventana).</summary>
    public static class AdUnitsFile
    {
        public static AdUnitsDefinition Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new AdUnitsDefinition();
            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"{AdsPackage.LOG_PREFIX} Could not read {path}: {e.Message}");
                return new AdUnitsDefinition();
            }
        }

        public static AdUnitsDefinition Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new AdUnitsDefinition();
            var parsed = JsonUtility.FromJson<AdUnitsDefinition>(json) ?? new AdUnitsDefinition();
            parsed.rewarded = parsed.rewarded ?? new AdFormatDefinition();
            parsed.interstitial = parsed.interstitial ?? new AdFormatDefinition();
            parsed.banner = parsed.banner ?? new BannerDefinition();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                var definition = parsed.Get(format);
                definition.androidAdUnitId = definition.androidAdUnitId ?? string.Empty;
                definition.iosAdUnitId = definition.iosAdUnitId ?? string.Empty;
                definition.placements = definition.placements ?? new List<string>();
            }
            parsed.banner.position = string.IsNullOrEmpty(parsed.banner.position) ? BannerPosition.BottomCenter.ToString() : parsed.banner.position;
            parsed.banner.backgroundColor = string.IsNullOrEmpty(parsed.banner.backgroundColor) ? "#000000" : parsed.banner.backgroundColor;
            return parsed;
        }

        public static string Serialize(AdUnitsDefinition definition)
        {
            return JsonUtility.ToJson(definition, true) + "\n";
        }

        public static bool Save(string path, AdUnitsDefinition definition)
        {
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(path, Serialize(definition));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{AdsPackage.LOG_PREFIX} Could not write {path}: {e.Message}");
                return false;
            }
        }
    }
}
