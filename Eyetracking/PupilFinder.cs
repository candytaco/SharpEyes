using NumSharp;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Num = NumSharp.np;

namespace Eyetracking
{
	/// <summary>
	/// Delegate for SetStatus from the main window
	/// </summary>
	/// <param name="status"></param>
	public delegate void SetStatusDelegate(string status = null);

	/// <summary>
	/// Delegate for things to do when a the pupil is found on a frame
	/// </summary>
	/// <param name="time">time as 0-1 fraction of total time</param>
	/// <param name="X">X center of pupil</param>
	/// <param name="Y">Y center of pupil</param>
	/// <param name="radius">Pupil radius</param>
	public delegate void FrameProcessedDelegate(double time, double X, double Y, double radius);

	/// <summary>
	/// Delegate for things to do when a chunk of frames is processed.
	/// FramesProcessed is called on every frame. This is called only once
	/// per click of the Find Frames button.
	/// </summary>
	public delegate void FramesProcessedDelegate();
	
	/// <summary>
	/// Delegate to be called for cancelling pupil finding
	/// </summary>
	public delegate void CancelPupilFindingDelegate();

	/// <summary>
	/// How should values be updated using manual adjustments?
	/// </summary>
	public enum ManualUpdateMode
	{
		Linear,			// fade linearly
		Exponential,	// fade exponentially
	}

	internal abstract class PupilFinder : DispatcherObject
	{
		// video information
		public string videoFileName { get; private set; }
		protected VideoCapture videoSource = null;

		public int width { get; private set; } = -1;
		public int height { get; private set; } = -1;
		public int fps { get; private set; } = -1;
		public int frameCount { get; private set; } = -1;
		public double duration { get; private set; } = -1.0;

		// parsing video stuff
		/// <summary>
		/// The frame number after calling <see cref="ReadFrame"/> or <see cref="ReadGrayscaleFrame"/>.
		/// </summary>
		private int _currentFrameNumber = -1;
		public int CurrentFrameNumber
		{
			get { return _currentFrameNumber; }
			set
			{
				_currentFrameNumber = value;
				videoSource.Set(VideoCaptureProperties.PosFrames, value - 1);
			}
		}
		public Mat cvFrame { get; protected set; } = null;
		protected Mat[] colorChannels = new Mat[3];
		protected Mat red;
		public bool isTimestampParsed { get; private set; } = false;

		// pupil finding stuff
		public int left, right, top, bottom;    // window within which to look for pupil
		public int minRadius = 6;               // min/max pupil sizes
		public int maxRadius = 24;
		public int nThreads = 1;
		/// <summary>
		/// pupilLocations is a time x 4 array in which the columns are [X, Y, Radius, Confidence.]
		/// Confidence is correlation value from the template finder and whatever that value is
		/// from Hough circle. If set to 2, then this indicates that it is a frame in which a manual
		/// adjustment was made.
		/// </summary>
		public NDArray pupilLocations { get; protected set; } = null;
		private NDArray timeStamps = null;
		protected Mat grayFrame = new Mat();
		protected Mat filteringFrame = new Mat();   // helper for filtering
		protected bool[] isFrameProcessed;			// has each frame been processed?
		public bool AreAllFramesProcessed			// has all frames been processed?
		{
			get
			{
				for (int i = 0; i < frameCount; i++)
					if (!isFrameProcessed[i]) return false;
				return true;
			}
		}

		public int bilateralBlurSize = 0;
		public int medianBlurSize = 0;
		public double bilateralSigmaColor = 0;
		public double bilateralSigmaSpace = 0;

		// UI delegates/references
		protected SetStatusDelegate SetStatus { get; private set; }
		protected System.Windows.Controls.ProgressBar progressBar;
		protected FrameProcessedDelegate UpdateFrame;
		protected FramesProcessedDelegate OnFramesProcessed;
		public CancelPupilFindingDelegate CancelPupilFinding;

		public PupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar, 
						   SetStatusDelegate setStatus, FrameProcessedDelegate updateFrame, FramesProcessedDelegate framesProcessed)
		{
			this.videoFileName = videoFileName;
			this.progressBar = progressBar;
			this.SetStatus = setStatus;
			this.UpdateFrame = updateFrame;
			this.OnFramesProcessed = framesProcessed;
			videoSource = new VideoCapture(videoFileName);
			width = (int)videoSource.Get(VideoCaptureProperties.FrameWidth);
			height = (int)videoSource.Get(VideoCaptureProperties.FrameHeight);
			fps = (int)videoSource.Get(VideoCaptureProperties.Fps);
			frameCount = (int)videoSource.Get(VideoCaptureProperties.FrameCount);
			duration = frameCount / fps;

			pupilLocations = Num.zeros((frameCount, 4), NPTypeCode.Double);
			timeStamps = Num.zeros((frameCount, 4), NPTypeCode.Int32);

			cvFrame = new Mat();
			for (int i = 0; i < 3; i++)
			{
				colorChannels[i] = new Mat();
			}

			red = colorChannels[0];

			top = 0;
			left = 0;
			right = width;
			bottom = height;

			isFrameProcessed = new bool[frameCount];
			for (int i = 0; i < frameCount; i++)
				isFrameProcessed[i] = false;
		}

		/// <summary>
		/// Find pupils in some set of frames
		/// </summary>
		/// <param name="Frames"> number of frames from current to find pupils for </param>
		public virtual void FindPupils(int Frames)
		{
			if (!isTimestampParsed)
				ParseTimeStamps();
		}


