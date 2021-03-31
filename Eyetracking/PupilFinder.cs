using NumSharp;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
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
	public delegate void FrameProcessedDelegate();

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
		Linear,         // fade linearly
		Exponential,    // fade exponentially
	}

	internal abstract class PupilFinder : DispatcherObject
	{
		// video information
		public string videoFileName { get; private set; }
		public string autoTimestampFileName
		{
			get
			{
				if (videoFileName == null) return null;
				return Path.Combine(Path.GetDirectoryName(videoFileName),
									String.Format("{0} timestamps.npy", Path.GetFileNameWithoutExtension(videoFileName)));
			}
		}
		public string autoPupilsFileName
		{
			get
			{
				if (videoFileName == null) return null;
				return Path.Combine(Path.GetDirectoryName(videoFileName),
									String.Format("{0} pupils.npy", Path.GetFileNameWithoutExtension(videoFileName)));
			}
		}
		protected VideoCapture videoSource = null;

		public int width { get; private set; } = -1;
		public int height { get; private set; } = -1;
		public int fps { get; private set; } = -1;
		public int frameCount { get; private set; } = -1;
		public double duration { get; private set; } = -1.0;

		// parsing video stuff		
		protected int _currentFrameNumber = -1;
		/// <summary>
		/// The current frame number that has been read in. When set, will read that frame!
		/// Use <see cref="Seek"/> if we do not want to go the frame reading.
		/// </summary>
		public int CurrentFrameNumber
		{
			get { return _currentFrameNumber; }
			set
			{
				int desired = value;
				if (desired < 0)
				{
					desired = 0;
				}
				else if (desired > frameCount - 1)
				{
					desired = frameCount - 1;
				}

				_currentFrameNumber = desired - 1;
				videoSource.Set(VideoCaptureProperties.PosFrames, desired);
				ReadGrayscaleFrame();
			}
		}
		// NOTE: IT LOOKS LIKE VideoCaptureProperties.PosFrames is 1-INDEXED!!
		// or rather, like my previews implementation of CurrentFrameNumber, it is the 0-indexed frame number
		// of the upcoming frame.
		public double OpenCVFramePosition { get { return videoSource.Get(VideoCaptureProperties.PosFrames); } }
		public Mat cvFrame { get; protected set; } = null;
		protected Mat[] colorChannels = new Mat[3];
		protected Mat red;
		public bool isTimestampParsed { get; private set; } = false;
		/// <summary>
		/// Used for converting a Cv2 Mat to a displayable bitmap
		/// </summary>
		private MemoryStream BMPConvertMemeory = new MemoryStream();
		/// <summary>
		/// Lazy flag in case for some reason we need to get the same frame twice
		/// </summary>
		private bool isCVFrameConverted = false;
		private bool isBitmapFrameGrayscale = false;
		private BitmapImage bitmapFrame = null;

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
		protected Mat filteredFrame = new Mat();	// processed frame in which to find pupils
		protected bool[] isFrameProcessed;          // has each frame been processed?
		public bool AreAllFramesProcessed           // has all frames been processed?
		{
			get
			{
				for (int i = 0; i < frameCount; i++)
				{
					if (!isFrameProcessed[i])
					{
						return false;
					}
				}

				return true;
			}
		}
		public bool isAnyFrameProcessed { get; protected set; } = false;

		public int bilateralBlurSize = 0;
		public int medianBlurSize = 0;
		public double bilateralSigmaColor = 0;
		public double bilateralSigmaSpace = 0;

		// UI delegates/references
		public SetStatusDelegate SetStatus { get; private set; }
		protected System.Windows.Controls.ProgressBar progressBar;
		public FrameProcessedDelegate UpdateFrame;
		public FramesProcessedDelegate OnFramesPupilsProcessed; // delegate for when pupils are found in a chunk of frames
		public FramesProcessedDelegate OnTimeStampsFound;		// delegate for when timestamps are found
		public CancelPupilFindingDelegate CancelPupilFinding;

		/// <summary>
		/// For taskbar progress bars
		/// </summary>
		protected System.Windows.Shell.TaskbarItemInfo taskbar;

		public PupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar, System.Windows.Shell.TaskbarItemInfo taskbar,
						   SetStatusDelegate setStatus, FrameProcessedDelegate updateFrame, FramesProcessedDelegate framesProcessed)
		{
			this.videoFileName = videoFileName;
			this.progressBar = progressBar;
			SetStatus = setStatus;
			UpdateFrame = updateFrame;
			OnFramesPupilsProcessed = framesProcessed;
			this.taskbar = taskbar;
			videoSource = new VideoCapture(videoFileName);
			width = (int)videoSource.Get(VideoCaptureProperties.FrameWidth);
			height = (int)videoSource.Get(VideoCaptureProperties.FrameHeight);
			fps = (int)videoSource.Get(VideoCaptureProperties.Fps);
			frameCount = (int)videoSource.Get(VideoCaptureProperties.FrameCount);
			duration = frameCount / fps;

			// try to auto load stuff if they exist
			if (File.Exists(autoTimestampFileName))
			{
				LoadTimestamps(autoTimestampFileName);
				isTimestampParsed = true;
			}
			else
				timeStamps = Num.zeros((frameCount, 4), NPTypeCode.Int32);

			if (File.Exists(autoPupilsFileName))
				LoadPupilLocations(autoPupilsFileName);
			else
			{
				pupilLocations = Num.zeros((frameCount, 4), NPTypeCode.Double);
				pupilLocations *= Num.NaN;	// use NaN to indicate pupil not yet found on this frame
			}

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
			{
				isFrameProcessed[i] = false;
			}
		}

		/// <summary>
		/// Find pupils in some set of frames. Must be overridden in child classes.
		/// </summary>
		/// <param name="Frames"> number of frames from current to find pupils for </param>
		/// <param name="threshold"> confidence threshold at which to auto-pause pupil finding</param>
		public virtual void FindPupils(int Frames, double threshold = 0)
		{
			if (!isTimestampParsed)
			{
				ParseTimeStamps();
			}
		}


		/// <summary>
		/// Parses timestamps from the video
		/// </summary>
		public void ParseTimeStamps()
		{
			SetStatus("Parsing timestamps 0%");
			DateTime start = DateTime.Now;
			BackgroundWorker worker = new BackgroundWorker
			{
				WorkerReportsProgress = true
			};
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				for (int i = 0; i < frameCount; i++)
				{
					if (!videoSource.Read(cvFrame)) break ;
					Cv2.Split(cvFrame, out colorChannels);
					timeStamps[i, 0] = Templates.MatchDigit(colorChannels[2][195, 207, 7, 15]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 15, 23]);     // hours
					timeStamps[i, 1] = Templates.MatchDigit(colorChannels[2][195, 207, 35, 43]) * 10 + Templates.MatchDigit(colorChannels[2][195, 207, 43, 51]);    // minutes

					if (Templates.SecondsMarkerMatch(colorChannels[2][195, 207, 103, 111])) // check for seconds marker location
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
				SetStatus(string.Format("Parsing timestamps {0}%", e.ProgressPercentage));
				taskbar.ProgressValue = e.ProgressPercentage / 100.0;
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				progressBar.Value = 0;
				TimeSpan elapsed = DateTime.Now - start;
				SetStatus(string.Format("Idle. {0} frames processed in {1:c} ({2} fps)", frameCount, elapsed, (int)(frameCount / elapsed.TotalSeconds)));
				// seek to beginning
				CurrentFrameNumber = 0;
				taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
				isTimestampParsed = true;
				OnTimeStampsFound();
			};

			taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
			worker.RunWorkerAsync();

		}

		/// <summary>
		/// Read the next frame and increment the internal counter
		/// </summary>
		/// <returns></returns>
		public bool ReadFrame()
		{
			bool success = videoSource.Read(cvFrame);
			if (!success)
			{
				return success;
			}

			_currentFrameNumber++;
			isCVFrameConverted = false;
			return success;
		}

		/// <summary>
		/// Reads the next frame and makes it grayscale
		/// Also does any needed filtering
		/// </summary>
		/// <returns></returns>
		public bool ReadGrayscaleFrame()
		{
			bool success = ReadFrame();
			if (success)
			{
				Cv2.CvtColor(cvFrame, grayFrame, ColorConversionCodes.RGB2GRAY);
				FilterCurrentFrame();
			}

			return success;
		}

		public void FilterCurrentFrame()
		{
			grayFrame.CopyTo(filteredFrame);
			if (bilateralBlurSize > 0)
			{
				filteringFrame = filteredFrame.BilateralFilter(bilateralBlurSize, bilateralSigmaColor, bilateralSigmaSpace);
				filteringFrame.CopyTo(filteredFrame);
				filteringFrame = new Mat();
			}
			if (medianBlurSize > 1)
			{
				filteringFrame = filteredFrame.MedianBlur(medianBlurSize);
				filteringFrame.CopyTo(filteredFrame);
				filteringFrame = new Mat();
			}
			isCVFrameConverted = false;
		}

		/// <summary>
		/// Gets the current frame that has been read in for display
		/// </summary>
		/// <param name="filtered">get the filtered frame instead of the RGB frame.</param>
		/// <returns></returns>
		public BitmapImage GetFrameForDisplay(bool filtered)
		{
			if (cvFrame == null)
			{
				return null;
			}

			if ((filtered == isBitmapFrameGrayscale) && isCVFrameConverted)
			{
				return bitmapFrame;
			}

			isBitmapFrameGrayscale = filtered;
			isCVFrameConverted = true;
			if (filtered)
			{
				filteredFrame.ToBitmap().Save(BMPConvertMemeory, ImageFormat.Bmp);
			}
			else
			{
				cvFrame.ToBitmap().Save(BMPConvertMemeory, ImageFormat.Bmp);
			}
			
			BMPConvertMemeory.Position = 0;
			bitmapFrame = new BitmapImage();
			bitmapFrame.BeginInit();
			bitmapFrame.StreamSource = BMPConvertMemeory;
			bitmapFrame.CacheOption = BitmapCacheOption.OnLoad;
			bitmapFrame.EndInit();
			BMPConvertMemeory.SetLength(0);
			return bitmapFrame;
		}

		/// <summary>
		/// Seek to a frame such that when <see cref="ReadFrame"/> or <see cref="ReadGrayscaleFrame"/> is called,
		/// this frame is read in.
		/// </summary>
		/// <param name="frame">frame to go to</param>
		public void Seek(int frame)
		{
			if (frame < 0)
			{
				frame = 0;
			}
			else if (frame > frameCount - 1)
			{
				frame = frameCount - 1;
			}

			_currentFrameNumber = frame - 1;
			videoSource.Set(VideoCaptureProperties.PosFrames, frame);
		}

		public void SaveTimestamps(string fileName = null)
		{
			fileName = fileName ?? this.autoTimestampFileName;
			Num.save(fileName, timeStamps);
		}

		public void SavePupilLocations(string fileName = null)
		{
			fileName = fileName ?? this.autoPupilsFileName;
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
			isAnyFrameProcessed = true;
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
				if (i + startFrame >= frameCount)
					break;
				if (pupilLocations[i + startFrame, 0] == Num.NaN)
					break;	// don't update auto values if they don't exist
				fade = (updateMode == ManualUpdateMode.Exponential) ? dD * Math.Exp(i / frameDecay) : (double)(frameDecay - i) / frameDecay;
				pupilLocations[i + startFrame, 0] += fade * dX;
				pupilLocations[i + startFrame, 1] += fade * dY;
				pupilLocations[i + startFrame, 2] += fade * dR;
			}
		}

		/// <summary>
		/// Gets a green-red representation of which frames has had FindPupils run over them
		/// </summary>
		/// <param name="width">width of image to get</param>
		/// <param name="height">height of image to get</param>
		/// <returns></returns>
		public BitmapImage GetFramesProcessedPreviewImage(int width = 1920, int height = 6)
		{
			if (pupilLocations == null) return null;
			Mat representation = new Mat(height, frameCount, MatType.CV_8UC3);
			MatIndexer<Vec3b> indexer = representation.GetGenericIndexer<Vec3b>();
			for (int i = 0; i < frameCount; i++)
			{
				for (int j = 0; j < height; j++)
					representation.Set(j, i, Double.IsNaN(pupilLocations[i, 0]) ? Scalar.DeepPink.ToVec3b() : Scalar.LimeGreen.ToVec3b());
					//indexer[i, j] = value > 0 ? Scalar.LimeGreen.ToVec3b() : Scalar.DeepPink.ToVec3b();
			}
			representation.Resize(new Size(width, height), 0, 0, InterpolationFlags.Nearest).ToBitmap().Save(BMPConvertMemeory, ImageFormat.Bmp);
			BMPConvertMemeory.Position = 0;
			bitmapFrame = new BitmapImage();
			bitmapFrame.BeginInit();
			bitmapFrame.StreamSource = BMPConvertMemeory;
			bitmapFrame.CacheOption = BitmapCacheOption.OnLoad;
			bitmapFrame.EndInit();
			BMPConvertMemeory.SetLength(0);
			return bitmapFrame;
		}

		public void ResetPupilLocations()
		{
			pupilLocations = Num.zeros((frameCount, 4), NPTypeCode.Double);
			pupilLocations *= Num.NaN;    // use -1 to indicate pupil not yet found on this frame
		}
	}
}
