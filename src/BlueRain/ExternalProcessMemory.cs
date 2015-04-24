using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BlueRain.Common;

namespace BlueRain
{
	/// <summary>
	/// Provides functionality for reading from, writing to, and allocating memory in external processes.
	/// This class relies on a handle to the remote process and to its main thread to perform memory reading and writing.
	/// </summary>
	public class ExternalProcessMemory : NativeMemory
	{
		private SafeMemoryHandle _processHandle;
		private SafeMemoryHandle _mainThreadHandle;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExternalProcessMemory"/> class.
		/// </summary>
		/// <param name="process">The process.</param>
		/// <param name="accessFlags">The access flags.</param>
		/// <exception cref="PlatformNotSupportedException">The platform is Windows 98 or Windows Millennium Edition (Windows Me); set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this property on Windows 98 and Windows Me.</exception>
		/// <exception cref="InvalidOperationException">The process's <see cref="P:System.Diagnostics.Process.Id" /> property has not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object. </exception>
		public ExternalProcessMemory(Process process,
			ProcessAccessFlags accessFlags) : base(process)
		{
			_processHandle = OpenProcess(accessFlags, false, process.Id);

			// Obtain a handle to the process' main thread so we can suspend/resume it whenever we need to.0
			_mainThreadHandle = OpenThread(ThreadAccess.ALL, false, (uint) process.Threads[0].Id);

			// TODO: Obtain SeDebugPrivilege here so we can CreateRemoteThread for lib injection!
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ExternalProcessMemory"/> class.
		/// This constructor opens a handle to the specified process using the following flags:
		/// ProcessAccessFlags.CreateThread | ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VMRead | ProcessAccessFlags.VMWrite | ProcessAccessFlags.VMOperation | ProcessAccessFlags.SetInformation
		/// </summary>
		/// <param name="process">The process.</param>
		/// <exception cref="PlatformNotSupportedException">The platform is Windows 98 or Windows Millennium Edition (Windows Me); set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this property on Windows 98 and Windows Me.</exception>
		/// <exception cref="InvalidOperationException">The process's <see cref="P:System.Diagnostics.Process.Id" /> property has not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object. </exception>
		public ExternalProcessMemory(Process process) : this (process,
				ProcessAccessFlags.CreateThread | ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VMRead |
				ProcessAccessFlags.VMWrite | ProcessAccessFlags.VMOperation | ProcessAccessFlags.SetInformation)
		{
		}

		public AllocatedMemory Allocate()
		{
			return new AllocatedMemory();
		}

		#region P/Invokes

		[DllImport("kernel32", EntryPoint = "OpenThread", SetLastError = true)]
		public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

		[DllImport("kernel32.dll")]
		private static extern SafeMemoryHandle OpenProcess(
			ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static unsafe extern bool ReadProcessMemory(
			SafeMemoryHandle hProcess, IntPtr lpBaseAddress, byte* lpBuffer, int dwSize, out int lpNumberOfBytesRead);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool WriteProcessMemory(
			SafeMemoryHandle hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

		[DllImport("kernel32.dll")]
		private static extern bool VirtualProtectEx(
			SafeMemoryHandle hProcess, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

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
		public override unsafe async Task<byte[]> ReadBytes(IntPtr address, int count, bool isRelative = false)
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
				if (ReadProcessMemory(_processHandle, address, b, count, out numRead) && numRead == count)
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
		public override async Task WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");
			Requires.NotEqual(bytes.Length, 0, "bytes");

			uint oldProtect;
			int numWritten;

			// dwSize is a size_t, meaning it *may* differ depending architecture. Though very unlikely to cause trouble if 
			// defined as uint, we're passing it as IntPtr here as it is the closest .NET equivalent.
			VirtualProtectEx(_processHandle, address, (IntPtr) bytes.Length, (uint) ProtectionFlags.PageExecuteReadWrite,
				out oldProtect);
			WriteProcessMemory(_processHandle, address, bytes, bytes.Length, out numWritten);
			VirtualProtectEx(_processHandle, address, (IntPtr) bytes.Length, oldProtect, out oldProtect);

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
		public override unsafe async Task<T> Read<T>(IntPtr address, bool isRelative = false)

		{
			var size = Marshal.SizeOf(typeof (T));

			// Unsafe context doesn't allow for the use of await - run the read synchronously for now.
			var buffer = ReadBytes(address, size, isRelative).Result;

			fixed (byte* b = buffer)
				return Marshal.PtrToStructure<T>((IntPtr) b);
		}

		public override Task<T[]> Read<T>(IntPtr address, int count, bool isRelative = false)
		{
			throw new NotImplementedException();
		}

		public override Task<T> Read<T>(bool isRelative = false, params IntPtr[] addresses)
		{
			throw new NotImplementedException();
		}

		public override Task Write<T>(IntPtr address, T value, bool isRelative = false)
		{
			throw new NotImplementedException();
		}

		public override Task Write<T>(bool isRelative, T value = default(T), params IntPtr[] addresses)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
