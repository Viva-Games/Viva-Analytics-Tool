using System.Collections.Generic;

namespace Viva.Services.Analytics.Consent
{
    /// <summary>
    /// Traduce el estado TCF a las 4 señales de Google Consent Mode.
    /// Fuera del EEE: todo concedido. Dentro del EEE: null se trata como denegado.
    /// El resultado se pasa a <see cref="AnalyticsService.SetConsent"/>.
    /// </summary>
    public static class ConsentModeMapper
    {
        public static Dictionary<ConsentSignal, bool> Map(TcfConsent c)
        {
            if (!c.IsGdpr)
            {
                return new Dictionary<ConsentSignal, bool>
                {
                    { ConsentSignal.AnalyticsStorage, true },
                    { ConsentSignal.AdStorage, true },
                    { ConsentSignal.AdUserData, true },
                    { ConsentSignal.AdPersonalization, true },
                };
            }

            bool p1 = c.Purpose1 == true;
            bool p3 = c.Purpose3 == true;
            bool p4 = c.Purpose4 == true;
            bool p7 = c.Purpose7 == true;
            bool gv = c.GoogleVendor == true;

            return new Dictionary<ConsentSignal, bool>
            {
                { ConsentSignal.AnalyticsStorage, p1 },
                { ConsentSignal.AdStorage, p1 },
                { ConsentSignal.AdUserData, p1 && p7 && gv },
                { ConsentSignal.AdPersonalization, p3 && p4 && gv },
            };
        }
    }
}
