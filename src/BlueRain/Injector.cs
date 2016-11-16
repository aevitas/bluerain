// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BlueRain.Common;

namespace BlueRain
{
	/// <summary>
	///     This type provides injector support for both internal and external memory manipulators.
	///     When out of process, we call CreateRemoteThread to force the proc to load our module - when we're in-process,
	///     we just call LoadLibrary directly. This way we can support both types with a single class, depending on what type
	///     of NativeMemory implementation we're constructed with.
	/// </summary>
	public class Injector : IDisposable
	{
		private readonly bool _ejectOnDispose;

		private readonly Dictionary<string, InjectedModule> _injectedModules;
		private readonly NativeMemory _memory;
		private bool _disposed;

		/// <summary>
		///     Initializes a new instance of the <see cref="Injector" /> class.
		/// </summary>
		/// <param name="memory">The memory.</param>
		/// <param name="ejectOnDispose">
		///     if set to <c>true</c> library is freed from the target process when the injector is
		///     disposed.
		/// </param>
		internal Injector(NativeMemory memory, bool ejectOnDispose = false)
		{
			Requires.NotNull(memory, nameof(memory));

			var epm = memory as ExternalProcessMemory;
			if (epm != null && epm.ProcessHandle.IsInvalid)
				throw new ArgumentException(
					"The specified ExternalProcessMemory has an invalid ProcessHandle - can not construct injector without a valid handle!");

			_memory = memory;
			_ejectOnDispose = ejectOnDispose;
			_injectedModules = new Dictionary<string, InjectedModule>();
		}

		private bool IsExternal => _memory is ExternalProcessMemory;

		/// <summary>
		///     Gets the modules this injector has successfully injected.
		///     The key represents the full path to the module, the value the InjectedModule type.
		/// </summary>
		/// <value>
		///     The injected modules.
		/// </value>
		public IReadOnlyDictionary<string, InjectedModule> InjectedModules => _injectedModules;

		#region Implementation of IDisposable

		/// <summary>
		///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
				return;

			if (_ejectOnDispose)
			{
				try
				{
					// Use this once we figure out how to properly notify success/failure.
					bool success;
					foreach (var m in InjectedModules)
						if (!m.Value.Free(IsExternal))
							success = false;
				}
				catch (BlueRainException)
				{
					// We don't want Dispose to throw when ejecting a module goes wrong.
				}
			}

			_disposed = true;
		}

		#endregion

		/// <summary>
		///     Injects the specified library path into the process the memory instance is currently attached to.
		/// </summary>
		/// <param name="libraryPath">The library path.</param>
		/// <returns></returns>
		/// <exception cref="System.IO.FileNotFoundException">Couldn't find the specified library to inject:  + libraryPath</exception>
		/// <exception cref="System.InvalidOperationException">Can not inject a library without a valid Memory instance!</exception>
		public InjectedModule Inject(string libraryPath)
		{
			if (!File.Exists(libraryPath))
				throw new FileNotFoundException("Couldn't find the specified library to inject: " + libraryPath);

			if (_memory == null)
				throw new InvalidOperationException("Can not inject a library without a valid Memory instance!");

			// External requires some additional love to inject a library (CreateRemoteThread etc.)
			var epm = _memory as ExternalProcessMemory;
			if (epm != null)
				return InjectLibraryExternal(libraryPath);

			// Otherwise, just call LoadLibraryW and be done with it.
			return InjectLibraryInternal(libraryPath);
		}

