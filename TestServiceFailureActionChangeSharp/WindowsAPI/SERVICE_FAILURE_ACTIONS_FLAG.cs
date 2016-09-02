using System.Runtime.InteropServices;

namespace TestServiceFailureActionChangeSharp.WindowsAPI
{
	[StructLayout(LayoutKind.Sequential)]
	struct SERVICE_FAILURE_ACTIONS_FLAG
	{
		public bool fFailureActionsOnNonCrashFailures;
	}
}
