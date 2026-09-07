namespace Viva.Services.Analytics.Consent
{
    /// <summary>
    /// Las 4 señales de Google Consent Mode que Firebase entiende. Enum propio para
    /// mantener el mapeo desacoplado del SDK de Firebase (testeable sin dependencias).
    /// </summary>
    public enum ConsentSignal
    {
        AnalyticsStorage,
        AdStorage,
        AdUserData,
        AdPersonalization
    }
}
