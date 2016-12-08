// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using Microsoft.Win32.SafeHandles;

namespace BlueRain
{
	/// <summary>
	///     Provides a safe handle for a library loaded via LoadLibraryEx.
	/// </summary>
	public class SafeLibraryHandle : SafeHandleMinusOneIsInvalid
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="SafeLibraryHandle" /> class.
		/// </summary>
		/// <param name="ownsHandle">
		///     true to reliably release the handle during the finalization phase; false to prevent reliable
		///     release (not recommended).
		/// </param>
		public SafeLibraryHandle(bool ownsHandle)
			: base(ownsHandle)
		{
		}

		/// <summary>
		///     Loads specified library by calling LoadLibraryExW.
		/// </summary>
		/// <param name="library">The library.</param>
		/// <param name="loadLibraryOptions">The load library options.</param>
		/// <returns></returns>
		public static unsafe SafeLibraryHandle LoadLibraryEx(string library, uint loadLibraryOptions = 0)
		{
			var result = UnsafeNativeMethods.LoadLibraryExW(library, null, loadLibraryOptions);
			if (result.IsInvalid)
			{
				result.SetHandleAsInvalid();
			}
			return result;
		}

		/// <summary>
		///     When overridden in a derived class, executes the code required to free the handle.
		/// </summary>
		/// <returns>
		///     true if the handle is released successfully; otherwise, in the event of a catastrophic failure, false. In this
		///     case, it generates a releaseHandleFailed MDA Managed Debugging Assistant.
		/// </returns>
		protected override bool ReleaseHandle()
		{
			return UnsafeNativeMethods.FreeLibrary(handle);
		}
	}
}