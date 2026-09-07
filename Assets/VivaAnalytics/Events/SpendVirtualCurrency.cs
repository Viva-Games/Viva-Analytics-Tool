using System.Collections.Generic;

namespace Viva.Services.Analytics
{
	public class SpendVirtualCurrency : IAnalyticsEvent
	{
#if UNITY_EDITOR
		public List<EventParameter> eventParameters = new()
		{
			new EventParameter("virtual_currency_name", "string"),
			new EventParameter("value", "int"),
			new EventParameter("item_name", "string"),
		};
#endif

		// Event Parameters Names
		public const string VIRTUAL_CURRENCY_NAME = "virtual_currency_name";
		public const string VALUE = "value";
		public const string ITEM_NAME = "item_name";

		// Event Parameters Variables
		private readonly string _virtualCurrencyName;
		private readonly int _value;
		private readonly string _itemName;

		// Constructor
		private SpendVirtualCurrency(string virtualCurrencyName, int value, string itemName)
		{
			_virtualCurrencyName = virtualCurrencyName;
			_value = value;
			_itemName = itemName;
		}

		public string GetEventKey() => "spend_virtual_currency";

		public Dictionary<string, object> GetTrackingFields() => new()
		{
			{ VIRTUAL_CURRENCY_NAME, _virtualCurrencyName },
			{ VALUE, _value },
			{ ITEM_NAME, _itemName },
		};

		public static void Track(string virtualCurrencyName, int value, string itemName) =>
			AnalyticsService.TrackEvent(new SpendVirtualCurrency(virtualCurrencyName, value, itemName));
	}
}
