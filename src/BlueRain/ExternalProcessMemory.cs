// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BlueRain.Common;

namespace BlueRain
{
    /// <summary>
    ///     Provides functionality for reading from, writing to, and allocating memory in external processes.
    ///     This class relies on a handle to the remote process and to its main thread to perform memory reading and writing.
    /// </summary>
    public sealed class ExternalProcessMemory : NativeMemory
    {
        private readonly SafeMemoryHandle _mainThreadHandle;
        internal readonly SafeMemoryHandle ProcessHandle;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ExternalProcessMemory" /> class.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="access">The access flags.</param>
        /// <param name="createInjector">if set to <c>true</c> creates an injector for module loading support.</param>
        /// <exception cref="PlatformNotSupportedException">
        ///     The platform is Windows 98 or Windows Millennium Edition (Windows Me);
        ///     set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this
        ///     property on Windows 98 and Windows Me.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     The process's <see cref="P:System.Diagnostics.Process.Id" /> property has
        ///     not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object.
        /// </exception>
        public ExternalProcessMemory(Process process,
            ProcessAccess access, bool createInjector = false) : base(process, createInjector)
        {
            ProcessHandle = OpenProcess(access, false, process.Id);

            // Obtain a handle to the process' main thread so we can suspend/resume it whenever we need to.0
            _mainThreadHandle = OpenThread(ThreadAccess.ALL, false, (uint)process.Threads[0].Id);

            Process.EnterDebugMode();
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ExternalProcessMemory" /> class.
        ///     This constructor opens a handle to the specified process using the following flags:
        ///     ProcessAccess.CreateThread | ProcessAccess.QueryInformation | ProcessAccess.VMRead | ProcessAccess.VMWrite |
        ///     ProcessAccess.VMOperation | ProcessAccess.SetInformation
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="createInjector">if set to <c>true</c> creates an injector for module loading support.</param>
        /// <exception cref="PlatformNotSupportedException">
        ///     The platform is Windows 98 or Windows Millennium Edition (Windows Me);
        ///     set the <see cref="P:System.Diagnostics.ProcessStartInfo.UseShellExecute" /> property to false to access this
        ///     property on Windows 98 and Windows Me.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     The process's <see cref="P:System.Diagnostics.Process.Id" /> property has
        ///     not been set.-or- There is no process associated with this <see cref="T:System.Diagnostics.Process" /> object.
        /// </exception>
        public ExternalProcessMemory(Process process, bool createInjector = false) : this(process,
            ProcessAccess.CreateThread | ProcessAccess.QueryInformation | ProcessAccess.VMRead |
            ProcessAccess.VMWrite | ProcessAccess.VMOperation | ProcessAccess.SetInformation, createInjector)
        {
        }

        // VirtualAllocEx specifies size as a size_t - we'll use the .NET equivalent.
        private IntPtr AllocateMemory(UIntPtr size)
        {
            return VirtualAllocEx(ProcessHandle, IntPtr.Zero, size, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
        }

        /// <summary>
        ///     Allocates a chunk of memory of the specified size in the remote process.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="BlueRainException"></exception>
        public override AllocatedMemory Allocate(UIntPtr size)
        {
            var chunk = AllocateMemory(size);

            if (chunk != IntPtr.Zero)
            {
                return new AllocatedMemory(chunk, size.ToUInt32(), this);
            }

            throw new BlueRainException($"Couldn't allocate {size.ToUInt32()} sized chunk!");
        }

        #region Implementation of IDisposable

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            if (IsDisposed)
                return;

            ProcessHandle?.Dispose();
            _mainThreadHandle?.Dispose();

            Process.LeaveDebugMode();

            base.Dispose();
        }

        #endregion

        #region P/Invokes

        [DllImport("kernel32", EntryPoint = "OpenThread", SetLastError = true)]
        public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern SafeMemoryHandle OpenProcess(
            ProcessAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool ReadProcessMemory(
            SafeMemoryHandle hProcess, IntPtr lpBaseAddress, byte* lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(
            SafeMemoryHandle hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtectEx(
            SafeMemoryHandle hProcess, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(SafeMemoryHandle hProcess, IntPtr lpAddress,
            UIntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        #endregion

        #region Overrides of NativeMemory

        /// <summary>
        ///     Reads the specified amount of bytes from the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="count">The count.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
        /// <exception cref="MemoryReadException">
        ///     Thrown if the ReadProcessMemory operation fails, or doesn't return the
        ///     specified amount of bytes.
        /// </exception>
        public override unsafe byte[] ReadBytes(IntPtr address, int count, bool isRelative = false)
        {
            Requires.NotEqual(count, 0, nameof(count));
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));

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

            throw new MemoryReadException(address, count);
        }

        /// <summary>
        ///     Writes the specified bytes at the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="MemoryWriteException"></exception>
        /// <exception cref="OverflowException">
        ///     The array is multidimensional and contains more than
        ///     <see cref="F:System.Int32.MaxValue" /> elements.
        /// </exception>
        public override void WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false)
        {
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));
            Requires.NotEqual(bytes.Length, 0, nameof(bytes));

            uint oldProtect;
            int numWritten;

            // dwSize is a size_t, meaning it *may* differ depending architecture. Though very unlikely to cause trouble if 
            // defined as uint, we're passing it as IntPtr here as it is the closest .NET equivalent.
            VirtualProtectEx(ProcessHandle, address, (IntPtr)bytes.Length, (uint)MemoryProtection.ExecuteReadWrite,
                out oldProtect);
            WriteProcessMemory(ProcessHandle, address, bytes, bytes.Length, out numWritten);
            VirtualProtectEx(ProcessHandle, address, (IntPtr)bytes.Length, oldProtect, out oldProtect);

            // All we need to check - if WriteProcessMemory fails numWriten will be 0.
            var success = numWritten == bytes.Length;
            if (!success)
                throw new MemoryWriteException(address, bytes.Length);
        }

        /// <summary>
        ///     Reads a value of the specified type at the specified address.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
        /// <exception cref="MemoryReadException">
        ///     Thrown if the ReadProcessMemory operation fails, or doesn't return the
        ///     specified amount of bytes.
        /// </exception>
        public override unsafe T Read<T>(IntPtr address, bool isRelative = false)
        {
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));

