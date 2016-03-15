﻿using System;
using System.Diagnostics;
using Android.App;
using Android.Content;
using Android.Net;
using Mindscape.Raygun4Net;
using SQLite.Net.Interop;
using SQLite.Net.Platform.XamarinAndroid;
using Toggl.Joey.Analytics;
using Toggl.Joey.Data;
using Toggl.Joey.Logging;
using Toggl.Joey.Net;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.Widget;
using Toggl.Phoebe;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Logging;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey
{
    [Application (
         Icon = "@drawable/Icon",
         Label = "@string/AppName",
         Description = "@string/AppDescription",
         Theme = "@style/Theme.Toggl.App")]
    [MetaData ("com.google.android.gms.version",
               Value = "@integer/google_play_services_version")]
    class AndroidApp : Application, IPlatformUtils
    {
        private bool componentsInitialized;
        private Stopwatch startTimeMeasure = Stopwatch.StartNew();

        public AndroidApp ()
        {
        }

        public AndroidApp (IntPtr javaRef, Android.Runtime.JniHandleOwnership transfer) : base (javaRef, transfer)
        {
        }

        public override void OnCreate ()
        {
            base.OnCreate ();
            RegisterComponents ();
            InitializeStartupComponents ();
        }

        private void RegisterComponents ()
        {
            // Attach bug tracker
            #if (!DEBUG)
            RaygunClient.Attach (Build.RaygunApiKey);
            #endif

            // Register platform service.
            ServiceContainer.Register<IPlatformUtils> (this);
            ServiceContainer.Register<SettingsStore> (() => new SettingsStore (Context));
            ServiceContainer.Register<ISettingsStore> (() => ServiceContainer.Resolve<SettingsStore> ());

            // Register Phoebe services.
            Services.Register ();

            // Register Joey components:
            ServiceContainer.Register<ITimeProvider> (() => new DefaultTimeProvider ());
            ServiceContainer.Register<ILogger> (() => new Logger ());
            ServiceContainer.Register<Context> (this);
            ServiceContainer.Register<ExperimentManager> (() => new ExperimentManager (
                typeof (Toggl.Phoebe.Analytics.Experiments),
                typeof (Toggl.Joey.Analytics.Experiments)));
            ServiceContainer.Register<SyncMonitor> ();
            ServiceContainer.Register<GcmRegistrationManager> ();
            ServiceContainer.Register<AndroidNotificationManager> ();
            ServiceContainer.Register<ILoggerClient> (() => new LogClient ());
            ServiceContainer.Register<ITracker> (() => new Tracker (this));
            ServiceContainer.Register<INetworkPresence> (() => new NetworkPresence (Context, (ConnectivityManager)GetSystemService (ConnectivityService)));

            // This needs some services, like ITimeProvider, so run it at the end
            Phoebe._Reactive.RxChain.Init (Phoebe._Reactive.AppState.Init ());
        }

        private void InitializeStartupComponents ()
        {
            //ServiceContainer.Resolve<UpgradeManger> ().TryUpgrade ();
            ServiceContainer.Resolve<ILoggerClient> ();
            ServiceContainer.Resolve<LoggerUserManager> ();
        }

        public void InitializeComponents ()
        {
            if (componentsInitialized) {
                return;
            }

            componentsInitialized = true;
            ServiceContainer.Resolve<SyncMonitor> ();
            ServiceContainer.Resolve<GcmRegistrationManager> ();
            ServiceContainer.Resolve<AndroidNotificationManager> ();
        }

        public void MarkLaunched()
        {
            if (!startTimeMeasure.IsRunning) {
                return;
            }

            startTimeMeasure.Stop ();
            ServiceContainer.Resolve<ITracker> ().SendAppInitTime (startTimeMeasure.Elapsed);
        }

        public override void OnTrimMemory (TrimMemory level)
        {
            base.OnTrimMemory (level);

            if (level <= TrimMemory.Moderate) {
                if (level <= TrimMemory.Complete) {
                    GC.Collect (GC.MaxGeneration);
                } else {
                    GC.Collect ();
                }
            }
        }

        public string AppIdentifier
        {
            get { return Build.AppIdentifier; }
        }

        public string AppVersion
        {
            get { return PackageManager.GetPackageInfo (PackageName, 0).VersionName; }
        }

        // Property to match with the IPlatformUtils
        // interface. This interface is implemented by iOS and Android.
        public bool IsWidgetAvailable
        {
            get { return true; }
        }

        public ISQLitePlatform SQLiteInfo
        {
            get {
                return new SQLitePlatformAndroid ();
            }
        }

        public bool ComponentsInitialized
        {
            get { return componentsInitialized; }
        }

        public void DispatchOnUIThread (Action action)
        {
            BaseActivity.CurrentActivity.RunOnUiThread (action);
        }
    }
}