		/// <summary>
		/// Parses timestamps from the video
		/// </summary>
		public void ParseTimeStamps()
		{
			SetStatus("Parsing timestamps 0/100%");
			BackgroundWorker worker = new BackgroundWorker
			{
				WorkerReportsProgress = true
			};
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				for (int i = 0; i < frameCount; i++)
				{
					videoSource.Read(cvFrame);
					Cv2.Split(cvFrame, out colorChannels);
					timeStamps[i, 0] = Templates.MatchDigit(colorChannels[2][195, 207, 7, 15]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 15, 23]);     // hours
					timeStamps[i, 1] = Templates.MatchDigit(colorChannels[2][195, 207, 35, 43]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 43, 51]);    // minutes

					if (Templates.SecondsMarkerMatch(colorChannels[0][195, 207, 103, 111])) // check for seconds marker location
					{
						timeStamps[i, 2] = Templates.MatchDigit(colorChannels[2][195, 207, 67, 75]);            // a single seconds digit
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
				SetStatus(string.Format("Parsing timestamps {0}/100%", e.ProgressPercentage));
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				progressBar.Value = 0;
				SetStatus();
				// seek to beginning
				CurrentFrameNumber = 0;
				isTimestampParsed = true;
			};

			worker.RunWorkerAsync();

		}

		/// <summary>
		/// Read the next frame and increment the internal counter
		/// </summary>
		/// <returns></returns>
		protected bool ReadFrame()
		{
			bool success = videoSource.Read(cvFrame);
			if (!success)
			{
				return success;
			}

			_currentFrameNumber++;
			return success;
		}

		/// <summary>
		/// Reads the next frame and makes it grayscale
		/// Also does any needed filtering
		/// </summary>
		/// <returns></returns>
		protected bool ReadGrayscaleFrame()
		{
			bool success = ReadFrame();
			if (success)
			{
				Cv2.CvtColor(cvFrame, grayFrame, ColorConversionCodes.RGB2GRAY);
				FilterCurrentFrame();
			}

			return success;
		}

		protected void FilterCurrentFrame()
		{
			if (bilateralBlurSize > 0)
			{
				filteringFrame = grayFrame.BilateralFilter(bilateralBlurSize, bilateralSigmaColor, bilateralSigmaSpace);
				filteringFrame.CopyTo(grayFrame);
				filteringFrame = new Mat();
			}
			if (medianBlurSize > 1)
			{
				filteringFrame = grayFrame.MedianBlur(medianBlurSize);
				filteringFrame.CopyTo(grayFrame);
				filteringFrame = new Mat();
			}
		}

		public BitmapImage GetFrameForDisplay()
		{
			// read the current frame, but we do not increment the counter
			int currentFrameNumber = CurrentFrameNumber;
			ReadGrayscaleFrame();
			CurrentFrameNumber = currentFrameNumber;

			MemoryStream memory = new MemoryStream();
			grayFrame.ToBitmap().Save(memory, ImageFormat.Bmp);
			memory.Position = 0;
			BitmapImage image = new BitmapImage();
			image.BeginInit();
			image.StreamSource = memory;
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.EndInit();
			return image;
		}

		public void SaveTimestamps(string fileName)
		{
			Num.save(fileName, timeStamps);
		}

		public void SavePupilLocations(string fileName)
		{
			Num.save(fileName, pupilLocations);
		}

		public void LoadTimestamps(string fileName)
		{
			timeStamps = Num.load(fileName);
			isTimestampParsed = true;
		}

		public void LoadPupilLocations(string fileName)
		{
			pupilLocations = Num.load(fileName);
		}

		/// <summary>
		/// Uses manual adjusts on the found pupil locations. Can either use a linear fade or an exponential decay.
		/// If linear, then <paramref name="frameDecay"/> specifies the number of frames over which to fade the delta.
		/// If exponential, then <paramref name="frameDecay"/> is the time constant of decay, and the 2D delta will
		/// be added until it is less than 1 pixel, or until the end of the video, whichever is earlier.
		/// </summary>
		/// <param name="startFrame">frame number at which the manual update was made</param>
		/// <param name="X">new X position of pupil</param>
		/// <param name="Y">new Y position of pupil</param>
		/// <param name="radius">new radius of pupil</param>
		/// <param name="frameDecay">number of frames over which to linearly fade the difference, or time constant of exponential decay</param>
		public void ManuallyUpdatePupilLocations(int startFrame, double X, double Y, double radius, int frameDecay, ManualUpdateMode updateMode)
		{
			double dX = X - pupilLocations[startFrame, 0];
			double dY = Y - pupilLocations[startFrame, 1];
			double dR = radius - pupilLocations[startFrame, 2];
			pupilLocations[startFrame, 3] = 2;  // mark manual adjustment
			double fade;
			double dD = Math.Sqrt(dX * dX + dY * dY);
			int numFramesToUpdate = (updateMode == ManualUpdateMode.Exponential) ? frameDecay : (int)(frameDecay / Math.Log(dD));
			for (int i = 0; i < numFramesToUpdate; i++)
			{
				fade = (updateMode == ManualUpdateMode.Exponential) ? dD * Math.Exp(i / frameDecay) : (double)(frameDecay - i) / frameDecay;
				pupilLocations[i + startFrame, 0] += fade * dX;
				pupilLocations[i + startFrame, 1] += fade * dY;
				pupilLocations[i + startFrame, 2] += fade * dR;
			}
		}
	}
}
