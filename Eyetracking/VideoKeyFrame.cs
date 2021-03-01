using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyetracking
{
	/// <summary>
	/// Struct for representing information about keyframes when editing gaze locations
	/// </summary>
	public class VideoKeyFrame
	{
		/// <summary>
		/// Video time in milliseconds
		/// </summary>
		public readonly double VideoTime;

		/// <summary>
		/// Timestamp representation of video time
		/// </summary>
		public readonly string VideoTimeStamp;

		/// <summary>
		/// Index in the gaze data
		/// </summary>
		public readonly int DataIndex;

		/// <summary>
		/// X of gaze in screen space
		/// </summary>
		public double GazeX;

		/// <summary>
		/// Y of gaze in screen space
		/// </summary>
		public double GazeY;

		public VideoKeyFrame(double time, int index, string timestamp, double gazeX, double gazeY)
		{
			VideoTime = time;
			VideoTimeStamp = timestamp;
			DataIndex = index;
			GazeX = gazeX;
			GazeY = gazeY;
		}

		public static bool operator <(VideoKeyFrame lhs, double time)
		{
			return lhs.VideoTime < time;
		}

		public static bool operator >(VideoKeyFrame lhs, double time)
		{
			return lhs.VideoTime > time;
		}
	}
}
