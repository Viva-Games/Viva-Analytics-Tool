using System.Collections.Generic;

namespace Viva.Services.Analytics
{
	public class AdImpression : IAnalyticsEvent
	{
#if UNITY_EDITOR
		public List<EventParameter> eventParameters = new()
		{
			new EventParameter("ad_name", "string"),
			new EventParameter("ad_platform", "string"),
			new EventParameter("ad_source", "string"),
			new EventParameter("ad_unit_name", "string"),
			new EventParameter("ad_format", "string"),
			new EventParameter("value", "double"),
			new EventParameter("currency", "string"),
		};
#endif

		// Event Parameters Names
		public const string AD_NAME = "ad_name";
		public const string AD_PLATFORM = "ad_platform";
		public const string AD_SOURCE = "ad_source";
		public const string AD_UNIT_NAME = "ad_unit_name";
		public const string AD_FORMAT = "ad_format";
		public const string VALUE = "value";
		public const string CURRENCY = "currency";

		// Event Parameters Variables
		private readonly string _adName;
		private readonly string _adPlatform;
		private readonly string _adSource;
		private readonly string _adUnitName;
		private readonly string _adFormat;
		private readonly double _value;
		private readonly string _currency;

		// Constructor
		private AdImpression(string adName, string adPlatform, string adSource, string adUnitName, string adFormat, double value, string currency)
		{
			_adName = adName;
			_adPlatform = adPlatform;
			_adSource = adSource;
			_adUnitName = adUnitName;
			_adFormat = adFormat;
			_value = value;
			_currency = currency;
		}

		public string GetEventKey() => "ad_impression";

		public Dictionary<string, object> GetTrackingFields() => new()
		{
			{ AD_NAME, _adName },
			{ AD_PLATFORM, _adPlatform },
			{ AD_SOURCE, _adSource },
			{ AD_UNIT_NAME, _adUnitName },
			{ AD_FORMAT, _adFormat },
			{ VALUE, _value },
			{ CURRENCY, _currency },
		};

		public static void Track(string adName, string adPlatform, string adSource, string adUnitName, string adFormat, double value, string currency) =>
			AnalyticsService.TrackEvent(new AdImpression(adName, adPlatform, adSource, adUnitName, adFormat, value, currency));
	}
}
