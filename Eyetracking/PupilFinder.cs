using System.IO;
using System.ComponentModel;
using OpenCvSharp;

using NumSharp;
using Num = NumSharp.np;
using System.Windows;

namespace Eyetracking
{
	internal abstract class PupilFinder
	{
		public string videoFileName { get; private set; }
		protected VideoCapture videoSource = null;

		public int width { get; private set; } = -1;
		public int height { get; private set; } = -1;
		public int fps { get; private set; } = -1;
		public int frameCount { get; private set; } = -1;
		public double duration { get; private set; } = -1.0;

		public int currentFrameNumber { get; private set; } = 0;

		public Mat currentFrameMatrix { get; private set; } = null;

		public NDArray pupilLocations { get; private set; } = null;

		private NDArray timeStamps = null;

		private NDArray frame = null;

		private Mat cvFrame = null;
		private Mat[] colorChannels = new Mat[3];
		private Mat red;

		protected static Mat[] DIGIT_TEMPLATES = new Mat[10];

		public PupilFinder(string videoFileName)
		{
			this.videoFileName = videoFileName;
			videoSource = new VideoCapture(videoFileName);
			width = (int)videoSource.Get(VideoCaptureProperties.FrameWidth);
			height = (int)videoSource.Get(VideoCaptureProperties.FrameHeight);
			fps = (int)videoSource.Get(VideoCaptureProperties.Fps);
			frameCount = (int)videoSource.Get(VideoCaptureProperties.FrameCount);
			duration = frameCount / fps;

			pupilLocations = Num.zeros((frameCount, 3), NPTypeCode.Double);
			timeStamps = Num.zeros((frameCount, 4), NPTypeCode.Int32);

			frame = Num.zeros((height, width));
			cvFrame = new Mat();
			for (int i = 0; i < 3; i++)
				colorChannels[i] = new Mat();
			red = colorChannels[0];
		}

		/// <summary>
		/// Find pupils in some set of frames
		/// </summary>
		/// <param name="Frames"> number of frames from current to find pupils for </param>
		abstract public void FindPupils(int Frames);

		public delegate void SetStatus(string status = null);

		public void ParseTimeStamps(System.Windows.Controls.ProgressBar progressBar, SetStatus setStatus)
		{
			setStatus("Parsing time stamps");
			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerReportsProgress = true;
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				for (int i = 0; i < frameCount; i++)
				{
					videoSource.Read(cvFrame);
					Cv2.Split(cvFrame, out colorChannels);
					timeStamps[i, 0] = Templates.MatchDigit(colorChannels[2][195, 207, 7, 15]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 15, 23]);		// hours
					timeStamps[i, 1] = Templates.MatchDigit(colorChannels[2][195, 207, 35, 43]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 43, 51]);	// minutes

					if (Templates.SecondsMarkerMatch(colorChannels[0][195, 207, 103, 111]))	// check for seconds marker location
					{
						timeStamps[i, 2] = Templates.MatchDigit(colorChannels[2][195, 207, 67, 75]);			// a single seconds digit
						timeStamps[i, 3] = Templates.MatchDigit(colorChannels[2][195, 207, 79, 87]) * 100 +
										   Templates.MatchDigit(colorChannels[2][195, 207, 87, 95]) * 10 +
										   Templates.MatchDigit(colorChannels[2][195, 207, 95, 103]);
					}
					else
					{
						timeStamps[i, 2] = Templates.MatchDigit(colorChannels[2][195, 207, 67, 75]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 75, 83]);
						timeStamps[i, 3] = Templates.MatchDigit(colorChannels[2][195, 207, 87, 95]) * 100 +
										   Templates.MatchDigit(colorChannels[2][195, 207, 95, 103]) * 10 +
										   Templates.MatchDigit(colorChannels[2][195, 207, 103, 111]);
					}
					colorChannels[0].Dispose();
					colorChannels[1].Dispose();
					colorChannels[2].Dispose();

					((BackgroundWorker)sender).ReportProgress((i + 1) * 100 / frameCount);
				}
			};

			worker.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
			{
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				progressBar.Value = 0;
				setStatus();
			};

			worker.RunWorkerAsync();
		}
	}
}
