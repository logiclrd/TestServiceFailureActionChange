using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace TestServiceFailureActionChangeSharp.WindowsAPI
{
	class ServiceFailureActions
	{
		public TimeSpan ResetPeriod;
		public string RebootMessage;
		public string RestartCommand;
		public ServiceFailureAction[] Actions;
		public bool PerformFailureActionsOnStopWithNonZeroExitCode;

		public void ApplyToService(string serviceName)
		{
			var failureActionsStructure = BuildFailureActionsStructure();
			var failureActionsFlagStructure = BuildFailureActionsFlagStructure();

			using (var serviceController = new ServiceController(serviceName))
			{
				bool success;

				var serviceHandle = serviceController.ServiceHandle;

				using (failureActionsStructure.Lock())
				{
					/*
					 * Diagnostic code: Marshal the structure to separate buffer, to inspect the marshaling behaviour and keep it off the stack.
					 * 

					int defaultSize = Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONSW));
					int actualSize = Marshal.SizeOf(failureActionsStructure);

					IntPtr structure = Marshal.AllocHGlobal(100000);

					Marshal.StructureToPtr(failureActionsStructure, structure, fDeleteOld: false);
					 */

					success = NativeMethods.ChangeServiceConfig2W(
						(int)serviceHandle.DangerousGetHandle(),
						(int)ServiceConfigType.SERVICE_CONFIG_FAILURE_ACTIONS,
						ref failureActionsStructure);

					if (!success)
						throw new Win32Exception();

					/*
					Marshal.DestroyStructure(structure, typeof(SERVICE_FAILURE_ACTIONSW));
					Marshal.FreeHGlobal(structure);
					 */
				}

				success = NativeMethods.ChangeServiceConfig2W(
					serviceHandle.DangerousGetHandle(),
					ServiceConfigType.SERVICE_CONFIG_FAILURE_ACTIONS_FLAG,
					ref failureActionsFlagStructure);

				if (!success)
					throw new Win32Exception();
			}
		}

		private SERVICE_FAILURE_ACTIONSW BuildFailureActionsStructure()
		{
			var ret = new SERVICE_FAILURE_ACTIONSW();

			ret.dwResetPeriod = (int)Math.Round(this.ResetPeriod.TotalMilliseconds);
			ret.lpRebootMsg = this.RebootMessage ?? "";
			ret.lpCommand = this.RestartCommand ?? "";
			ret.cActions = this.Actions.Length;
			ret.lpsaActions = new SC_ACTION[this.Actions.Length];

			for (int i = 0; i < this.Actions.Length; i++)
			{
				ret.lpsaActions[i] =
					new SC_ACTION()
					{
						Type = (SC_ACTION_TYPE)(int)this.Actions[i].Type,
						Delay = (int)Math.Round(this.Actions[i].DelayBeforeAction.TotalMilliseconds),
					};
			}

			return ret;
		}

		private SERVICE_FAILURE_ACTIONS_FLAG BuildFailureActionsFlagStructure()
		{
			var ret = new SERVICE_FAILURE_ACTIONS_FLAG();

			ret.fFailureActionsOnNonCrashFailures = this.PerformFailureActionsOnStopWithNonZeroExitCode;

			return ret;
		}
	}
}
