using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;

namespace SharpEyes.ViewModels
{
	public class ViewModelBase : ReactiveObject
	{
		// == window reference. needed for showing dialogs ==
		public Window? MainWindow =>
			Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
				? desktop.MainWindow
				: null;
	}
}
