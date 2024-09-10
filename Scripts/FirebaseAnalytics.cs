using System.Collections.Generic;
using Firebase.Analytics;

namespace Viva.Services.Analytics
{
    public class FirebaseAnalytics : AAnalyticsTracker
    {
        // Insert here all the keys for the common parameters. You can also put them directly in the InsertCommonParameters method.
        // EXAMPLE:
        // public const string PARAM_PLAYER_LEVEL = "player_level";

        public static FirebaseAnalytics Instance { get; private set; } = new();

        private bool _isInitialized;

        private FirebaseAnalytics(){}

        public override void TrackEvent(IAnalyticsEvent eventToTrack)
        {
            string eventKey = eventToTrack.GetEventKey();
            List<string> stringParams = new();
            Parameter[] parameters = BuildParameters(eventToTrack.GetTrackingFields(), stringParams);
            UnityEngine.Debug.Log($"ANALYTICS: {eventKey} ({string.Join(", ", stringParams)})");
            global::Firebase.Analytics.FirebaseAnalytics.LogEvent(eventKey, parameters);
        }

        protected internal override void Initialize()
        {
            _isInitialized = true;
        }

        protected internal override bool IsInitialized() => _isInitialized;

        private Parameter[] BuildParameters(Dictionary<string, object> eventParams, List<string> stringParams)
        {
            List<Parameter> parameters = new();

            InsertCommonParameters(parameters, stringParams);
            InsertEventParameters(eventParams, parameters, stringParams);

            return parameters.ToArray();
        }

        /// <summary>
        /// Insert here all the common parameters that are going to be sent with every event.
        /// </summary>
        /// <param name="parameters"> The event parameters </param>
        /// <param name="stringParams"> Event parameters in string format </param>
        private void InsertCommonParameters(List<Parameter> parameters, List<string> stringParams)
        {
            // Insert here all the common parameters that are going to be sent with every event.
            // The value should be retrieved from the game data or player prefs.
            // EXAMPLE:
            // parameters.Add(new Parameter(PARAM_PLAYER_LEVEL, PlayerPrefs.GetInt("level_game")));
            // stringParams.Add($"{PARAM_PLAYER_LEVEL} = {PlayerPrefs.GetInt("level_game")}");
        }

        private void InsertEventParameters(Dictionary<string, object> eventParams, List<Parameter> parameters,
            List<string> stringParams)
        {
            foreach (var param in eventParams)
            {
                if (param.Value is long longValue)
                {
                    parameters.Add(new Parameter(param.Key, longValue));
                    stringParams.Add($"{param.Key} = {longValue}");
                }
                else if (param.Value is int intValue)
                {
                    parameters.Add(new Parameter(param.Key, intValue));
                    stringParams.Add($"{param.Key} = {intValue}");
                }
                else if (param.Value is float floatValue)
                {
                    parameters.Add(new Parameter(param.Key, floatValue));
                    stringParams.Add($"{param.Key} = {floatValue}");
                }
                else if (param.Value is double doubleValue)
                {
                    parameters.Add(new Parameter(param.Key, doubleValue));
                    stringParams.Add($"{param.Key} = {doubleValue}");
                }
                else if (param.Value is bool boolValue)
                {
                    int boolParamValue = boolValue ? 1 : 0;
                    parameters.Add(new Parameter(param.Key, boolParamValue));
                    stringParams.Add($"{param.Key} = {boolParamValue}");
                }
                else if (param.Value is float stringValue)
                {
                    parameters.Add(new Parameter(param.Key, stringValue));
                    stringParams.Add($"{param.Key} = {stringValue}");
                }
                else
                {
                    parameters.Add(new Parameter(param.Key, param.Value.ToString()));
                    stringParams.Add($"{param.Key} = {param.Value}");
                }
            }
        }
    }
}