using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using DynamicData.Kernel;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using Sentry;

namespace SharpEyes.Views
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			//ExtendClientAreaToDecorationsHint = true;
			ExtendClientAreaTitleBarHeightHint = -1;

			TransparencyLevelHint = WindowTransparencyLevel.AcrylicBlur;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				CheckLinuxLibraries();
		}

		public void GlobalExceptionHandler(Exception e)
		{
			IMsBoxWindow<MessageBox.Avalonia.Enums.ButtonResult> messageBox =
				MessageBoxManager.GetMessageBoxStandardWindow("Internal error",
					"SharpEyes has encountered an internal error and is exiting.\nAn error report will be sent",
					ButtonEnum.Ok, MessageBox.Avalonia.Enums.Icon.Error);
			SentrySdk.CaptureException(e);
			messageBox.ShowDialog(this).Wait(2000);
		}

		private void Window_OnClosing(object? sender, CancelEventArgs e)
		{
			PupilFindingUserControl.OnClosing();
		}

		public void CheckLinuxLibraries()
		{
			string[] libraries =
			{
				"libtesseract.so.4",
				"libgtk-x11-2.0.so.0",
				"libgdk-x11-2.0.so.0",
				"libcairo.so.2",
				"libgdk_pixbuf-2.0.so.0",
				"libgobject-2.0.so.0",
				"libglib-2.0.so.0",
				"libdc1394.so.22",
				"libavcodec.so.57",
				"libavformat.so.57",
				"libavutil.so.55",
				"libswscale.so.4",
				"libjpeg.so.8",
				"libpng16.so.16",
				"libtiff.so.5",
				"libIlmImf-2_2.so.22",
				"libz.so.1",
				"libdl.so.2",
				"libpthread.so.0",
				"librt.so.1",
				"libstdc++.so.6",
				"libm.so.6",
				"libgcc_s.so.1",
				"libc.so.6",
				"libgmodule-2.0.so.0",
				"libpangocairo-1.0.so.0",
				"libX11.so.6",
				"libXfixes.so.3",
				"libatk-1.0.so.0",
				"libgio-2.0.so.0",
				"libpangoft2-1.0.so.0",
				"libpango-1.0.so.0",
				"libfontconfig.so.1",
				"libXrender.so.1",
				"libXinerama.so.1",
				"libXi.so.6",
				"libXrandr.so.2",
				"libXcursor.so.1",
				"libXcomposite.so.1",
				"libXdamage.so.1",
				"libXext.so.6",
				"libpixman-1.so.0",
				"libfreetype.so.6",
				"libxcb-shm.so.0",
				"libxcb.so.1",
				"libxcb-render.so.0",
				"libffi.so.6",
				"libpcre.so.3",
				"libraw1394.so.11",
				"libusb-1.0.so.0",
				"libswresample.so.2",
				"libwebp.so.6",
				"libcrystalhd.so.3",
				"libva.so.2",
				"libzvbi.so.0",
				"libxvidcore.so.4",
				"libx265.so.146",
				"libx264.so.152",
				"libwebpmux.so.3",
				"libwavpack.so.1",
				"libvpx.so.5",
				"libvorbisenc.so.2",
				"libvorbis.so.0",
				"libtwolame.so.0",
				"libtheoraenc.so.1",
				"libtheoradec.so.1",
				"libspeex.so.1",
				"libsnappy.so.1",
				"libshine.so.3",
				"librsvg-2.so.2",
				"libopus.so.0",
				"libopenjp2.so.7",
				"libmp3lame.so.0",
				"libgsm.so.1",
				"liblzma.so.5",
				"libssh-gcrypt.so.4",
				"libopenmpt.so.0",
				"libbluray.so.2",
				"libgnutls.so.30",
				"libxml2.so.2",
				"libgme.so.0",
				"libchromaprint.so.1",
				"libbz2.so.1.0",
				"libdrm.so.2",
				"libvdpau.so.1",
				"libva-x11.so.2",
				"libva-drm.so.2",
				"libjbig.so.0",
				"libHalf.so.12",
				"libIex-2_2.so.12",
				"libIlmThread-2_2.so.12",
				"libselinux.so.1",
				"libresolv.so.2",
				"libmount.so.1",
				"libharfbuzz.so.0",
				"libthai.so.0",
				"libexpat.so.1",
				"libXau.so.6",
				"libXdmcp.so.6",
				"libudev.so.1",
				"libsoxr.so.0",
				"libnuma.so.1",
				"libogg.so.0",
				"libcroco-0.6.so.3",
				"libgcrypt.so.20",
				"libgssapi_krb5.so.2",
				"libmpg123.so.0",
				"libvorbisfile.so.3",
				"libp11-kit.so.0",
				"libidn2.so.0",
				"libunistring.so.2",
				"libtasn1.so.6",
				"libnettle.so.6",
				"libhogweed.so.4",
				"libgmp.so.10",
				"libicuuc.so.60",
				"libblkid.so.1",
				"libgraphite2.so.3",
				"libdatrie.so.1",
				"libbsd.so.0",
				"libgomp.so.1",
				"libgpg-error.so.0",
				"libkrb5.so.3",
				"libk5crypto.so.3",
				"libcom_err.so.2",
				"libkrb5support.so.0",
				"libicudata.so.60",
				"libuuid.so.1",
				"libkeyutils.so.1"
			};
			List<string> missing = new List<string>();
			Process ldconfig = new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					FileName = "/sbin/ldconfig",
					Arguments = "-p",
					UseShellExecute = false,
					RedirectStandardOutput = true,
				}
			};
			ldconfig.Start();
			string all = ldconfig.StandardOutput.ReadToEnd();
			foreach (string library in libraries)
			{
				if (!all.Contains(library))
					missing.Add(library);
			}

			if (missing.Count > 0)
			{
				mainGrid.Children.Clear();
				StackPanel panel = new StackPanel();
				panel.Children.Add(new TextBlock(){Text = "The following libraries are missing and need to be installed:"});
				foreach (string library in missing)
					panel.Children.Add(new TextBlock(){Text = library});
				mainGrid.Children.Add(panel);
			}
		}
	}
}
