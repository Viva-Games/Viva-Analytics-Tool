namespace Viva.Services.Analytics.Consent
{
    /// <summary>
    /// Estado de consentimiento leído del TCF (el CMP, por ejemplo el de AppLovin MAX).
    /// Cada purpose es nullable: null = sin dato TCF en disco (p. ej. usuario fuera del EEE).
    /// IsGdpr indica si aplica el RGPD.
    /// </summary>
    public readonly struct TcfConsent
    {
        public readonly bool IsGdpr;
        public readonly bool? Purpose1;      // Almacenar/acceder info en el dispositivo
        public readonly bool? Purpose3;      // Crear perfiles para publicidad personalizada
        public readonly bool? Purpose4;      // Usar perfiles para publicidad personalizada
        public readonly bool? Purpose7;      // Medir rendimiento de anuncios
        public readonly bool? GoogleVendor;  // Consentimiento de Google (IAB vendor 755)

        /// <summary>Identificador de Google en la Global Vendor List del IAB.</summary>
        public const int GoogleVendorId = 755;

        public TcfConsent(bool isGdpr, bool? purpose1, bool? purpose3, bool? purpose4,
            bool? purpose7, bool? googleVendor)
        {
            IsGdpr = isGdpr;
            Purpose1 = purpose1;
            Purpose3 = purpose3;
            Purpose4 = purpose4;
            Purpose7 = purpose7;
            GoogleVendor = googleVendor;
        }
    }
}
