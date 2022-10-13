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
	public struct VideoKeyFrame
	{
		/// <summary>
		/// Video time in frames
		/// </summary>
		public int VideoFrame { get; }

		/// <summary>
		/// Timestamp representation of video time
		/// </summary>
		public string VideoTimeStamp { get; }

		/// <summary>
		/// Index in the gaze data
		/// </summary>
		public int DataIndex { get; }

		/// <summary>
		/// X of gaze in screen space
		/// </summary>
		public double GazeX { get; }

		/// <summary>
		/// Y of gaze in screen space
		/// </summary>
		public double GazeY { get; }

		public VideoKeyFrame(int frame, int index, string timestamp, double gazeX, double gazeY)
		{
			VideoFrame = frame;
			VideoTimeStamp = timestamp;
			DataIndex = index;
			GazeX = gazeX;
			GazeY = gazeY;
		}

		public static bool operator <(VideoKeyFrame lhs, double time)
		{
			return lhs.VideoFrame < time;
		}

		public static bool operator >= (VideoKeyFrame lhs, double time)
		{
			return !(lhs.VideoFrame < time);
		}

		public static bool operator >(VideoKeyFrame lhs, double time)
		{
			return lhs.VideoFrame > time;
		}

		public static bool operator <=(VideoKeyFrame lhs, double time)
		{
			return !(lhs.VideoFrame > time);
		}

		public static bool operator ==(VideoKeyFrame lhs, double time)
		{
			return (lhs.VideoFrame - time) < Double.Epsilon;
		}

		public static bool operator !=(VideoKeyFrame lhs, double time)
		{
			return !(lhs == time);
		}
	}
}