		private InjectedModule InjectLibraryExternal(string libraryPath)
		{
			// Injecting remotely consists of a few steps:
			// 1. GetProcAddress on kernel32 to get a pointer to LoadLibraryW
			// 2. Allocate chunk of memory to write the path to our library to
			// 3. CreateRemoteThread that calls LoadLibraryW and pass it a pointer to our chunk
			// 4. Get thread's exit code
			// 5. ????
			// 6. Profit
			var memory = _memory as ExternalProcessMemory;

			// Realistically won't happen, but code analysis complains about it being null.
			if (memory == null)
				throw new InvalidOperationException("A valid memory instance is required for InjectLibraryExternal!");

			if (memory.ProcessHandle.IsInvalid)
				throw new InvalidOperationException("Can not inject library with an invalid ProcessHandle in ExternalProcessMemory!");

			var path = Path.GetFullPath(libraryPath);
			var libraryFileName = Path.GetFileName(libraryPath);

			ProcessModule ourModule;

			SafeMemoryHandle threadHandle = null;
			SafeMemoryHandle kernel32Handle = null;

			try
			{
				kernel32Handle = UnsafeNativeMethods.GetModuleHandle(UnsafeNativeMethods.Kernel32);

				var loadLibraryPtr =
					UnsafeNativeMethods.GetProcAddress(kernel32Handle.DangerousGetHandle(), "LoadLibraryW");

				if (loadLibraryPtr == IntPtr.Zero)
					throw new InjectionException("Couldn't obtain handle to LoadLibraryW in remote process!");

				var pathBytes = Encoding.Unicode.GetBytes(path);

				using (var alloc = memory.Allocate((UIntPtr) pathBytes.Length))
				{
					alloc.WriteBytes(IntPtr.Zero, pathBytes);

					threadHandle = UnsafeNativeMethods.CreateRemoteThread(memory.ProcessHandle.DangerousGetHandle(), IntPtr.Zero, 0x0,
						loadLibraryPtr, alloc.Address, 0, IntPtr.Zero);

					if (threadHandle.IsInvalid)
						throw new InjectionException(
							"Couldn't obtain a handle to the remotely created thread for module injection!");
				}

				// ThreadWaitValue.Infinite = 0xFFFFFFFF = uint.MaxValue - Object0 = 0x0
				if (UnsafeNativeMethods.WaitForSingleObject(threadHandle.DangerousGetHandle(), uint.MaxValue) != 0x0)
					throw new InjectionException(
						"WaitForSingleObject returned an unexpected value while waiting for the remote thread to be created for module injection.");

				uint exitCode;
				if (!UnsafeNativeMethods.GetExitCodeThread(threadHandle.DangerousGetHandle(), out exitCode))
					throw new InjectionException("Couldn't obtain exit code for LoadLibraryW thread in remote process!");

				// Let's make sure our module is actually present in the remote process now (assuming it's doing nothing special to hide itself..)
				var moduleHandle = UnsafeNativeMethods.GetModuleHandle(libraryFileName);
				if (moduleHandle.IsInvalid)
					throw new InjectionException(
						"Couldn't obtain module handle to remotely injected library after LoadLibraryW!");

				ourModule = memory.Process.Modules.Cast<ProcessModule>()
					.FirstOrDefault(m => m.BaseAddress == moduleHandle.DangerousGetHandle());
			}
			finally
			{
				if (threadHandle != null && !threadHandle.IsClosed)
					threadHandle.Close();

				if (kernel32Handle != null && !kernel32Handle.IsClosed)
					kernel32Handle.Close();
			}

			// We can safely do this - if something went wrong we wouldn't be here.
			var module = new InjectedModule(ourModule, _memory);
			_injectedModules.Add(path, module);
			return module;
		}

		private InjectedModule InjectLibraryInternal(string libraryPath)
		{
			// It's hardly "injecting" when we're in-process, but for the sake of keeping the API uniform we'll go with it.
			// All we have to do is call LoadLibrary on the local process and wrap it in an InjectedModule type.
			var lib = SafeLibraryHandle.LoadLibraryEx(libraryPath);

			if (lib == null)
				throw new InjectionException("LoadLibrary failed in local process!");

			var module = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().FirstOrDefault(s => s.FileName == libraryPath);

			if (module == null)
				throw new InjectionException("The injected library couldn't be found in the Process' module list!");

			return new InjectedModule(module, _memory);
		}

		/// <summary>
		///     Ejects the specified path.
		/// </summary>
		/// <param name="path">The path.</param>
		/// <returns></returns>
		/// <exception cref="BlueRainException">Couldn't eject the specified library - it wasn't injected by this injector:  + path</exception>
		/// <exception cref="InjectionException">
		///     WaitForSingleObject returned an unexpected value while waiting for the
		///     remote thread to be created for module eject.
		/// </exception>
		public bool Eject(string path)
		{
			if (!InjectedModules.ContainsKey(path))
				throw new BlueRainException("Couldn't eject the specified library - it wasn't injected by this injector: " + path);

			var lib = InjectedModules.FirstOrDefault(s => s.Key == path);

			return lib.Value.Free(IsExternal);
		}
	}
}