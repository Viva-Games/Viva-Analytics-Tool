using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Viva.Services.Ads.Editor
{
    public sealed class AdsValidationIssue
    {
        /// <summary>Formato al que pertenece, o null si es general.</summary>
        public AdFormat? Format;
        public string Message;

        public AdsValidationIssue(AdFormat? format, string message)
        {
            Format = format;
            Message = message;
        }

        public override string ToString() => Format.HasValue ? $"{Format}: {Message}" : Message;
    }

    public sealed class AdsValidationResult
    {
        public readonly List<AdsValidationIssue> Errors = new List<AdsValidationIssue>();
        public readonly List<AdsValidationIssue> Warnings = new List<AdsValidationIssue>();

        public bool IsValid => Errors.Count == 0;

        public IEnumerable<AdsValidationIssue> ErrorsOf(AdFormat format)
        {
            foreach (var error in Errors) if (error.Format == format) yield return error;
        }

        public IEnumerable<AdsValidationIssue> WarningsOf(AdFormat format)
        {
            foreach (var warning in Warnings) if (warning.Format == format) yield return warning;
        }
    }

    /// <summary>
    /// Reglas del JSON de ad units antes de guardarse: un formato activo necesita ad unit y placements, los
    /// placements deben ser únicos y dar un miembro de enum válido, y el banner una posición y un color válidos.
    /// </summary>
    public static class AdsValidator
    {
        private static readonly Regex PlacementPattern = new Regex("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
        private static readonly Regex AdUnitPattern = new Regex("^[0-9a-fA-F]{16}$", RegexOptions.Compiled);
        private static readonly Regex ColorPattern = new Regex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

        public static AdsValidationResult Validate(AdUnitsDefinition definition)
        {
            var result = new AdsValidationResult();
            bool anyEnabled = false;

            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                var section = definition.Get(format);
                if (section == null || !section.enabled) continue;
                anyEnabled = true;

                ValidateAdUnits(format, section, result);
                ValidatePlacements(format, section, result);
            }

            if (definition.banner != null && definition.banner.enabled)
            {
                if (!Enum.TryParse(definition.banner.position, out BannerPosition _))
                    result.Errors.Add(new AdsValidationIssue(AdFormat.Banner, $"Unknown banner position \"{definition.banner.position}\"."));
                if (!ColorPattern.IsMatch(definition.banner.backgroundColor ?? string.Empty))
                    result.Errors.Add(new AdsValidationIssue(AdFormat.Banner, $"Background color \"{definition.banner.backgroundColor}\" must be #RRGGBB or #RRGGBBAA."));
            }

            if (!anyEnabled) result.Warnings.Add(new AdsValidationIssue(null, "No ad format is enabled: the generated class will have no placements."));
            return result;
        }

        private static void ValidateAdUnits(AdFormat format, AdFormatDefinition section, AdsValidationResult result)
        {
            string android = (section.androidAdUnitId ?? string.Empty).Trim();
            string ios = (section.iosAdUnitId ?? string.Empty).Trim();

            if (android.Length == 0 && ios.Length == 0)
            {
                result.Errors.Add(new AdsValidationIssue(format, "An enabled format needs at least one ad unit id (Android or iOS)."));
                return;
            }
            if (android.Length == 0) result.Warnings.Add(new AdsValidationIssue(format, "No Android ad unit id: the format will be disabled on Android."));
            if (ios.Length == 0) result.Warnings.Add(new AdsValidationIssue(format, "No iOS ad unit id: the format will be disabled on iOS."));
            if (android.Length > 0 && !AdUnitPattern.IsMatch(android)) result.Warnings.Add(new AdsValidationIssue(format, $"Android ad unit id \"{android}\" does not look like a MAX ad unit id (16 hex characters)."));
            if (ios.Length > 0 && !AdUnitPattern.IsMatch(ios)) result.Warnings.Add(new AdsValidationIssue(format, $"iOS ad unit id \"{ios}\" does not look like a MAX ad unit id (16 hex characters)."));
        }

        private static void ValidatePlacements(AdFormat format, AdFormatDefinition section, AdsValidationResult result)
        {
            var placements = section.placements ?? new List<string>();
            if (placements.Count == 0)
            {
                result.Errors.Add(new AdsValidationIssue(format, "Add at least one placement: the API needs one to show the ad."));
                return;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < placements.Count; i++)
            {
                string placement = placements[i] ?? string.Empty;
                string error = ValidatePlacement(placement);
                if (error != null)
                {
                    result.Errors.Add(new AdsValidationIssue(format, $"Placement #{i + 1}: {error}"));
                    continue;
                }
                if (!names.Add(placement))
                {
                    result.Errors.Add(new AdsValidationIssue(format, $"Duplicated placement \"{placement}\"."));
                    continue;
                }

                string identifier = AdsNaming.ToIdentifier(placement);
                if (!AdsNaming.IsValidIdentifier(identifier))
                    result.Errors.Add(new AdsValidationIssue(format, $"Placement \"{placement}\" does not produce a valid enum member ({identifier})."));
                else if (identifiers.TryGetValue(identifier, out string other))
                    result.Errors.Add(new AdsValidationIssue(format, $"Placements \"{placement}\" and \"{other}\" would both become {AdsNaming.EnumName(format)}.{identifier}."));
                else
                    identifiers[identifier] = placement;
            }
        }

        /// <summary>null si el placement es válido; si no, el motivo.</summary>
        public static string ValidatePlacement(string placement)
        {
            if (string.IsNullOrWhiteSpace(placement)) return "the placement is empty.";
            if (!PlacementPattern.IsMatch(placement)) return $"\"{placement}\" may only contain letters, digits and underscores.";
            return null;
        }
    }
}
