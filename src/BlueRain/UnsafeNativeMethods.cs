// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace BlueRain
{
	/// <summary>
	///     Contains all required P/invoke or "unsafe" Win32 call signatures.
	/// </summary>
	internal static class UnsafeNativeMethods
	{
		// This may be different when the library is compiled for coreclr platforms.
		internal const string Kernel32 = "kernel32";

		[DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern unsafe SafeLibraryHandle LoadLibraryExW([In] string lpwLibFileName, [In] void* hFile,
			[In] uint dwFlags);

		[DllImport(Kernel32, ExactSpelling = true, SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		internal static extern bool FreeLibrary([In] IntPtr hModule);

		[DllImport(Kernel32, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
		internal static extern IntPtr GetProcAddress([In] IntPtr hModule, [In] string procName);

		[DllImport(Kernel32, CharSet = CharSet.Auto)]
		internal static extern SafeMemoryHandle GetModuleHandle(string lpModuleName);

		[DllImport(Kernel32, ExactSpelling = true, SetLastError = true)]
		internal static extern SafeMemoryHandle CreateRemoteThread(IntPtr hProcess,
			IntPtr lpThreadAttributes, uint dwStackSize, IntPtr
				lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

		[DllImport(Kernel32, SetLastError = true)]
		internal static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

		[DllImport(Kernel32, SetLastError = true, ExactSpelling = true)]
		internal static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
	}
}