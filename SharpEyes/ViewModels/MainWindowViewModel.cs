using System;
using System.Collections.Generic;
using System.Text;

namespace SharpEyes.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public PupilFindingUserControlViewModel pupilFindingUserControlViewModel { get; }
		public StimulusGazeViewModel stimulusGazeViewModel { get; }

		public MainWindowViewModel()
		{
			pupilFindingUserControlViewModel = new PupilFindingUserControlViewModel();
			stimulusGazeViewModel = new StimulusGazeViewModel();
		}
	}
}
