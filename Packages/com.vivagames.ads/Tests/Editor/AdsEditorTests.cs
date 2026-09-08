using System.Collections.Generic;
using NUnit.Framework;
using Viva.Services.Ads.Editor;

namespace Viva.Services.Ads.Tests
{
    public class AdsEditorTests
    {
        private static AdUnitsDefinition ArrowsLike()
        {
            return new AdUnitsDefinition
            {
                rewarded = new AdFormatDefinition { enabled = true, androidAdUnitId = "f760b9e92755eb16", iosAdUnitId = "", placements = new List<string> { "hint", "continue", "daily_recharge" } },
                interstitial = new AdFormatDefinition { enabled = true, androidAdUnitId = "11fd751ba36b110d", iosAdUnitId = "11fd751ba36b110d", placements = new List<string> { "level_start" } },
                banner = new BannerDefinition { enabled = false },
            };
        }

        [TestCase("hint", "Hint")]
        [TestCase("daily_recharge", "DailyRecharge")]
        [TestCase("levelStart", "LevelStart")]
        [TestCase("MAIN_MENU", "MainMenu")]
        [TestCase("continue", "Continue")]
        [TestCase("1st", "_1st")]
        public void Naming(string placement, string expected)
        {
            Assert.AreEqual(expected, AdsNaming.ToIdentifier(placement));
        }

        [Test]
        public void Validate_ArrowsLike_IsValidWithIosWarnings()
        {
            var result = AdsValidator.Validate(ArrowsLike());
            Assert.IsTrue(result.IsValid, string.Join("\n", result.Errors));
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains("No iOS ad unit id", result.Warnings[0].Message);
            Assert.AreEqual(AdFormat.Rewarded, result.Warnings[0].Format);
        }

        [Test]
        public void Validate_EnabledFormatNeedsAdUnitAndPlacements()
        {
            var definition = new AdUnitsDefinition { rewarded = new AdFormatDefinition { enabled = true } };
            var result = AdsValidator.Validate(definition);
            Assert.IsFalse(result.IsValid);
            var errors = new List<AdsValidationIssue>(result.ErrorsOf(AdFormat.Rewarded));
            Assert.AreEqual(2, errors.Count);
            StringAssert.Contains("at least one ad unit id", errors[0].Message);
            StringAssert.Contains("at least one placement", errors[1].Message);
        }

        [Test]
        public void Validate_DisabledFormats_AreIgnored_AndWarnWhenNothingIsEnabled()
        {
            var result = AdsValidator.Validate(new AdUnitsDefinition());
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains("No ad format is enabled", result.Warnings[0].Message);
        }

        [Test]
        public void Validate_Placements_FormatDuplicatesAndCollisions()
        {
            var definition = ArrowsLike();
            definition.rewarded.placements = new List<string> { "hint", "hint", "my-placement", "", "daily_recharge", "dailyRecharge" };
            var result = AdsValidator.Validate(definition);
            var messages = string.Join("\n", result.ErrorsOf(AdFormat.Rewarded));

            StringAssert.Contains("Duplicated placement \"hint\"", messages);
            StringAssert.Contains("letters, digits and underscores", messages);
            StringAssert.Contains("is empty", messages);
            StringAssert.Contains("would both become RewardedPlacement.DailyRecharge", messages);
        }

        [Test]
        public void Validate_AdUnitFormat_OnlyWarns()
        {
            var definition = ArrowsLike();
            definition.rewarded.androidAdUnitId = "not-an-ad-unit";
            var result = AdsValidator.Validate(definition);
            Assert.IsTrue(result.IsValid);
            StringAssert.Contains("does not look like a MAX ad unit id", string.Join("\n", result.WarningsOf(AdFormat.Rewarded)));
        }

        [Test]
        public void Validate_Banner_PositionAndColor()
        {
            var definition = ArrowsLike();
            definition.banner = new BannerDefinition { enabled = true, androidAdUnitId = "aaaaaaaaaaaaaaaa", iosAdUnitId = "aaaaaaaaaaaaaaaa", placements = new List<string> { "menu" }, position = "Middle", backgroundColor = "red" };
            var result = AdsValidator.Validate(definition);
            var messages = string.Join("\n", result.ErrorsOf(AdFormat.Banner));
            StringAssert.Contains("Unknown banner position", messages);
            StringAssert.Contains("#RRGGBB", messages);

            definition.banner.position = "TopCenter";
            definition.banner.backgroundColor = "#FF000080";
            Assert.IsTrue(AdsValidator.Validate(definition).IsValid);
        }

