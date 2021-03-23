
using System;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Eyetracking
{
	/// <summary>
	/// Exception for when the video isn't the expected 1024x768
	/// </summary>
	class VideoSizeException : Exception
	{
	}

	/// <summary>
	/// Does things with stimulus videos from the driving project.
	/// The only thing is that it finds which frame is the first TTL
	/// </summary>
	internal class DrivingVideoParser
	{
		public static readonly Mat TTLMarker = new Mat(18, 18, MatType.CV_8UC1,
			new byte[,]
			{{  000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 255, 255, 255, 255, 255, 255, 255, 255, 255, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000},
			{	000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000, 000}});

		/// <summary>
		/// Finds the time in the driving video in which the data starts
		/// </summary>
		/// <param name="videoFileName">video to look at</param>
		/// <returns>time in milliseconds from start of video</returns>
		public static async Task<int> FindStartTime(string videoFileName)
		{
			VideoCapture videoFile = new VideoCapture(videoFileName);
			return await FindStartTime(videoFile, true);
		}

		public static async Task<int> FindStartTime(VideoCapture videoFile, bool release)
		{
			if (!videoFile.IsOpened())
				throw new IOException("File could not be opened");
			if ((videoFile.FrameHeight != 768) || (videoFile.FrameWidth != 1024))
				throw new VideoSizeException();

			return await Task.Run(() =>
			{
				Mat rawFrame = new Mat();
				Mat grayFrame = new Mat();
				Mat patch = new Mat();
				Mat correlationResult = new Mat();
				bool success = videoFile.Read(rawFrame);
				double startTime = -1;
				while (success)
				{
					Cv2.CvtColor(rawFrame, grayFrame, ColorConversionCodes.RGB2GRAY);
					// binarize the patch in which the TTL indicator appears
					patch = grayFrame[745, 763, 1000, 1018].GreaterThan(196);

					Cv2.MatchTemplate(patch, TTLMarker, correlationResult, TemplateMatchModes.CCoeffNormed);
					double corrVal = correlationResult.At<float>(0, 0);
					if (corrVal > 0.9)
					{
						startTime = videoFile.PosMsec;
						break;
					}
					else
					{
						Vec3b sumPatch = grayFrame[745, 762, 1000, 1018].Sum().ToVec3b();
						if ((sumPatch.Item0 + sumPatch.Item1 + sumPatch.Item2) - 137 * 18 * 18 == 0) // the pixels are all 137
						{
							// previous frame is start
							startTime = videoFile.PosMsec - 1000.0 / videoFile.Fps;
							break;
						}
					}

					success = videoFile.Read(rawFrame);
				}

				if (release)
					videoFile.Release();

				return (int)startTime;
			});
		}
	}
}
