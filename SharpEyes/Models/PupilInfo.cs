using NumSharp;
using System.Collections.Generic;
using System.Reactive;
using System.Text.RegularExpressions;
using System;
using Avalonia;

namespace Eyetracking
{
	/// <summary>
	/// A simple class that encapsulates pupil information, namely
	/// two arrays - one for pupil x,y,radius data, and one for Timestamps
	/// </summary>
	public class PupilInfo
	{

		/// <summary>
		/// Median function, since numsharp does not implement a median
		/// </summary>
		/// <param name="list"></param>
		/// <returns></returns>
		public static double Median(double[] list)
		{
			Array.Sort(list);
			int middle = list.Length / 2;
			return (list.Length % 2 != 0) ? (double)list[middle] : ((double)list[middle] + (double)list[middle - 1]) / 2;
		}

		/// <summary>
		/// Overloaded median for a list of double. Take that, python.
		/// </summary>
		/// <param name="list"></param>
		/// <returns></returns>
		public static double Median(List<double> list)
		{
			list.Sort();
			int middle = list.Count / 2;
			return (list.Count % 2 != 0) ? (double)list[middle] : ((double)list[middle] + (double)list[middle - 1]) / 2;
		}


		public NDArray Pupils { get; private set; } = null;
		public NDArray Timestamps { get; private set; } = null;
		public double FPS { get; set; } = 0;

		public PupilInfo(NDArray pupils, NDArray timestamps, double fps = 0)
		{
			this.Pupils = pupils;
			this.Timestamps = timestamps;
			this.FPS = fps;
		}

		public Tuple<int, int, int, int> GetTimestampForFrame(int frameNumber)
		{
			return new Tuple<int, int, int, int>(Timestamps[frameNumber, 0],
				Timestamps[frameNumber, 1],
				Timestamps[frameNumber, 2],
				Timestamps[frameNumber, 3]);
		}

		public Point GetMedianPupilLocation(int startFrame, int endFrame)
		{
			if (startFrame < 0 || endFrame < 0 || endFrame < startFrame)
				throw new ArgumentOutOfRangeException();

			List<double> xPositions = new List<double>();
			List<double> yPositions = new List<double>();
			for (int i = startFrame; i < endFrame; i++)
			{
				double x = Pupils[i, 0];
				double y = Pupils[i, 1];
				if (Double.IsNaN(x)) continue;
				xPositions.Add(x);
				yPositions.Add(y);
			}

			return new Point(Median(xPositions), Median(yPositions));
		}

		/// <summary>
		/// For a timestamp, gets the index of the closest frame
		/// </summary>
		/// <param name="timestamp">timestamp in HH:MM:SS.mmm format</param>
		/// <returns>index of frame</returns>
		public int TimeStampToFrameNumber(string timestamp)
		{
			if (Timestamps == null)
				throw new InvalidOperationException();

			Match match = Regex.Match(timestamp, "([0-9]{1,2}):([0-9]{1,2}):([0-9]{1,2}).([0-9]{1,3})");
			if (!match.Success)
				throw new ArgumentException();

			// Groups[0] is entire string that matched the regex
			int hour = int.Parse(match.Groups[1].Value);
			int minute = int.Parse(match.Groups[2].Value);
			int second = int.Parse(match.Groups[3].Value);
			int millisecond = int.Parse(match.Groups[4].Value);

			// iterate through all Timestamps and get the timestamp with the lowest difference
			int minIndex = 0;
			int minDiff = int.MaxValue; // difference in milliseconds from desired timestamp
										//object comparisonLock = new object();

			for (int i = 0; i < Timestamps.shape[0]; i++)
			//Parallel.For(0, Timestamps.shape[0], i =>
			{
				int diff = (hour - Timestamps[i, 0]) * 3600 * 1000 +
					(minute - Timestamps[i, 1]) * 60 * 1000 +
					(second - Timestamps[i, 2]) * 1000 +
					millisecond - Timestamps[i, 3];
				if (diff < 0) diff *= -1;
				//lock (comparisonLock)
				//{
				if (diff < minDiff)
				{
					minDiff = diff;
					minIndex = i;
				}
				//}

			}//);

			return minIndex;
		}

	}
}