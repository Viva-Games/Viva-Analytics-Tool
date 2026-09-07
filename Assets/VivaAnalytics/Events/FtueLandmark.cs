using System.Collections.Generic;

namespace Viva.Services.Analytics
{
	public class FtueLandmark : IAnalyticsEvent
	{
#if UNITY_EDITOR
		public List<EventParameter> eventParameters = new()
		{
			new EventParameter("ftue_step", "int"),
			new EventParameter("ftue_description", "string"),
		};
#endif

		// Event Parameters Names
		public const string FTUE_STEP = "ftue_step";
		public const string FTUE_DESCRIPTION = "ftue_description";

		// Event Parameters Variables
		private readonly int _ftueStep;
		private readonly string _ftueDescription;

		// Constructor
		private FtueLandmark(int ftueStep, string ftueDescription)
		{
			_ftueStep = ftueStep;
			_ftueDescription = ftueDescription;
		}

		public string GetEventKey() => "ftue_landmark";

		public Dictionary<string, object> GetTrackingFields() => new()
		{
			{ FTUE_STEP, _ftueStep },
			{ FTUE_DESCRIPTION, _ftueDescription },
		};

		public static void Track(int ftueStep, string ftueDescription) =>
			AnalyticsService.TrackEvent(new FtueLandmark(ftueStep, ftueDescription));
	}
}
