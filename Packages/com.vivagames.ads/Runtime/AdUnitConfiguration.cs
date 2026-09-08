using System;
using System.Collections.Generic;
using UnityEngine;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Ad unit de un formato por plataforma y sus placements. La clase generada AdPlacements.Configuration()
    /// crea uno por formato activo; el enum de placements se registra para validar las llamadas.
    /// </summary>
    public class AdUnitSetup
    {
        public string AndroidAdUnitId { get; }
        public string IosAdUnitId { get; }

        /// <summary>Enum generado para este formato (RewardedPlacement, InterstitialPlacement, BannerPlacement).</summary>
        public Type PlacementEnumType { get; }

        /// <summary>Nombres de placement en el orden del enum: el valor ordinal N es PlacementNames[N].</summary>
        public IReadOnlyList<string> PlacementNames { get; }

        public AdUnitSetup(string androidAdUnitId, string iosAdUnitId, Type placementEnumType, string[] placementNames)
        {
            AndroidAdUnitId = androidAdUnitId ?? string.Empty;
            IosAdUnitId = iosAdUnitId ?? string.Empty;
            PlacementEnumType = placementEnumType;
            PlacementNames = placementNames ?? new string[0];
        }

        /// <summary>Ad unit de la plataforma en la que corre el juego. En el editor, el de Android o, si falta, el de iOS.</summary>
        public string AdUnitId
        {
            get
            {
#if UNITY_IOS
                return !string.IsNullOrEmpty(IosAdUnitId) ? IosAdUnitId : AndroidAdUnitId;
#else
                return !string.IsNullOrEmpty(AndroidAdUnitId) ? AndroidAdUnitId : IosAdUnitId;
#endif
            }
        }

        public bool HasAdUnitId => !string.IsNullOrEmpty(AdUnitId);

        /// <summary>
        /// Nombre de placement de un valor del enum generado. false si el valor no es de este formato
        /// o está fuera de la lista.
        /// </summary>
        public bool TryResolvePlacement(Enum placement, out string name)
        {
            name = null;
            if (placement == null || PlacementEnumType == null || placement.GetType() != PlacementEnumType) return false;

            int index = Convert.ToInt32(placement);
            if (index < 0 || index >= PlacementNames.Count) return false;
            name = PlacementNames[index];
            return !string.IsNullOrEmpty(name);
        }

        public bool HasPlacement(string name)
        {
            foreach (var placement in PlacementNames)
            {
                if (placement == name) return true;
            }
            return false;
        }
    }

    /// <summary>Banner: ad unit, placements, posición y aspecto.</summary>
    public sealed class BannerSetup : AdUnitSetup
    {
        public BannerPosition Position { get; }
        public bool Adaptive { get; }
        public Color BackgroundColor { get; }

        public BannerSetup(string androidAdUnitId, string iosAdUnitId, Type placementEnumType, string[] placementNames,
            BannerPosition position, bool adaptive, Color backgroundColor)
            : base(androidAdUnitId, iosAdUnitId, placementEnumType, placementNames)
        {
            Position = position;
            Adaptive = adaptive;
            BackgroundColor = backgroundColor;
        }
    }

    /// <summary>Configuración completa que consume <see cref="AdsService.Initialize"/>. null en un formato = no activo.</summary>
    public sealed class AdUnitConfiguration
    {
        public AdUnitSetup Rewarded;
        public AdUnitSetup Interstitial;
        public BannerSetup Banner;

        public AdUnitSetup Get(AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Rewarded: return Rewarded;
                case AdFormat.Interstitial: return Interstitial;
                case AdFormat.Banner: return Banner;
                default: return null;
            }
        }

        public bool IsEnabled(AdFormat format)
        {
            var setup = Get(format);
            return setup != null && setup.HasAdUnitId;
        }

        /// <summary>Ad units de los formatos activos, para InitializeSdk.</summary>
        public string[] EnabledAdUnitIds()
        {
            var ids = new List<string>();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                if (IsEnabled(format)) ids.Add(Get(format).AdUnitId);
            }
            return ids.ToArray();
        }
    }
}
