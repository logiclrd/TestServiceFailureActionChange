using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TestServiceFailureActionChangeSharp.WindowsAPI
{
	class NativeMethods
	{
		[DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool ChangeServiceConfig2W([MarshalAs(UnmanagedType.I4)] int hService, [MarshalAs(UnmanagedType.I4)] int dwInfoLevel, /**/ref SERVICE_FAILURE_ACTIONSW/*/IntPtr/**/ lpInfo);
		[DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern bool ChangeServiceConfig2W(IntPtr hService, ServiceConfigType dwInfoLevel, ref SERVICE_FAILURE_ACTIONS_FLAG lpInfo);
	}
}
