using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Eyetracking
{
	public delegate void AcceptParametersDelegate(object sender, ParametersAcceptedEventArgs e);

	public class ParametersAcceptedEventArgs : EventArgs
	{

	}

	/// <summary>
	/// Interaction logic for CalibrationParametersWindow.xaml
	/// </summary>
	public partial class CalibrationParametersWindow : Window
	{
		public CalibrationParameters calibrationParameters { get; private set; }

		public CalibrationParametersWindow(CalibrationParameters parameters)
		{
			calibrationParameters = parameters;
			// TODO: Create data bindings between calibration parameters object and UI
			InitializeComponent();
		}
	}
}
