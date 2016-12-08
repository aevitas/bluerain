using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BlueRain;

namespace BlueInject
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.Title = "BlueRain.Injector";

			Console.WriteLine("BlueRain Injector v1.0 - aevitas 2015, all rights reserved");
			Console.WriteLine("This software is licensed under the Apache License 2\n\n");

			if (args.Length < 2)
			{
				Console.WriteLine("Invalid args specified. Usage follows:\n");
				Console.WriteLine("BlueInject -p procname dll");
				Console.WriteLine("\tprocname:\tProcess name without extension (e.g. notepad)");
				Console.WriteLine("\tdll:\t\tThe DLL file to be injected");

				Console.ReadLine();

				return;
			}

			var mode = args[0];
			var procName = args[1];
			var libName = args[2];

			Console.WriteLine("Waiting for process \"{0}\"\n", procName);

			Process proc = null;
			while (proc == null)
			{
				try
				{
					var procs = Process.GetProcessesByName(procName);

					if (procs.Length < 1)
						continue;

					if (procs.Length == 1)
					{
						proc = procs.First();
						continue;
					}

					Console.WriteLine("Found {0} processes that match the specified name.", procs.Length);
					Console.WriteLine("Will be using the first instance of the process I can find.\nPress ENTER to proceed...");
					Console.ReadLine();

					proc = procs.First();
				}
				catch (Win32Exception)
				{
					// Don't care.
				}
			}

			if (proc.Modules.Cast<ProcessModule>().Any(mod => Path.GetFileName(mod.FileName).Equals(libName, StringComparison.OrdinalIgnoreCase)))
			{
				Console.WriteLine("Process with ID {0} already contains module {1}! Already injected?", proc.Id, libName);
				Console.ReadLine();
				return;
			}

			var epm = new ExternalProcessMemory(proc, true);

			var module = epm.Injector.Inject(Path.GetFullPath(libName));

			if (module == null)
				Console.WriteLine("Injection failed!");

			Console.ReadLine();
		}
	}
}
