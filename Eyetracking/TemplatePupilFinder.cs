﻿using System;
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
using System.Windows;
using Point = OpenCvSharp.Point;

namespace Eyetracking
{

	class TemplatePupilFinder : PupilFinder
	{
		/// <summary>
		/// Templates for a dark pupil on a light background
		/// </summary>
		public List<Template> templates { get; private set; }
		public List<Mat> matchResults { get; private set; }
		private double bestCorrelationOnThisFrame = -1;
		public int NumTemplates { get; private set; } = 0;

		/// <summary>
		/// Templates for rejecting matches
		/// </summary>
		public List<Template> antiTemplates { get; private set; }
		public List<Mat> antiResults { get; private set; }
		public int NumAntiTemplates => antiTemplates.Count;

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
		/// How many of the templates to actually use, since more accurate templates may be added
		/// throughout the run. If 0, use all.
		/// </summary>
		public int NumActiveTemplates = 0;

		/// <summary>
		/// Are we using custom templates?
		/// </summary>
		public bool IsUsingCustomTemplates { get; private set; }

		/// <summary>
		/// Used for averaging across multiple matches. Is a n x 4 array, in which n is the
		/// number of best matches to average. The columns are [x, y, radius, confidence].
		/// The final value will be a weighted average of these top matches.
		/// </summary>
		private double[,] topMatches = null;

		public TemplateMatchModes TemplateMatchMode = TemplateMatchModes.CCoeffNormed;

		public int NumMatches
		{
			get => topMatches?.Length / 4 ?? 1;	// .length on a 2D array is num elements
			set
			{
				topMatches = value > 1 ? new double[value, 4] : null;
			}
		}

		public TemplatePupilFinder(string videoFileName, System.Windows.Controls.ProgressBar progressBar, System.Windows.Shell.TaskbarItemInfo taskbar,
								   SetStatusDelegate setStatus, FrameProcessedDelegate updateFrame, FramesProcessedDelegate framesProcessed)
			: base(videoFileName, progressBar, taskbar, setStatus, updateFrame, framesProcessed)
		{
			if (File.Exists(autoTemplatesFileName))
				LoadTemplates(autoTemplatesFileName);
			else
				MakeTemplates();

			antiTemplates = new List<Template>();
			antiResults = new List<Mat>();
		}

		public void MakeTemplates()
		{
			NumTemplates = maxRadius - minRadius + 1;
			templates = new List<Template>(NumTemplates);
			matchResults = new List<Mat>(NumTemplates);
			for (int i = 0; i < NumTemplates; i++)
			{
				Mat template = new Mat(maxRadius * 2 + 1, maxRadius * 2 + 1, MatType.CV_8UC1, 255);
				Cv2.Circle(template, maxRadius + 1, maxRadius + 1, i + minRadius, 0, -1);   // negative thickness == filled
				templates.Add(new Template(template, minRadius + i));
				matchResults.Add(new Mat());
				
			}
			IsUsingCustomTemplates = false;
		}

		/// <summary>
		/// Adds a segment of the image as a template. Will crop template to image bounds if needed.
		/// </summary>
		/// <param name="top">desired top index of image segment</param>
		/// <param name="bottom">desired bottom index of image segment</param>
		/// <param name="left">desired left index of image segment</param>
		/// <param name="right">desired right index of image segment</param>
		/// <param name="radius">radius</param>
		public void AddImageSegmentAsTemplate(int top, int bottom, int left, int right, double radius)
		{
			if (!IsUsingCustomTemplates)
			{
				templates.Clear();
				matchResults.Clear();
				NumTemplates = 0;
				IsUsingCustomTemplates = true;
			}
			if (filteredFrame.Width < 1)
			{
				_currentFrameNumber--;
				ReadGrayscaleFrame();
			}
			// TODO: check edge of image and set center accordingly

			int deltaTop = top < 0 ? -1 * top : 0;
			int deltaBottom = bottom > filteredFrame.Height ? bottom - filteredFrame.Height : 0;
			int deltaLeft = left < 0 ? -1 * left : 0;
			int deltaRight = right > filteredFrame.Width ? right - filteredFrame.Width : 0;

			if ((deltaTop > 0 && deltaBottom > 0) || (deltaLeft > 0 && deltaRight > 0))
				throw new ArgumentOutOfRangeException();	// only one of each pair should be out side of the image...

			Mat template = new Mat(bottom - top - deltaTop - deltaBottom, right - left - deltaLeft - deltaRight, MatType.CV_8UC1);
			filteredFrame[top + deltaTop, bottom - deltaBottom, left + deltaLeft, right - deltaRight].CopyTo(template);
			templates.Add(new Template(template, radius, (double)template.Width / 2 - deltaLeft + deltaRight, (double)template.Height / 2 - deltaTop + deltaBottom));
			matchResults.Add(new Mat());
			
		}