        [Test]
        public void File_RoundTrip()
        {
            var definition = ArrowsLike();
            definition.banner = new BannerDefinition { enabled = true, androidAdUnitId = "aaaaaaaaaaaaaaaa", placements = new List<string> { "menu" }, position = "TopCenter", adaptive = false, backgroundColor = "#123456" };

            var parsed = AdUnitsFile.Parse(AdUnitsFile.Serialize(definition));

            Assert.IsTrue(parsed.rewarded.enabled);
            CollectionAssert.AreEqual(definition.rewarded.placements, parsed.rewarded.placements);
            Assert.AreEqual("11fd751ba36b110d", parsed.interstitial.iosAdUnitId);
            Assert.AreEqual("TopCenter", parsed.banner.position);
            Assert.IsFalse(parsed.banner.adaptive);
            Assert.AreEqual("#123456", parsed.banner.backgroundColor);
        }

        [Test]
        public void File_ParseTolerantToMissingSections()
        {
            var parsed = AdUnitsFile.Parse("{\"rewarded\":{\"enabled\":true,\"androidAdUnitId\":\"abc\"}}");
            Assert.IsTrue(parsed.rewarded.enabled);
            Assert.IsNotNull(parsed.rewarded.placements);
            Assert.IsNotNull(parsed.interstitial);
            Assert.IsNotNull(parsed.banner);
            Assert.AreEqual("BottomCenter", parsed.banner.position);
            Assert.AreEqual(0, AdUnitsFile.Parse("").rewarded.placements.Count);
        }

        [Test]
        public void Generate_ProducesEnumsAndConfiguration()
        {
            var definition = ArrowsLike();
            definition.banner = new BannerDefinition { enabled = true, androidAdUnitId = "aaaaaaaaaaaaaaaa", iosAdUnitId = "", placements = new List<string> { "main_menu" }, position = "TopCenter", adaptive = true, backgroundColor = "#FF0000" };

            string code = AdsCodeGenerator.Generate(definition, "Assets/VivaAds/AdUnits.json");

            const string expected = @"// <auto-generated>
// Generated by Viva Ads from Assets/VivaAds/AdUnits.json.
// Edit the ad units and placements in Viva > Ads > Ad Units; do not edit this file.
// </auto-generated>
using UnityEngine;

namespace Viva.Services.Ads
{
    /// <summary>Rewarded placements. The name passed to the SDK is the one declared in the window.</summary>
    public enum RewardedPlacement
    {
        /// <summary>hint</summary>
        Hint = 0,
        /// <summary>continue</summary>
        Continue = 1,
        /// <summary>daily_recharge</summary>
        DailyRecharge = 2,
    }

    /// <summary>Interstitial placements. The name passed to the SDK is the one declared in the window.</summary>
    public enum InterstitialPlacement
    {
        /// <summary>level_start</summary>
        LevelStart = 0,
    }

    /// <summary>Banner placements. The name passed to the SDK is the one declared in the window.</summary>
    public enum BannerPlacement
    {
        /// <summary>main_menu</summary>
        MainMenu = 0,
    }

    /// <summary>Ad units and placements declared in Viva > Ads > Ad Units.</summary>
    public static class AdPlacements
    {
        /// <summary>Configuration for AdsService.Initialize.</summary>
        public static AdUnitConfiguration Configuration() => new AdUnitConfiguration
        {
            Rewarded = new AdUnitSetup(""f760b9e92755eb16"", """", typeof(RewardedPlacement), new[] { ""hint"", ""continue"", ""daily_recharge"" }),
            Interstitial = new AdUnitSetup(""11fd751ba36b110d"", ""11fd751ba36b110d"", typeof(InterstitialPlacement), new[] { ""level_start"" }),
            Banner = new BannerSetup(""aaaaaaaaaaaaaaaa"", """", typeof(BannerPlacement), new[] { ""main_menu"" },
                BannerPosition.TopCenter, true, new Color(1.0f, 0.0f, 0.0f, 1.0f)),
        };
    }
}";
            Assert.AreEqual(Normalize(expected), Normalize(code));
        }

        [Test]
        public void Generate_DisabledFormats_AreNullAndHaveNoEnum()
        {
            string code = AdsCodeGenerator.Generate(new AdUnitsDefinition(), "x.json");
            StringAssert.DoesNotContain("enum RewardedPlacement", code);
            StringAssert.Contains("Rewarded = null,", code);
            StringAssert.Contains("Interstitial = null,", code);
            StringAssert.Contains("Banner = null,", code);
        }

        [Test]
        public void ColorLiteral_ParsesHexWithAlpha()
        {
            Assert.AreEqual("new Color(0.0f, 0.0f, 0.0f, 1.0f)", AdsCodeGenerator.ColorLiteral("#000000"));
            Assert.AreEqual("new Color(1.0f, 1.0f, 1.0f, 0.502f)", AdsCodeGenerator.ColorLiteral("#FFFFFF80"));
            Assert.AreEqual("new Color(0.0f, 0.0f, 0.0f, 1.0f)", AdsCodeGenerator.ColorLiteral("garbage"));
        }

        private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();
    }
}
