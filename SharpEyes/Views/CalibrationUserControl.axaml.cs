using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SharpEyes.ViewModels;

namespace SharpEyes.Views
{
	public partial class CalibrationUserControl : UserControl
	{
		private CalibrationViewModel viewModel => (CalibrationViewModel)this.DataContext;

		public CalibrationUserControl()
		{
			InitializeComponent();
		}

		private void ForceRedrawButton_OnClick(object? sender, RoutedEventArgs e)
		{
			DisplayCanvas.Children.Clear();
			DisplayCanvas.Children.Add(new Rectangle()
			{
				Width = viewModel.StimulusWidth,
				Height = viewModel.StimulusHeight,
				StrokeThickness = 4,
				Stroke = new SolidColorBrush(Colors.DodgerBlue)
			});
			int i = 1;
			foreach (Point p in viewModel.CalibrationPoints)
			{
				DisplayCanvas.Children.Add(new Rectangle()
				{
					Width = 12,
					Height = 12,
					Fill = new SolidColorBrush(Colors.LimeGreen),
					RenderTransform = new TranslateTransform(p.X - 6, p.Y - 6),
				});
				DisplayCanvas.Children.Add(new TextBlock()
				{
					Text = i.ToString(),
					RenderTransform = new TranslateTransform(p.X - 16, p.Y - 16),
				});
				i++;
			}
		}
	}
}
