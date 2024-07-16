using System.Collections.Generic;

namespace Viva.Services.Analytics
{
	public class FpsHealthCheck : IAnalyticsEvent
	{
#if UNITY_EDITOR
		public List<EventParameter> eventParameters = new()
		{
			new EventParameter("window_id", "string"),
			new EventParameter("fps_mean", "int"),
			new EventParameter("fps_min", "int"),
			new EventParameter("fps_max", "int"),
		};
#endif

		// Event Parameters Names
		public const string WINDOW_ID = "window_id";
		public const string FPS_MEAN = "fps_mean";
		public const string FPS_MIN = "fps_min";
		public const string FPS_MAX = "fps_max";

		// Event Parameters Variables
		private readonly string _windowId;
		private readonly int _fpsMean;
		private readonly int _fpsMin;
		private readonly int _fpsMax;

		// Constructor
		private FpsHealthCheck(string windowId, int fpsMean, int fpsMin, int fpsMax)
		{
			_windowId = windowId;
			_fpsMean = fpsMean;
			_fpsMin = fpsMin;
			_fpsMax = fpsMax;
		}

		public string GetEventKey() => "fps_health_check";

		public Dictionary<string, object> GetTrackingFields() => new()
		{
			{ WINDOW_ID, _windowId },
			{ FPS_MEAN, _fpsMean },
			{ FPS_MIN, _fpsMin },
			{ FPS_MAX, _fpsMax },
		};

		public static void Track(string windowId, int fpsMean, int fpsMin, int fpsMax) =>
			AnalyticsService.TrackEvent(new FpsHealthCheck(windowId, fpsMean, fpsMin, fpsMax));
	}
}
