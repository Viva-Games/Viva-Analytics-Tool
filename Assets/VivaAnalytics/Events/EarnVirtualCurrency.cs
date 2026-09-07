using System.Collections.Generic;

namespace Viva.Services.Analytics
{
	public class EarnVirtualCurrency : IAnalyticsEvent
	{
#if UNITY_EDITOR
		public List<EventParameter> eventParameters = new()
		{
			new EventParameter("virtual_currency_name", "string"),
			new EventParameter("value", "int"),
			new EventParameter("source_id", "string"),
		};
#endif

		// Event Parameters Names
		public const string VIRTUAL_CURRENCY_NAME = "virtual_currency_name";
		public const string VALUE = "value";
		public const string SOURCE_ID = "source_id";

		// Event Parameters Variables
		private readonly string _virtualCurrencyName;
		private readonly int _value;
		private readonly string _sourceId;

		// Constructor
		private EarnVirtualCurrency(string virtualCurrencyName, int value, string sourceId)
		{
			_virtualCurrencyName = virtualCurrencyName;
			_value = value;
			_sourceId = sourceId;
		}

		public string GetEventKey() => "earn_virtual_currency";

		public Dictionary<string, object> GetTrackingFields() => new()
		{
			{ VIRTUAL_CURRENCY_NAME, _virtualCurrencyName },
			{ VALUE, _value },
			{ SOURCE_ID, _sourceId },
		};

		public static void Track(string virtualCurrencyName, int value, string sourceId) =>
			AnalyticsService.TrackEvent(new EarnVirtualCurrency(virtualCurrencyName, value, sourceId));
	}
}
