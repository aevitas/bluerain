// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Diagnostics;
using BlueRain.Common;

namespace BlueRain
{
	/// <summary>
	/// A module that was forcibly loaded by the (remote) process by the framework.
	/// </summary>
	public class InjectedModule
	{
		/// <summary>
		/// Gets the module handle that represents this module.
		/// </summary>
		public ProcessModule Module { get; private set; }

		/// <summary>
		/// Gets the base address of this module in the target process.
		/// </summary>
		public IntPtr BaseAddress
		{
			get { return Module.BaseAddress; }
		}

		private InjectedModule()
		{
		} // Hide the default constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="InjectedModule"/> class.
		/// </summary>
		/// <param name="module">The module.</param>
		public InjectedModule(ProcessModule module)
		{
			Module = module;
		}

		/// <summary>
		/// Obtains a pointer to the specified exported function.
		/// </summary>
		/// <param name="exportName">Name of the export.</param>
		/// <returns></returns>
		/// <exception cref="BlueRain.Common.BlueRainInjectionException">
		/// Couldn't LoadLibrary into local thread to obtain export pointer!
		/// or
		/// Couldn't obtain function pointer for the specified export!
		/// </exception>
		public IntPtr GetExportPointer(string exportName)
		{
			// Fairly certain this method was first implemented by Cypher aka RaptorFactor, so kudos to him.
			IntPtr exportPtr;

			// Call LoadLibraryExW without resolving DLL references - if at all possible we don't want to run any remote code 
			// on "our" thread - all we need to do is resolve an export.
			using (var lib = SafeLoadLibrary.LoadLibraryEx(Module.FileName, (uint) LoadLibraryExOptions.DontResolveDllReferences))
			{
				if (lib == null)
					throw new BlueRainInjectionException("Couldn't LoadLibrary into local thread to obtain export pointer!");

				var funcPtr = UnsafeNativeMethods.GetProcAddress(lib.DangerousGetHandle(), exportName);
				if (funcPtr == IntPtr.Zero)
					throw new BlueRainInjectionException("Couldn't obtain function pointer for the specified export!");

				// abs - base = ptr
				exportPtr = funcPtr - Module.BaseAddress.ToInt32();
			}

			return exportPtr;
		}
	}
}