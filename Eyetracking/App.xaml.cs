using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Sentry;

namespace Eyetracking
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
	    public App()
	    {
		    SentrySdk.Init("https://4aa216608a894bd99da3daa7424c995d@o553633.ingest.sentry.io/5689896");
			DispatcherUnhandledException += App_DispatcherUnhandledException;
	    }

	    void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	    {
		    SentrySdk.CaptureException(e.Exception);
	    }
    }
}