		/// <summary>
		/// Adds a segment of the image to use as a rejection template
		/// </summary>
		/// <param name="top"></param>
		/// <param name="bottom"></param>
		/// <param name="left"></param>
		/// <param name="right"></param>
		public void AddImageSegmentAsAntiTemplate(int top, int bottom, int left, int right)
		{
			if (filteredFrame.Width < 1)
			{
				_currentFrameNumber--;
				ReadGrayscaleFrame();
			}
			Mat template = new Mat(bottom - top, right - left, MatType.CV_8UC1);
			filteredFrame[top, bottom, left, right].CopyTo(template);
			antiTemplates.Add(new Template(template, null));
			antiResults.Add(new Mat());
		}

		/// <summary>
		/// Gets a bitmap of the template for display
		/// </summary>
		/// <param name="index">index of the template to display. if outside of bounds, will return first template</param>
		/// <returns>The first template, if using automated templates, or the template image picked from the video</returns>
		public BitmapImage GetTemplateImage(int index = 0)
		{
			return GetTemplateFromList(index, templates);
		}

		public BitmapImage GetAntiTemplateImage(int index = 0)
		{
			return GetTemplateFromList(index, antiTemplates);
		}

		private BitmapImage GetTemplateFromList(int index, List<Template> templatesList)
		{
			if (templatesList.Count < 1) return null;

			if (index < 0) index = 0;
			if (index >= templatesList.Count) index = templatesList.Count - 1;
			MemoryStream memory = new MemoryStream();
			templatesList[index].Image.ToBitmap().Save(memory, ImageFormat.Bmp);
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
			if (!IsUsingCustomTemplates || index < 0 || index >= NumTemplates) return;

			templates.RemoveAt(index);
			matchResults.RemoveAt(index);
			NumTemplates--;
		}

		public void RemoveAntiTemplate(int index)
		{
			if (index < 0 || index >= NumAntiTemplates) return;

			antiTemplates.RemoveAt(index);
			antiResults.RemoveAt(index);
		}

