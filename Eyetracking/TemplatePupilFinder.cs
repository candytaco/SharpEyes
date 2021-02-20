using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.Serialization.Formatters.Binary;

namespace Eyetracking
{

	class TemplatePupilFinder : PupilFinder
	{
		/// <summary>
		/// Templates for a dark pupil on a light background
		/// </summary>
		public List<Mat> templates { get; private set; }
		public List<Mat> matchResults { get; private set; }
		private double bestCorrelationOnThisFrame = -1;
		public int NumTemplates { get; private set; } = 0;

		public string autoTemplatesFileName
		{
			get
			{
				if (videoFileName == null) return null;
				return Path.Combine(Path.GetDirectoryName(videoFileName),
									String.Format("{0} templates.dat", Path.GetFileNameWithoutExtension(videoFileName)));
			}
		}

		/// <summary>
		/// Because there is an option to use a part of a frame as a template,
		/// there is no good way to determine pupil size if that is the case. So we store
		/// the values from those custom templates here and apply it onwards.
		/// The nullable-ness of a List also doubles as an indicator of whether we are using
		/// custom templates. If null, we are using generated templates.
		/// </summary>
		private List<double> storedPupilSize = null;

		/// <summary>
		/// How many of the templates to actually use, since more accurate templates may be added
		/// throughout the run. If 0, use all.
		/// </summary>
		public int NumActiveTemplates = 0;

		public bool IsUsingCustomTemplates { get { return storedPupilSize != null; } }

		public TemplatePupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar, System.Windows.Shell.TaskbarItemInfo taskbar,
								   SetStatusDelegate setStatus, FrameProcessedDelegate updateFrame, FramesProcessedDelegate framesProcessed)
			: base(videoFileName, progressBar, taskbar, setStatus, updateFrame, framesProcessed)
		{
			if (File.Exists(autoTemplatesFileName))
				LoadTemplates(autoTemplatesFileName);
			else
				MakeTemplates();
		}

		public void MakeTemplates()
		{
			NumTemplates = maxRadius - minRadius + 1;
			templates = new List<Mat>(NumTemplates);
			matchResults = new List<Mat>(NumTemplates);
			for (int i = 0; i < NumTemplates; i++)
			{
				templates.Add(new Mat(maxRadius * 2 + 1, maxRadius * 2 + 1, MatType.CV_8UC1, 255));
				matchResults.Add(new Mat());
				Cv2.Circle(templates[i], maxRadius + 1, maxRadius + 1, i + minRadius, 0, -1);   // negative thickness == filled
			}
			storedPupilSize = null;
		}

		/// <summary>
		/// Adds a segment of the image as a template
		/// </summary>
		/// <param name="top"></param>
		/// <param name="bottom"></param>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <param name="radius"></param>
		public void AddImageSegmentAsTemplate(int top, int bottom, int left, int right, double radius)
		{
			if (storedPupilSize == null)
			{
				templates.Clear();
				matchResults.Clear();
				NumTemplates = 0;
				storedPupilSize = new List<double>();
			}
			if (filteredFrame.Width < 1)
			{
				_currentFrameNumber--;
				ReadGrayscaleFrame();
			}
			templates.Add(new Mat(bottom - top, right - left, MatType.CV_8UC1));
			storedPupilSize.Add(radius);
			matchResults.Add(new Mat());
			filteredFrame[top, bottom, left, right].CopyTo(templates[NumTemplates++]);
		}

		/// <summary>
		/// Gets a bitmap of the template for display
		/// </summary>
		/// <param name="index">index of the template to display. if outside of bounds, will return first template</param>
		/// <returns>The first template, if using automated templates, or the template image picked from the video</returns>
		public BitmapImage GetTemplateImage(int index = 0)
		{
			if (index < 0) index = 0;
			if (index >= NumTemplates) index = NumTemplates - 1;
			MemoryStream memory = new MemoryStream();
			templates[index].ToBitmap().Save(memory, ImageFormat.Bmp);
			memory.Position = 0;
			BitmapImage image = new BitmapImage();
			image.BeginInit();
			image.StreamSource = memory;
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.EndInit();
			return image;
		}

		/// <summary>
		/// Removes custom template
		/// </summary>
		/// <param name="index"></param>
		public void RemoveTemplate(int index)
		{
			if (storedPupilSize == null || index < 0 || index >= NumTemplates) return;

			storedPupilSize.RemoveAt(index);
			templates.RemoveAt(index);
			matchResults.RemoveAt(index);
			NumTemplates--;
		}

