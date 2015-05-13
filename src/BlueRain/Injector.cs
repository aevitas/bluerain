// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Collections.Generic;

namespace BlueRain
{
	/// <summary>
	/// This type provides injector support for both internal and external memory manipulators.
	/// When out of process, we call CreateRemoteThread to force the proc to load our module - when we're in-process,
	/// we just call LoadLibrary directly. This way we can support both types with a single class, depending on what type
	/// of NativeMemory implementation we're constructed with.
	/// </summary>
	public class Injector : IDisposable
	{
		private readonly NativeMemory _memory;
		private bool _ejectOnDispose;

		private Dictionary<string, InjectedModule> _injectedModules;

		/// <summary>
		/// Gets the modules this injector has successfully injected.
		/// </summary>
		/// <value>
		/// The injected modules.
		/// </value>
		public IReadOnlyDictionary<string, InjectedModule> InjectedModules
		{
			get { return _injectedModules; }
		}

		// Make sure we hide the default constructor - this should only be initialized from a Memory instance.
		private Injector()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Injector" /> class.
		/// </summary>
		/// <param name="memory">The memory.</param>
		/// <param name="ejectOnDispose">if set to <c>true</c> library is freed from the target process when the injector is disposed.</param>
		internal Injector(NativeMemory memory, bool ejectOnDispose = false)
		{
			var epm = memory as ExternalProcessMemory;
			if (epm != null && epm.ProcessHandle.IsInvalid)
				throw new ArgumentException(
					"The specified ExternalProcessMemory has an invalid ProcessHandle - can not construct injector without a valid handle!");

			_memory = memory;
			_ejectOnDispose = ejectOnDispose;
			_injectedModules = new Dictionary<string, InjectedModule>();
		}

		#region Implementation of IDisposable

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <exception cref="NotImplementedException"></exception>
		public void Dispose()
		{
		}

		#endregion
	}
}