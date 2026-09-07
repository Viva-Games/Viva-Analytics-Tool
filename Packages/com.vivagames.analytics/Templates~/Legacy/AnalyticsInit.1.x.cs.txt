using System;
using Firebase;
using UnityEngine;

namespace Viva.Services.Analytics
{
    public class AnalyticsInit : MonoBehaviour
    {
        private static bool _init = false;
        
        private FirebaseApp App;
        
        private void Awake()
        {
            if (_init)
                return;
            Init();
        }
        
        private void Start()
        {
            //Comprueba las dependencias de Google Play Services
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    // Create and hold a reference to your FirebaseApp,
                    // where app is a Firebase.FirebaseApp property of your application class.
                    App = FirebaseApp.DefaultInstance;

                    // Set a flag here to indicate whether Firebase is ready to use by your app.
                    UnityEngine.Debug.Log("Firebase ready to use.");

                    // USE THIS IF YOU HAVE CRASHLYTICS IN YOUR PROJECT
                    // When this property is set to true, Crashlytics will report all
                    // uncaught exceptions as fatal events. This is the recommended behavior.
                    // Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                }
                else
                {
                    UnityEngine.Debug.LogError(String.Format(
                        "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                    // Firebase Unity SDK is not safe to use here.
                }
            });
        }

        private void Init()
        {
            AnalyticsService.Initialize(FirebaseAnalytics.Instance);
            _init = true;
        }
    }
}