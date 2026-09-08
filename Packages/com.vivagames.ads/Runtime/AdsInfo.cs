namespace Viva.Services.Ads
{
    /// <summary>Datos de una impresión con ingresos, tal y como los reporta el SDK. Para ad_impression, Singular, Adjust...</summary>
    public sealed class AdRevenueInfo
    {
        public AdFormat Format;
        /// <summary>Placement con el que se mostró (el de la ventana), o vacío en el banner sin placement.</summary>
        public string Placement;
        public string AdUnitId;
        /// <summary>Red que sirvió el anuncio (AdMob, Unity Ads...).</summary>
        public string NetworkName;
        public string NetworkPlacement;
        public string CreativeId;
        public double Revenue;
        /// <summary>Precisión del ingreso según el SDK: exact, estimated, publisher_defined, undefined.</summary>
        public string RevenuePrecision;
        public string Currency = "USD";

        /// <summary>Nombre del formato como lo espera el evento ad_impression (REWARDED, INTER, BANNER en MAX).</summary>
        public string FormatName;
    }

    /// <summary>Lo que el CMP de MAX ha resuelto, para SDKs que no pasan por Viva Core (el consentimiento a analíticas va por VivaConsent).</summary>
    public sealed class AdsConsentInfo
    {
        /// <summary>true si el usuario está en región GDPR.</summary>
        public bool IsGdpr;
        /// <summary>Unknown, Gdpr u Other.</summary>
        public string Geography;
        public bool? Purpose1;
        public bool? Purpose3;
        public bool? Purpose4;
        public bool? Purpose7;
        public bool? GoogleVendor;
        /// <summary>Estado de App Tracking Transparency en iOS (Authorized, Denied, NotDetermined, Restricted, Unavailable).</summary>
        public string AppTrackingStatus;

        /// <summary>Atajo para las banderas de SDKs de publicidad: fuera de GDPR, o purpose 1 concedido.</summary>
        public bool AdStorageGranted => !IsGdpr || Purpose1 == true;
    }
}
