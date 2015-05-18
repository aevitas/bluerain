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
		private bool _isDisposed;

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

		/// <summary>
		/// Frees this library through calling FreeLibrary.
		/// </summary>
		/// <param name="isLocalProcessMemory">if set to <c>true</c> [is local process memory].</param>
		/// <returns>
		/// The exit code of FreeLibrary
		/// </returns>
		/// <exception cref="BlueRainException">Couldn't find FreeMemory in Kernel32!</exception>
		/// <exception cref="BlueRainInjectionException">WaitForSingleObject returned an unexpected value while waiting for the remote thread to be created for module eject.</exception>
		public bool Free(bool isLocalProcessMemory)
		{
			// Easy game easy life.
			if (isLocalProcessMemory)
				return UnsafeNativeMethods.FreeLibrary(BaseAddress);

			var kernel32Handle = UnsafeNativeMethods.GetModuleHandle(UnsafeNativeMethods.Kernel32);
			var freeLibrary = UnsafeNativeMethods.GetProcAddress(kernel32Handle.DangerousGetHandle(), "FreeLibrary");
			if (freeLibrary == IntPtr.Zero)
				throw new BlueRainException("Couldn't find FreeMemory in Kernel32!");

			SafeMemoryHandle threadHandle = null;
			uint exitCode;

			try
			{
				threadHandle = UnsafeNativeMethods.CreateRemoteThread(kernel32Handle.DangerousGetHandle(), IntPtr.Zero, 0,
					freeLibrary, BaseAddress, 0, IntPtr.Zero);

				if (UnsafeNativeMethods.WaitForSingleObject(threadHandle.DangerousGetHandle(), uint.MaxValue) != 0x0)
					throw new BlueRainInjectionException("WaitForSingleObject returned an unexpected value while waiting for the remote thread to be created for module eject.");

				UnsafeNativeMethods.GetExitCodeThread(threadHandle.DangerousGetHandle(), out exitCode);
			}
			finally
			{
				if (kernel32Handle != null && !kernel32Handle.IsClosed)
					kernel32Handle.Close();

				if (threadHandle != null && !threadHandle.IsClosed)
					threadHandle.Close();
			}

			return exitCode != 0;
		}
	}
}