		public override void FindPupils(int Frames, double threshold = 0, int thresholdFrames = 0)
		{
			base.FindPupils(Frames);
			DateTime start = DateTime.Now;
			SetStatus("Finding pupils 0%");
			BackgroundWorker worker = new BackgroundWorker
			{
				WorkerReportsProgress = true,
				WorkerSupportsCancellation = true
			};
			
			double cumulativeConfidence;
			int frames;

			int framesProcessed = 0;	// number of frames actually processed
			bool stepBack = false;		// do we need to step back because the confidence fell?
			worker.DoWork += delegate (object sender, DoWorkEventArgs args)
			{
				// TODO: perhaps parallelize over frames rather than templates
				object templateLock = new object();
				int f = 0;
				for (; f < Frames; f++)
				{
					ReadGrayscaleFrame();
					if (NumTemplates > 1)
					{
						// match negative templates
						Parallel.For(0, NumAntiTemplates, i =>
						{
							Cv2.MatchTemplate(filteredFrame[top, bottom, left, right], antiTemplates[i].Image,
								antiResults[i], TemplateMatchMode);
						});


						int startIndex = NumActiveTemplates == 0 ? 0 : templates.Count - NumActiveTemplates;
						if (startIndex < 0) startIndex = 0;

						// match positive templates
						Parallel.For(startIndex, templates.Count, index =>
						{
							int i = (int)index;	// because Parallel.For uses a long, and that cannot be implicitly cast to int to index into lists

							Cv2.MatchTemplate(filteredFrame[top, bottom, left, right], templates[i].Image, matchResults[i],
								TemplateMatchMode);
							matchResults[i].MinMaxLoc(out double minVal, out double maxVal, out Point minLocation,
								out Point maxLocation);

							// square difference uses minimum value as best
							switch (TemplateMatchMode)
							{
								case TemplateMatchModes.SqDiff:
									maxVal = filteredFrame.Width * filteredFrame.Height * 255 * 255 - minVal;
									maxLocation = minLocation;
									break;
								case TemplateMatchModes.SqDiffNormed:
									maxVal = 1 - minVal;
									maxLocation = minLocation;
									break;
								default:
									break;
							}

							// if there are negative templates, subtract the value of the best anti-match
							// at this best match
							double maxAntiMatch = 0;
							double thisAntiValue, antiValue;
							int x, y;	// indexes into the antimatch results, which may be of difference sizes because template
							for (int j = 0; j < NumAntiTemplates; j++)
							{
								x = maxLocation.X + (templates[i].Width - antiTemplates[j].Width);
								y = maxLocation.Y + (templates[i].Height - antiTemplates[j].Height);
								lock (antiResults[j])
								{
									antiValue = antiResults[j].At<double>(y, x);
								}

								switch (TemplateMatchMode)
								{
									case TemplateMatchModes.SqDiff:
										thisAntiValue = filteredFrame.Width * filteredFrame.Height * 255 * 255 -
										                antiValue;
										break;
									case TemplateMatchModes.SqDiffNormed:
										thisAntiValue = 1 - antiValue;
										break;
									default:
										thisAntiValue = antiValue;
										break;
								}

								if (thisAntiValue > maxAntiMatch)
									maxAntiMatch = thisAntiValue;
							}
							maxVal -= maxAntiMatch;

							lock (templateLock)
							{
								if (NumMatches == 1) // only need highest match and so write directly
								{
									if (maxVal > bestCorrelationOnThisFrame)
									{
										if (!IsUsingCustomTemplates) // case auto-generated templates
										{
											pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + maxRadius;
											pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + maxRadius;
											pupilLocations[CurrentFrameNumber, 2] = i + minRadius;
										}
										else // custom templates that may have different sizes. I was going to use ternary ops because slick but it would make three of the same comparisons
										{
											pupilLocations[CurrentFrameNumber, 0] =
												maxLocation.X + left + templates[i].X;
											pupilLocations[CurrentFrameNumber, 1] =
												maxLocation.Y + top + templates[i].Y;
											pupilLocations[CurrentFrameNumber, 2] = templates[i].Radius.Value;
										}

										bestCorrelationOnThisFrame = maxVal;
									}
								}
								else // have to store values to intermediate and then do weighted average
								{
									for (int j = 0; j < NumMatches; j++)
									{
										// look over existing stored matches, and if this is better than any
										// immediately overwrite that one
										if (maxVal > topMatches[j, 3])
										{
											if (!IsUsingCustomTemplates) // case auto-generated templates
											{
												topMatches[j, 0] = maxLocation.X + left + maxRadius;
												topMatches[j, 1] = maxLocation.Y + top + maxRadius;
												topMatches[j, 2] = i + minRadius;
											}
											else // custom templates that may have different sizes. I was going to use ternary ops because slick but it would make three of the same comparisons
											{
												topMatches[j, 0] =
													maxLocation.X + left + templates[i].X;
												topMatches[j, 1] =
													maxLocation.Y + top + templates[i].Y;
												topMatches[j, 2] = templates[i].Radius.Value;
											}

											topMatches[j, 3] = maxVal;
											break;
										}
									}
								}
							}
						});

						if (NumMatches > 1) // calculate weighted average
						{
							pupilLocations[CurrentFrameNumber, 0] =
								pupilLocations[CurrentFrameNumber, 1] =
									pupilLocations[CurrentFrameNumber, 2] = 0;
							double sum = 0;
							for (int j = 0; j < NumMatches; j++)
							{
								// we still only store single hishest value as the confidence value
								if (topMatches[j, 3] > bestCorrelationOnThisFrame)
									bestCorrelationOnThisFrame = topMatches[j, 3];
								sum += topMatches[j, 3];
								pupilLocations[CurrentFrameNumber, 0] += topMatches[j, 0] * topMatches[j, 3];
								pupilLocations[CurrentFrameNumber, 1] += topMatches[j, 1] * topMatches[j, 3];
								pupilLocations[CurrentFrameNumber, 2] += topMatches[j, 2] * topMatches[j, 3];
							}

							pupilLocations[CurrentFrameNumber, 0] /= sum;
							pupilLocations[CurrentFrameNumber, 1] /= sum;
							pupilLocations[CurrentFrameNumber, 2] /= sum;

							for (int j = 0; j < NumMatches; j++)
							for (int k = 0; k < 4; k++)
								topMatches[j, k] = -1;
						}

						pupilLocations[CurrentFrameNumber, 3] = bestCorrelationOnThisFrame;
						bestCorrelationOnThisFrame = -1;
					}
					else
					{
						Cv2.MatchTemplate(grayFrame[top, bottom, left, right], templates[0].Image, matchResults[0],
							TemplateMatchModes.CCoeffNormed);
						matchResults[0].MinMaxLoc(out double minVal, out double maxVal, out Point minLocation,
							out Point maxLocation);
						pupilLocations[CurrentFrameNumber, 0] = maxLocation.X + left + templates[0].X;
						pupilLocations[CurrentFrameNumber, 1] = maxLocation.Y + top + templates[0].Y;
						pupilLocations[CurrentFrameNumber, 2] = templates[0].Radius.Value;
						pupilLocations[CurrentFrameNumber, 3] = maxVal;
					}

					isFrameProcessed[CurrentFrameNumber] = true;
					isAnyFrameProcessed = true;
					framesProcessed++;

					this.Dispatcher.Invoke(() => { UpdateFrame(); });
					((BackgroundWorker) sender).ReportProgress((f + 1) * 100 / Frames);
					if (worker.CancellationPending)
					{
						args.Cancel = true;
						break;
					}

					// stop if average confidence for the past n frames drops below threshold
					cumulativeConfidence = 0;
					if (f >= thresholdFrames)
					{
						for (int i = 0; i < thresholdFrames; i++)
							cumulativeConfidence += pupilLocations[CurrentFrameNumber - i, 3];
						if (cumulativeConfidence < threshold * thresholdFrames)
						{
							stepBack = true;
							break;
						}
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
				string additionalMessage = "";
				if (stepBack)
					additionalMessage = "Confidence fell below threshold";
				else if (e.Cancelled)
					additionalMessage = "Pupil finding cancelled";
				SetStatus(string.Format("Idle.{3} {0} frames processed in {1:c} ({2} fps)", framesProcessed, elapsed, (int)(framesProcessed / elapsed.TotalSeconds), 
																							additionalMessage));
				this.Dispatcher.Invoke(OnFramesPupilsProcessed, false, null, stepBack);
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

					// because opencvsharp objects are not directly serializable, we break the template objects apart
					List<double> pupilSizes = new List<double>();
					List<Tuple<double, double>> centers = new List<Tuple<double, double>>();
					for (int i = 0; i < templates.Count; i++)
					{
						pupilSizes.Add(templates[i].Radius.Value);
						centers.Add(new Tuple<double, double>(templates[i].X, templates[i].Y));
					}

					ZipArchiveEntry pupilRadiusEntry = dataFile.CreateEntry("pupil radii.list");
					using (Stream stream = pupilRadiusEntry.Open())
						formatter.Serialize(stream, pupilSizes);

					ZipArchiveEntry pupilCentersEntry = dataFile.CreateEntry("pupil centers.list");
					using (Stream stream = pupilCentersEntry.Open())
						formatter.Serialize(stream, centers);

					for (int i = 0; i < NumTemplates; i++)
					{
						ZipArchiveEntry templateEntry = dataFile.CreateEntry(string.Format("template {0}.png", i));
						using (Stream stream = templateEntry.Open())
							templates[i].Image.WriteToStream(stream);
					}
					for (int i = 0; i < NumAntiTemplates; i++)
					{
						ZipArchiveEntry templateEntry = dataFile.CreateEntry(string.Format("anti-template {0}.png", i));
						using (Stream stream = templateEntry.Open())
							antiTemplates[i].Image.WriteToStream(stream);
					}
				}
			}
		}

		public void LoadTemplates(string fileName)
		{
			try
			{
				using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
				using (ZipArchive dataFile = new ZipArchive(fileStream, ZipArchiveMode.Read, true))
				{
					ZipArchiveEntry radiiEntry = dataFile.GetEntry("pupil radii.list");
					if (radiiEntry != null)
					{
						templates = new List<Template>();
						antiTemplates = new List<Template>();

						List<double> pupilSizes;
						List<Tuple<double, double>> centers = new List<Tuple<double, double>>();

						BinaryFormatter formatter = new BinaryFormatter();
						using (Stream stream = radiiEntry.Open())
							pupilSizes = (List<double>) formatter.Deserialize(stream);

						ZipArchiveEntry centersEntry = dataFile.GetEntry("pupil centers.list");
						using (Stream stream = centersEntry.Open())
							centers = (List<Tuple<double, double>>) formatter.Deserialize(stream);

						NumTemplates = pupilSizes.Count;

						for (int i = 0; i < NumTemplates; i++)
						{
							ZipArchiveEntry templateEntry = dataFile.GetEntry(string.Format("template {0}.png", i));
							using (Stream stream = templateEntry.Open())
							{
								MemoryStream decompressed = new MemoryStream();
								stream.CopyTo(decompressed);
								decompressed.Position = 0;
								templates.Add(new Template(Mat.FromStream(decompressed, ImreadModes.Grayscale), pupilSizes[i], centers[i].Item1, centers[i].Item2));
							}
						}

						// remaining files, if any, are antitemplates
						int numAntiTemplates = dataFile.Entries.Count - NumTemplates - 2;
						antiTemplates = new List<Template>(numAntiTemplates);
						for (int i = 0; i < numAntiTemplates; i++)
						{
							ZipArchiveEntry templateEntry = dataFile.GetEntry(string.Format("anti-template {0}.png", i));
							using (Stream stream = templateEntry.Open())
							{
								MemoryStream decompressed = new MemoryStream();
								stream.CopyTo(decompressed);
								decompressed.Position = 0;
								antiTemplates.Add(new Template(Mat.FromStream(decompressed, ImreadModes.Grayscale), null));
							}
						}
					}
					else LoadTemplatesLegacy(dataFile); // legacy format
				}

				IsUsingCustomTemplates = true;

				matchResults = new List<Mat>(NumTemplates);
				for (int i = 0; i < NumTemplates; i++)
					matchResults.Add(new Mat());

				antiResults = new List<Mat>(antiTemplates.Count);
				for (int i = 0; i < antiTemplates.Count; i++)
					antiResults.Add(new Mat());
			}
			catch (InvalidDataException)
			{
				MessageBox.Show("Corrupt templates data file");
			}
		}

		/// <summary>
		/// Loads template data stored in the format prior to switching to Template objects.
		/// To be called by <see cref="LoadTemplates(string)"/> when it determines that it's a legacy save file
		/// </summary>
		/// <param name="dataFile">already opened ziparchive</param>
		private void LoadTemplatesLegacy(ZipArchive dataFile)
		{
			BinaryFormatter formatter = new BinaryFormatter();
			ZipArchiveEntry pupilLocationEntry = dataFile.GetEntry("storeedPupilSizes.list");

			List<double> storedPupilSize;
			using (Stream stream = pupilLocationEntry.Open())
				storedPupilSize = (List<double>)formatter.Deserialize(stream);
			NumTemplates = storedPupilSize.Count;
			templates = new List<Template>(NumTemplates);
			for (int i = 0; i < NumTemplates; i++)
			{
				ZipArchiveEntry templateEntry = dataFile.GetEntry(string.Format("template{0}.png", i));
				using (Stream stream = templateEntry.Open())
				{
					MemoryStream decompressed = new MemoryStream();
					stream.CopyTo(decompressed);
					decompressed.Position = 0;
					templates.Add(new Template(Mat.FromStream(decompressed, ImreadModes.Grayscale), storedPupilSize[i]));
				}
			}

			// remaining files, if any, are antitemplates
			int numAntiTemplates = dataFile.Entries.Count - NumTemplates - 1;
			antiTemplates = new List<Template>(numAntiTemplates);
			for (int i = 0; i < numAntiTemplates; i++)
			{
				ZipArchiveEntry templateEntry = dataFile.GetEntry(string.Format("anti-template{0}.png", i));
				using (Stream stream = templateEntry.Open())
				{
					MemoryStream decompressed = new MemoryStream();
					stream.CopyTo(decompressed);
					decompressed.Position = 0;
					antiTemplates.Add(new Template(Mat.FromStream(decompressed, ImreadModes.Grayscale), null));
				}
			}
			
		}
	}
}
