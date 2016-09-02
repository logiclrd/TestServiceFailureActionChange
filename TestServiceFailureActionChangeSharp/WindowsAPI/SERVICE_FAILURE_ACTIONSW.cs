using System;
using System.Runtime.InteropServices;

namespace TestServiceFailureActionChangeSharp.WindowsAPI
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	struct SERVICE_FAILURE_ACTIONSW
	{
		public int dwResetPeriod;
		public string lpRebootMsg;
		public string lpCommand;
		public int cActions;
		public IntPtr lpsaActionsPtr;

		// This serializes as extra data, past the end of the actual SERVICE_FAILURE_ACTIONSW structure. Calling code should
		// use the Lock() method to temporarily allocate a region of memory for lpsaActionsPtr to point at and marshal the
		// contents of lpsaActions into it.
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
		public SC_ACTION[] lpsaActions;

		class DataLock : IDisposable
		{
			SERVICE_FAILURE_ACTIONSW _data;
			SC_ACTION[] _originalActionsArray;
			IntPtr _buffer;

			public DataLock(ref SERVICE_FAILURE_ACTIONSW data)
			{
				_data = data;

				int actionStructureSize = Marshal.SizeOf(typeof(SC_ACTION));

				// Allocate a buffer with a bit of extra space at the end, so that if the first byte isn't aligned to a 64-bit
				// boundary, we can simply ignore the first few bytes and find the next 64-bit boundary.
				_buffer = Marshal.AllocHGlobal(data.lpsaActions.Length * actionStructureSize + 8);

				data.lpsaActionsPtr = _buffer;

				// Round up to the next multiple of 8 to get a 64-bit-aligned pointer.
				if ((data.lpsaActionsPtr.ToInt64() & 7) != 0)
				{
					data.lpsaActionsPtr += 8;
					data.lpsaActionsPtr -= (int)((long)data.lpsaActionsPtr & ~7);
				}

				// Copy the data from lpsaActions into the buffer.
				IntPtr elementPtr = data.lpsaActionsPtr;

				for (int i=0; i < data.lpsaActions.Length; i++, elementPtr += actionStructureSize)
					Marshal.StructureToPtr(data.lpsaActions[i], elementPtr, fDeleteOld: false);

				// Replace the lpsaActions array with a dummy that contains only one element, otherwise the P/Invoke marshaller
				// will allocate a buffer of size 1 and then write lpsaActions.Length items to it and corrupt memory.
				_originalActionsArray = data.lpsaActions;

				data.lpsaActions = new SC_ACTION[1];
			}

			public void Dispose()
			{
				Marshal.FreeHGlobal(_buffer);

				// Restore the lpsaActions array to its original value.
				_data.lpsaActions = _originalActionsArray;
			}
		}

		internal IDisposable Lock()
		{
			return new DataLock(ref this);
		}
	}
}
