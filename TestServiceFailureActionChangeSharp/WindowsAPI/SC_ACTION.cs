using System.Runtime.InteropServices;

namespace TestServiceFailureActionChangeSharp.WindowsAPI
{
	[StructLayout(LayoutKind.Sequential)]
	struct SC_ACTION
	{
		public SC_ACTION_TYPE Type;
		public int Delay;
	}
}
