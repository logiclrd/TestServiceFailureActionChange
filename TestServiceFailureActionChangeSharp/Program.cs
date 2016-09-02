using System;
using System.IO;
using TestServiceFailureActionChangeSharp.WindowsAPI;

namespace TestServiceFailureActionChangeSharp
{
	class Program
	{
		static int Main(string[] args)
		{
			if (args.Length != 1)
			{
				Console.WriteLine("Usage: {0} <name of service to adjust>", Path.GetFileName(typeof(Program).Assembly.Location));
				Console.WriteLine("If you'd like to run this test against a dummy service, use these commands:");
				Console.WriteLine("=> Create:   sc create TestSvc binPath= {0} start= demand", EscapeArg(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe")));
				Console.WriteLine("=> Remove:   sc delete TestSvc");

				return 2;
			}

			Console.WriteLine("Adjusting service failure actions for service: {0}", args[0]);

			var actions =
				new ServiceFailureActions()
				{
					ResetPeriod = TimeSpan.FromSeconds(60),
					RestartCommand = null,
					RebootMessage = null,
					PerformFailureActionsOnStopWithNonZeroExitCode = false,
					Actions =
						new ServiceFailureAction[]
						{
							new ServiceFailureAction() { Type = ServiceFailureActionType.RestartService, DelayBeforeAction = TimeSpan.FromSeconds(5) },
							new ServiceFailureAction() { Type = ServiceFailureActionType.RestartService, DelayBeforeAction = TimeSpan.FromSeconds(15) },
							new ServiceFailureAction() { Type = ServiceFailureActionType.RestartService, DelayBeforeAction = TimeSpan.FromSeconds(25) },
							new ServiceFailureAction() { Type = ServiceFailureActionType.RestartService, DelayBeforeAction = TimeSpan.FromSeconds(35) },
							new ServiceFailureAction() { Type = ServiceFailureActionType.RestartService, DelayBeforeAction = TimeSpan.FromSeconds(45) },
							new ServiceFailureAction() { Type = ServiceFailureActionType.None, DelayBeforeAction = TimeSpan.FromSeconds(0) },
						},
				};

			try
			{
				actions.ApplyToService(args[0]);

				Console.WriteLine("The operation appears to have succeeded.");

				return 0;
			}
			catch (Exception e)
			{
				Console.WriteLine("EXCEPTION:");
				Console.WriteLine(e);

				return 1;
			}
		}

		static string EscapeArg(string arg)
		{
			if (arg.IndexOfAny(" \t\"".ToCharArray()) >= 0)
				return '"' + arg.Replace("\"", "\\\"");
			else
				return arg;
		}
	}
}