            // We can bypass the marshal completely, unless the type we're reading has marshalling
            // directives such as a [MarshalAs] attribute.
            bool requiresMarshal = MarshalCache<T>.TypeRequiresMarshal;
            var size = requiresMarshal ? MarshalCache<T>.Size : Unsafe.SizeOf<T>();
            
            var buffer = ReadBytes(address, size, isRelative);
            fixed (byte* b = buffer)
            {
                if (requiresMarshal)
                    return Marshal.PtrToStructure<T>(new IntPtr(b));

                return Unsafe.Read<T>(b);
            }
        }

        /// <summary>
        ///     Reads the specified amount of values of the specified type at the specified address.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="count">The count.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
        /// <exception cref="MemoryReadException">
        ///     Thrown if the ReadProcessMemory operation fails, or doesn't return the
        ///     specified amount of bytes.
        /// </exception>
        public override T[] Read<T>(IntPtr address, int count, bool isRelative = false)
        {
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));

            var size = MarshalCache<T>.Size;

            var ret = new T[count];

            // Read = add + n * size
            for (var i = 0; i < count; i++)
                ret[i] = Read<T>(address + (i * size), isRelative);

            return ret;
        }

        /// <summary>
        ///     Writes the specified value at the specfied address.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="OverflowException">
        ///     The array is multidimensional and contains more than
        ///     <see cref="F:System.Int32.MaxValue" /> elements.
        /// </exception>
        /// <exception cref="MemoryWriteException">WriteProcessMemory failed.</exception>
        /// <exception cref="ArgumentException"><paramref name="T" /> is a reference type that is not a formatted class. </exception>
        public override unsafe void Write<T>(IntPtr address, T value, bool isRelative = false)
        {
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));
            
            bool requiresMarshal = MarshalCache<T>.TypeRequiresMarshal;

            var size = requiresMarshal ? MarshalCache<T>.Size : Unsafe.SizeOf<T>();
            var buffer = new byte[size];

            fixed (byte* b = buffer)
            {
                if (requiresMarshal)
                    Marshal.StructureToPtr(value, (IntPtr)b, true);
                else
                {
                    Unsafe.Write(b, value);
                }
            }
            
            WriteBytes(address, buffer, isRelative);
        }

        #endregion
    }
}