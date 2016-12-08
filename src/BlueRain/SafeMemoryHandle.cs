// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using Microsoft.Win32.SafeHandles;

namespace BlueRain
{
	/// <summary>
	///     Class of safe handle which uses 0 or -1 as an invalid handle.
	/// </summary>
	[HostProtection(MayLeakOnAbort = true)]
	[SuppressUnmanagedCodeSecurity]
	public class SafeMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="SafeMemoryHandle" /> class.
		/// </summary>
		public SafeMemoryHandle()
			: base(true)
		{
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		protected internal static extern bool CloseHandle(IntPtr hObject);

		#region Overrides of SafeHandle

		/// <summary>
		///     When overridden in a derived class, executes the code required to free the handle.
		/// </summary>
		/// <returns>
		///     true if the handle is released successfully; otherwise, in the event of a catastrophic failure, false. In this
		///     case, it generates a releaseHandleFailed MDA Managed Debugging Assistant.
		/// </returns>
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		protected override bool ReleaseHandle()
		{
			return CloseHandle(handle);
		}

		#endregion
	}
}
