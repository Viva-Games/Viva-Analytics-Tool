using System.Text;

namespace Viva.Services.Ads.Editor
{
    /// <summary>
    /// Nombre de placement → miembro del enum generado (PascalCase), con la misma regla que Remote Config:
    /// daily_recharge → DailyRecharge, levelStart → LevelStart, MAIN_MENU → MainMenu. El placement se pasa a MAX tal cual.
    /// </summary>
    public static class AdsNaming
    {
        public const string REWARDED_ENUM = "RewardedPlacement";
        public const string INTERSTITIAL_ENUM = "InterstitialPlacement";
        public const string BANNER_ENUM = "BannerPlacement";

        public static string EnumName(AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Rewarded: return REWARDED_ENUM;
                case AdFormat.Interstitial: return INTERSTITIAL_ENUM;
                default: return BANNER_ENUM;
            }
        }

        public static string ToIdentifier(string placement)
        {
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(placement))
            {
                bool startOfWord = true;
                for (int i = 0; i < placement.Length; i++)
                {
                    char c = placement[i];
                    if (c == '_' || c == '-' || c == ' ' || c == '.')
                    {
                        startOfWord = true;
                        continue;
                    }
                    if (!char.IsLetterOrDigit(c)) continue;

                    if (i > 0 && char.IsUpper(c))
                    {
                        char previous = placement[i - 1];
                        bool afterLowerOrDigit = char.IsLower(previous) || char.IsDigit(previous);
                        bool endOfAcronym = char.IsUpper(previous) && i + 1 < placement.Length && char.IsLower(placement[i + 1]);
                        if (afterLowerOrDigit || endOfAcronym) startOfWord = true;
                    }

                    result.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    startOfWord = false;
                }
            }

            if (result.Length == 0) return "_";
            if (char.IsDigit(result[0])) result.Insert(0, '_');
            return result.ToString();
        }

        public static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            if (!(char.IsLetter(identifier[0]) || identifier[0] == '_')) return false;
            for (int i = 1; i < identifier.Length; i++)
            {
                if (!(char.IsLetterOrDigit(identifier[i]) || identifier[i] == '_')) return false;
            }
            return true;
        }
    }
}
