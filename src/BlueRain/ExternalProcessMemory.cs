// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using BlueRain.Common;

namespace BlueRain
{
	/// <summary>
	/// Provides functionality for reading from, writing to, and allocating memory in external processes.
	/// This class relies on a handle to the remote process and to its main thread to perform memory reading and writing.
	/// </summary>
	public class ExternalProcessMemory : NativeMemory
	{
		internal readonly SafeMemoryHandle ProcessHandle;
		private SafeMemoryHandle _mainThreadHandle;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExternalProcessMemory"/> class.
		/// </summary>
		/// <param name="process">The process.</param>
		/// <param name="access">The access flags.</param>
		/// <exception cref="PlatformNotSupportedException">The platform is Windows 98 or Windows Millennium Edition (Windows Me); set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this property on Windows 98 and Windows Me.</exception>
		/// <exception cref="InvalidOperationException">The process's <see cref="P:System.Diagnostics.Process.Id" /> property has not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object. </exception>
		public ExternalProcessMemory(Process process,
			ProcessAccess access) : base(process)
		{
			ProcessHandle = OpenProcess(access, false, process.Id);

			// Obtain a handle to the process' main thread so we can suspend/resume it whenever we need to.0
			_mainThreadHandle = OpenThread(ThreadAccess.ALL, false, (uint) process.Threads[0].Id);

			// TODO: Obtain SeDebugPrivilege here so we can CreateRemoteThread for lib injection!
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExternalProcessMemory"/> class.
		/// This constructor opens a handle to the specified process using the following flags:
		/// ProcessAccess.CreateThread | ProcessAccess.QueryInformation | ProcessAccess.VMRead | ProcessAccess.VMWrite | ProcessAccess.VMOperation | ProcessAccess.SetInformation
		/// </summary>
		/// <param name="process">The process.</param>
		/// <exception cref="PlatformNotSupportedException">The platform is Windows 98 or Windows Millennium Edition (Windows Me); set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this property on Windows 98 and Windows Me.</exception>
		/// <exception cref="InvalidOperationException">The process's <see cref="P:System.Diagnostics.Process.Id" /> property has not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object. </exception>
		public ExternalProcessMemory(Process process) : this (process,
				ProcessAccess.CreateThread | ProcessAccess.QueryInformation | ProcessAccess.VMRead |
				ProcessAccess.VMWrite | ProcessAccess.VMOperation | ProcessAccess.SetInformation)
		{
		}

		// VirtualAllocEx specifies size as a size_t - we'll use the .NET equivalent.
		private IntPtr AllocateMemory(UIntPtr size)
		{
			return VirtualAllocEx(ProcessHandle, IntPtr.Zero, size, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
		}

		/// <summary>
		/// Allocates a chunk of memory of the specified size in the remote process.
		/// </summary>
		/// <param name="size">The size.</param>
		/// <returns></returns>
		/// <exception cref="BlueRainException"></exception>
		public AllocatedMemory Allocate(UIntPtr size)
		{
			var chunk = AllocateMemory(size);

			if (chunk != IntPtr.Zero)
			{
				return new AllocatedMemory(chunk, size.ToUInt32(), this);
			}

			throw new BlueRainException(string.Format("Couldn't allocate {0} sized chunk!", size.ToUInt32()));
		}

		#region P/Invokes

		[DllImport("kernel32", EntryPoint = "OpenThread", SetLastError = true)]
		public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

		[DllImport("kernel32.dll")]
		private static extern SafeMemoryHandle OpenProcess(
			ProcessAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static unsafe extern bool ReadProcessMemory(
			SafeMemoryHandle hProcess, IntPtr lpBaseAddress, byte* lpBuffer, int dwSize, out int lpNumberOfBytesRead);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool WriteProcessMemory(
			SafeMemoryHandle hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

		[DllImport("kernel32.dll")]
		private static extern bool VirtualProtectEx(
			SafeMemoryHandle hProcess, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		static extern IntPtr VirtualAllocEx(SafeMemoryHandle hProcess, IntPtr lpAddress,
		   UIntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

		#endregion

		#region Overrides of NativeMemory

		/// <summary>
		/// Reads the specified amount of bytes from the specified address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
		/// <exception cref="BlueRainReadException">Thrown if the ReadProcessMemory operation fails, or doesn't return the specified amount of bytes.</exception>
		public override unsafe byte[] ReadBytes(IntPtr address, int count, bool isRelative = false)
		{
			Requires.NotEqual(count, 0, "count");
			Requires.NotEqual(address, IntPtr.Zero, "address");

			if (isRelative)
				address = ToAbsolute(address);

			// We should consider not throwing if the amount of bytes read doesn't match the expected amount of bytes.
			// It is perfectly possible for user code to handle these scenarios - we just have to think of a proper way of notifying them.

			var buffer = new byte[count];
			fixed (byte* b = buffer)
			{
				int numRead;
				if (ReadProcessMemory(ProcessHandle, address, b, count, out numRead) && numRead == count)
					return buffer;
			}

			throw new BlueRainReadException(address, count);
		}

		/// <summary>
		/// Writes the specified bytes at the specified address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="bytes">The bytes.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		/// <exception cref="BlueRain.Common.BlueRainWriteException"></exception>
		/// <exception cref="OverflowException">The array is multidimensional and contains more than <see cref="F:System.Int32.MaxValue" /> elements.</exception>
		public override void WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");
			Requires.NotEqual(bytes.Length, 0, "bytes");

			uint oldProtect;
			int numWritten;

			// dwSize is a size_t, meaning it *may* differ depending architecture. Though very unlikely to cause trouble if 
			// defined as uint, we're passing it as IntPtr here as it is the closest .NET equivalent.
			VirtualProtectEx(ProcessHandle, address, (IntPtr) bytes.Length, (uint) MemoryProtection.ExecuteReadWrite,
				out oldProtect);
			WriteProcessMemory(ProcessHandle, address, bytes, bytes.Length, out numWritten);
			VirtualProtectEx(ProcessHandle, address, (IntPtr) bytes.Length, oldProtect, out oldProtect);

			// All we need to check - if WriteProcessMemory fails numWriten will be 0.
			var success = numWritten == bytes.Length;
			if (!success)
				throw new BlueRainWriteException(address, bytes.Length);
		}

		/// <summary>
		/// Reads a value of the specified type at the specified address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
		/// <exception cref="BlueRainReadException">Thrown if the ReadProcessMemory operation fails, or doesn't return the specified amount of bytes.</exception>
		/// <exception cref="MissingMethodException">The class specified by <paramref name="T" /> does not have an accessible default constructor. </exception>
		public override unsafe T Read<T>(IntPtr address, bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");

			var size = MarshalCache<T>.Size;
			// Unsafe context doesn't allow for the use of await - run the read synchronously.
			var buffer = ReadBytes(address, size, isRelative);

			fixed (byte* b = buffer)
				return Marshal.PtrToStructure<T>((IntPtr) b);
		}

		/// <summary>
		/// Reads the specified amount of values of the specified type at the specified address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
		/// <exception cref="BlueRainReadException">Thrown if the ReadProcessMemory operation fails, or doesn't return the specified amount of bytes.</exception>
		/// <exception cref="MissingMethodException">The class specified by <paramref name="T" /> does not have an accessible default constructor. </exception>
		public override T[] Read<T>(IntPtr address, int count, bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");

			var size = MarshalCache<T>.Size;

			T[] ret = new T[count];

			// Read = add + n * size
			for (int i = 0; i < count; i++)
				ret[i] = Read<T>(address + (i*size), isRelative);

			return ret;
		}

		/// <summary>
		/// Reads a value of the specified type at the specified address. This method is used if multiple-pointer dereferences
		/// are required.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <param name="addresses">The addresses.</param>
		/// <returns></returns>
		/// <exception cref="BlueRainReadException">Thrown if the ReadProcessMemory operation fails, or doesn't return the specified amount of bytes.</exception>
		/// <exception cref="MissingMethodException">The class specified by <paramref name="T" /> does not have an accessible default constructor. </exception>
		/// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
		/// <exception cref="OverflowException">On a 64-bit platform, the value of this instance is too large or too small to represent as a 32-bit signed integer. </exception>
		public override T Read<T>(bool isRelative = false, params IntPtr[] addresses)
		{
			Requires.Condition(() => addresses.Length > 0, "addresses");
			Requires.NotEqual(addresses[0], IntPtr.Zero, "addresses");

			// We can just read right away if it's a single address - avoid the hassle.
			if (addresses.Length == 1)
				return Read<T>(addresses[0], isRelative);

			IntPtr tempPtr = Read<IntPtr>(addresses[0], isRelative);

			for (int i = 1; i < addresses.Length - 1; i++)
				tempPtr = Read<IntPtr>(tempPtr + addresses[i].ToInt32(), isRelative);

			return Read<T>(tempPtr, isRelative);
		}

		/// <summary>
		/// Writes the specified value at the specfied address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="value">The value.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		/// <exception cref="OverflowException">The array is multidimensional and contains more than <see cref="F:System.Int32.MaxValue" /> elements.</exception>
		/// <exception cref="BlueRainWriteException">WriteProcessMemory failed.</exception>
		/// <exception cref="ArgumentException"><paramref name="structure" /> is a reference type that is not a formatted class. </exception>
		public override unsafe void Write<T>(IntPtr address, T value, bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");

			// TODO: Optimize this method to take marshalling requirements into account

			var size = MarshalCache<T>.Size;
			var buffer = new byte[size];

			fixed (byte* b = buffer)
				Marshal.StructureToPtr(value, (IntPtr)b, true);

			WriteBytes(address, buffer, isRelative);
		}

		/// <summary>
		/// Writes the specified value at the specified address. This method is used if multiple-pointer dereferences are
		/// required.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <param name="value">The value.</param>
		/// <param name="addresses">The addresses.</param>
		/// <returns></returns>
		/// <exception cref="MissingMethodException">The class specified by <paramref name="T" /> does not have an accessible default constructor. </exception>
		/// <exception cref="BlueRainReadException">Thrown if the ReadProcessMemory operation fails, or doesn't return the specified amount of bytes.</exception>
		/// <exception cref="BlueRainWriteException">WriteProcessMemory failed.</exception>
		/// <exception cref="OverflowException">The array is multidimensional and contains more than <see cref="F:System.Int32.MaxValue" /> elements.</exception>
		public override void Write<T>(bool isRelative, T value = default(T), params IntPtr[] addresses)
		{ 
			Requires.Condition(() => addresses.Length > 0, "addresses");
			Requires.NotEqual(addresses[0], IntPtr.Zero, "addresses");

			// If a single addr is passed, just write it right away.
			if (addresses.Length == 1)
			{
				Write(addresses[0], value, isRelative);
				return;
			}

			// Same thing as sequential reads - we read until we find the last addr, then we write to it.
			IntPtr tempPtr = Read<IntPtr>(addresses[0]);

			for (int i = 1; i < addresses.Length - 1; i++)
				tempPtr = Read<IntPtr>(tempPtr + addresses[i].ToInt32());

			Write(tempPtr + addresses.Last().ToInt32(), value, isRelative);
		}

		#endregion
	}
}
