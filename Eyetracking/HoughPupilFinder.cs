using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyetracking
{
	internal class HoughPupilFinder : PupilFinder
	{
		public HoughPupilFinder(string videoFileName)
			: base(videoFileName)
		{
			
		}

		public override void FindPupils(int Frames)
		{
			throw new NotImplementedException();
		}
	}
}