		public override void FindPupils(int Frames)
		{
			base.FindPupils(Frames);
			DateTime start = DateTime.Now;
			SetStatus("Finding pupils 0%");
			BackgroundWorker worker = new BackgroundWorker
			{
				WorkerReportsProgress = true,
				WorkerSupportsCancellation = true
			};

			int framesProcessed = 0;
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				// TODO: perhaps parallelize over frames rather than templates
				object templateLock = new object();
				for (int f = 0; f < Frames; f++)
				{
					ReadGrayscaleFrame();
					if (NumTemplates > 1)
					{
						int startIndex = NumActiveTemplates == 0 ? 0 : templates.Count - NumActiveTemplates;
						if (startIndex < 0) startIndex = 0;

						Parallel.For(startIndex, templates.Count, i =>
						{
							Cv2.MatchTemplate(filteredFrame[top, bottom, left, right], templates[i], matchResults[i], TemplateMatchModes.CCoeffNormed);
							double minVal, maxVal;
							Point minLocation, maxLocation;
							matchResults[i].MinMaxLoc(out minVal, out maxVal, out minLocation, out maxLocation);
							lock (templateLock)
							{
								if (maxVal > bestCorrelationOnThisFrame)
								{
									if (storedPupilSize == null)	// case auto-generated templates
									{
										pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + maxRadius;
										pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + maxRadius;
										pupilLocations[CurrentFrameNumber, 2] = i + minRadius;
									}
									else	// custom templates that may have different sizes. I was going to use ternary ops because slick but it would make three of the same comparisons
									{
										pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + templates[i].Width / 2;
										pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + templates[i].Height / 2;
										pupilLocations[CurrentFrameNumber, 2] = storedPupilSize[i];
									}
									bestCorrelationOnThisFrame = maxVal;
								}
							}
						});
						pupilLocations[CurrentFrameNumber, 3] = bestCorrelationOnThisFrame;
						bestCorrelationOnThisFrame = -1;
					}
					else
					{
						Cv2.MatchTemplate(grayFrame[top, bottom, left, right], templates[0], matchResults[0], TemplateMatchModes.CCoeffNormed);
						double minVal, maxVal;
						Point minLocation, maxLocation;
						matchResults[0].MinMaxLoc(out minVal, out maxVal, out minLocation, out maxLocation);
						pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + templates[0].Width / 2;
						pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + templates[0].Height / 2;
						pupilLocations[CurrentFrameNumber, 2] = storedPupilSize[0];
						pupilLocations[CurrentFrameNumber, 3] = maxVal;
					}

					isFrameProcessed[CurrentFrameNumber] = true;
					isAnyFrameProcessed = true;
					framesProcessed++;

					this.Dispatcher.Invoke(() =>
					{
						UpdateFrame();
					});
					((BackgroundWorker)sender).ReportProgress((f + 1) * 100 / Frames);
					if (worker.CancellationPending)
					{
						args.Cancel = true;
						break;
					}
				}
			};

			worker.ProgressChanged += delegate (object sender, ProgressChangedEventArgs e)
			{
				SetStatus(string.Format("Finding pupils in {0} frames {1}%", Frames, e.ProgressPercentage));
				taskbar.ProgressValue = (double)e.ProgressPercentage / 100;
				progressBar.Value = e.ProgressPercentage;
			};

			worker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
			{
				TimeSpan elapsed = DateTime.Now - start;
				progressBar.Value = 0;
				SetStatus(string.Format("Idle.{3} {0} frames processed in {1:c} ({2} fps)", framesProcessed, elapsed, (int)(Frames / elapsed.TotalSeconds), 
																							e.Cancelled ? "Pupil finding cancelled." : ""));
				this.Dispatcher.Invoke(OnFramesPupilsProcessed);
				taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
				CancelPupilFinding -= worker.CancelAsync;
			};

			CancelPupilFinding += worker.CancelAsync;
			taskbar.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
			worker.RunWorkerAsync();
		}

		public void SaveTemplates(string fileName = null)
		{
			if (IsUsingCustomTemplates)
			{
				fileName = fileName ?? this.autoTemplatesFileName;
				using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
				using (ZipArchive dataFile = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
				{
					BinaryFormatter formatter = new BinaryFormatter();
					ZipArchiveEntry pupilLocationEntry = dataFile.CreateEntry("storeedPupilSizes.list");
					using (Stream stream = pupilLocationEntry.Open())
						formatter.Serialize(stream, storedPupilSize);
					for (int i = 0; i < NumTemplates; i++)
					{
						ZipArchiveEntry templateEntry = dataFile.CreateEntry(string.Format("template{0}.png", i));
						using (Stream stream = templateEntry.Open())
							templates[i].WriteToStream(stream);
					}
				}
			}
		}

		public void LoadTemplates(string fileName)
		{
			using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
			using (ZipArchive dataFile = new ZipArchive(fileStream, ZipArchiveMode.Read, true))
			{
				BinaryFormatter formatter = new BinaryFormatter();
				ZipArchiveEntry pupilLocationEntry = dataFile.GetEntry("storeedPupilSizes.list");
				using (Stream stream = pupilLocationEntry.Open())
					storedPupilSize = (List<double>)formatter.Deserialize(stream);
				NumTemplates = storedPupilSize.Count;
				templates = new List<Mat>(NumTemplates);
				matchResults = new List<Mat>(NumTemplates);
				for (int i = 0; i < NumTemplates; i++)
				{
					ZipArchiveEntry templateEntry = dataFile.GetEntry(string.Format("template{0}.png", i));
					using (Stream stream = templateEntry.Open())
					{
						MemoryStream decompressed = new MemoryStream();
						stream.CopyTo(decompressed);
						decompressed.Position = 0;
						templates.Add(Mat.FromStream(decompressed, ImreadModes.Grayscale));
					}
					matchResults.Add(new Mat());
				}
			}
		}
	}
}
