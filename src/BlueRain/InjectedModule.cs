// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlueRain.Common;

namespace BlueRain
{
	/// <summary>
	///     A module that was forcibly loaded by the (remote) process by the framework.
	/// </summary>
	public class InjectedModule : IDisposable
	{
		private readonly NativeMemory _memory;
		private bool _isDisposed;

		/// <summary>
		///     Initializes a new instance of the <see cref="InjectedModule" /> class.
		/// </summary>
		/// <param name="module">The module.</param>
		/// <param name="memory">The memory.</param>
		public InjectedModule(ProcessModule module, NativeMemory memory)
		{
			Module = module;
			_memory = memory;
		}

		/// <summary>
		///     Gets the module handle that represents this module.
		/// </summary>
		public ProcessModule Module { get; private set; }

		/// <summary>
		///     Gets the base address of this module in the target process.
		/// </summary>
		public IntPtr BaseAddress
		{
			get { return Module.BaseAddress; }
		}

		#region Implementation of IDisposable

		/// <summary>
		///     Releases unmanaged and - optionally - managed resources.
		/// </summary>
		public void Dispose()
		{
		}

		#endregion

		/// <summary>
		///     Obtains a pointer to the specified exported function.
		/// </summary>
		/// <param name="exportName">Name of the export.</param>
		/// <returns></returns>
		/// <exception cref="BlueRain.Common.BlueRainInjectionException">
		///     Couldn't LoadLibrary into local thread to obtain export pointer!
		///     or
		///     Couldn't obtain function pointer for the specified export!
		/// </exception>
		public IntPtr GetExportPointer(string exportName)
		{
			// Fairly certain this method was first implemented by Cypher aka RaptorFactor, so kudos to him.
			IntPtr exportPtr;

			// Call LoadLibraryExW without resolving DLL references - if at all possible we don't want to run any remote code 
			// on "our" thread - all we need to do is resolve an export.
			using (var lib = SafeLoadLibrary.LoadLibraryEx(Module.FileName, (uint) LoadLibraryExOptions.DontResolveDllReferences)
				)
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
		///     Calls the specified export with the specified args.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="exportName">Name of the export.</param>
		/// <param name="args">The arguments.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">exportName</exception>
		/// <exception cref="BlueRainException">
		///     Couldn't resolve export named with name  + exportName +  in remotely injected
		///     library.
		/// </exception>
		/// <exception cref="BlueRainInjectionException">
		///     WaitForSingleObject returned an unexpected value while waiting for the
		///     remote thread to be created for export call.
		/// </exception>
		public IntPtr Call<T>(string exportName, params T[] args) where T : struct
		{
			// The idea behind this method's quite simple. We first obtain a pointer to the specified export by resolving it,
			// then - if we have args to push to the func - we allocate a chunk of memory and write the arguments to it.
			// Finally we call CreateRemoteThread and pass the address of the chunk to the lpParameter.
			// This func *assumes* the callee's calling convention doesn't require its arguments to be on the stack.

			if (string.IsNullOrEmpty(exportName))
				throw new ArgumentNullException("exportName");

			var exportPtr = GetExportPointer(exportName);

			if (exportPtr == IntPtr.Zero)
				throw new BlueRainException("Couldn't resolve export named with name " + exportName +
											" in remotely injected library.");

			var kernel32Handle = UnsafeNativeMethods.GetModuleHandle(UnsafeNativeMethods.Kernel32);

			uint exitCode;
			SafeMemoryHandle threadHandle = null;
			AllocatedMemory alloc = null;
			try
			{
				// Only allocate a chunk for args if we have actual args to push to the func.
				if (args.Length > 0)
				{
					var size = Marshal.SizeOf(args);
					var bytesWritten = IntPtr.Zero;
					alloc = _memory.Allocate((UIntPtr) size);

					foreach (var a in args)
					{
						alloc.Write(bytesWritten, a);
						bytesWritten += Marshal.SizeOf(a);
					}
				}

				threadHandle = UnsafeNativeMethods.CreateRemoteThread(kernel32Handle.DangerousGetHandle(), IntPtr.Zero, 0, exportPtr,
					alloc != null ? alloc.Address : IntPtr.Zero, 0x0, IntPtr.Zero);

				if (UnsafeNativeMethods.WaitForSingleObject(threadHandle.DangerousGetHandle(), uint.MaxValue) != 0x0)
					throw new BlueRainInjectionException(
						"WaitForSingleObject returned an unexpected value while waiting for the remote thread to be created for export call.");

				UnsafeNativeMethods.GetExitCodeThread(threadHandle.DangerousGetHandle(), out exitCode);
			}
			finally
			{
				if (kernel32Handle != null && !kernel32Handle.IsClosed)
					kernel32Handle.Close();

				if (threadHandle != null && !threadHandle.IsClosed)
					threadHandle.Close();

				// Make sure we free the chunk for the args.
				if (alloc != null)
					alloc.Dispose();
			}

			return (IntPtr) exitCode;
		}

		/// <summary>
		///     Frees this library through calling FreeLibrary.
		/// </summary>
		/// <param name="isLocalProcessMemory">if set to <c>true</c> [is local process memory].</param>
		/// <returns>
		///     The exit code of FreeLibrary
		/// </returns>
		/// <exception cref="BlueRainException">Couldn't find FreeMemory in Kernel32!</exception>
		/// <exception cref="BlueRainInjectionException">
		///     WaitForSingleObject returned an unexpected value while waiting for the
		///     remote thread to be created for module eject.
		/// </exception>
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
					throw new BlueRainInjectionException(
						"WaitForSingleObject returned an unexpected value while waiting for the remote thread to be created for module eject.");

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