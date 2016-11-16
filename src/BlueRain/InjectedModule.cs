// Copyright (C) 2013-2016 aevitas
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
		public ProcessModule Module { get; }

		/// <summary>
		///     Gets the base address of this module in the target process.
		/// </summary>
		public IntPtr BaseAddress => Module.BaseAddress;

		#region Implementation of IDisposable

		/// <summary>
		///     Releases unmanaged and - optionally - managed resources.
		/// </summary>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			Free(_memory is LocalProcessMemory);

			_isDisposed = true;
		}

		#endregion

		/// <summary>
		///     Obtains a pointer to the specified exported function.
		/// </summary>
		/// <param name="exportName">Name of the export.</param>
		/// <returns></returns>
		/// <exception cref="InjectionException">
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
			using (var lib = SafeLibraryHandle.LoadLibraryEx(Module.FileName, (uint) LoadLibraryExOptions.DontResolveDllReferences)
				)
			{
				if (lib == null)
					throw new InjectionException("Couldn't LoadLibrary into local thread to obtain export pointer!");

				var funcPtr = UnsafeNativeMethods.GetProcAddress(lib.DangerousGetHandle(), exportName);
				if (funcPtr == IntPtr.Zero)
					throw new InjectionException("Couldn't obtain function pointer for the specified export!");

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
		/// <param name="parameter">The argument.</param>
		/// <param name="callWithParameter">
		///     if set to <c>true</c>, the specified export will be called with the specified
		///     parameter.
		/// </param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">exportName</exception>
		/// <exception cref="BlueRainException">
		///     Couldn't resolve export named with name  + exportName +  in remotely injected
		///     library.
		/// </exception>
		/// <exception cref="InjectionException">
		///     WaitForSingleObject returned an unexpected value while waiting for the
		///     remote thread to be created for export call.
		/// </exception>
		public IntPtr Call<T>(string exportName, T parameter, bool callWithParameter = false) where T : struct
		{
			// The idea behind this method's quite simple. We can only call an __stdcall export with one parameter,
			// any subsequent arguments would require injecting a stub to push the args to the stack. 
			// We don't support that as of yet - we'll resort to allocating and calling the export with a single parameter for now.

			if (string.IsNullOrEmpty(exportName))
				throw new ArgumentNullException(nameof(exportName));

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
				if (callWithParameter)
				{
					var size = Marshal.SizeOf(parameter);
					alloc = _memory.Allocate((UIntPtr) size);

					alloc.Write(IntPtr.Zero, parameter);
				}

				threadHandle = UnsafeNativeMethods.CreateRemoteThread(kernel32Handle.DangerousGetHandle(), IntPtr.Zero, 0, exportPtr,
					alloc?.Address ?? IntPtr.Zero, 0x0, IntPtr.Zero);

				if (UnsafeNativeMethods.WaitForSingleObject(threadHandle.DangerousGetHandle(), uint.MaxValue) != 0x0)
					throw new InjectionException(
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
				alloc?.Dispose();
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
		/// <exception cref="InjectionException">
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
					throw new InjectionException(
						"WaitForSingleObject returned an unexpected value while waiting for the remote thread to be created for module eject.");

				UnsafeNativeMethods.GetExitCodeThread(threadHandle.DangerousGetHandle(), out exitCode);
			}
			finally
			{
				if (!kernel32Handle.IsClosed)
					kernel32Handle.Close();

				if (threadHandle != null && !threadHandle.IsClosed)
					threadHandle.Close();
			}

			return exitCode != 0;
		}
	}
}