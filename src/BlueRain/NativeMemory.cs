// Copyright (C) 2013-2017 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using BlueRain.Common;

namespace BlueRain
{
    /// <summary>
    ///     Base class for all internal and external process manipulation types.
    /// </summary>
    public abstract class NativeMemory : IDisposable
    {
        // We store the base address both as an IntPtr and a regular int. The IntPtr is the "API" member,
        // the int is just for internal use - allowing for faster pointer arithmetics.
        private IntPtr _baseAddress;
        private int _fastBaseAddress;

        /// <summary>
        ///     Initializes a new instance of the <see cref="NativeMemory" /> class.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="access">The access.</param>
        /// <param name="injectorOptions">The injector options.</param>
        protected NativeMemory(Process process, ProcessAccess access, InjectorCreationOptions injectorOptions)
        {
            Requires.NotNull(process, nameof(process));

            Process = process;

            Process.EnableRaisingEvents = true;
            Process.Exited += async (sender, args) =>
            {
                // Just pass the exit code and the EventArgs to the handler.
                await OnExited(Process.ExitCode, args);
            };


            if (injectorOptions.CreateInjector)
            {
                if (IsExternal)
                    ProcessHandle = OpenProcess(access, false, process.Id);

                Injector = new Injector(this);
            }

            PatternScanner = new PatternScanner(this);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NativeMemory" /> class.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="injectorOptions">The injector options.</param>
        protected NativeMemory(Process process, InjectorCreationOptions injectorOptions)
            : this(process, DefaultProcessAccess, injectorOptions)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="NativeMemory" /> class.
        /// </summary>
        /// <param name="process">The process.</param>
        protected NativeMemory(Process process) : this(process, InjectorCreationOptions.Default)
        {
        }

        /// <summary>
        ///     Gets or sets the process handle. Only required for external memory manipulation. For local process memory
        ///     instances, this will always be null.
        /// </summary>
        internal SafeMemoryHandle ProcessHandle { get; set; }

        protected static ProcessAccess DefaultProcessAccess { get; } = ProcessAccess.CreateThread |
                                                                       ProcessAccess.QueryInformation |
                                                                       ProcessAccess.VMRead |
                                                                       ProcessAccess.VMWrite | ProcessAccess.VMOperation |
                                                                       ProcessAccess.SetInformation;

        /// <summary>
        ///     Gets the module injector/loader for this memory instance.
        /// </summary>
        public Injector Injector { get; }

        /// <summary>
        ///     Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed { get; private set; }

        private bool IsExternal => Process != null && Process != Process.GetCurrentProcess();

        /// <summary>
        ///     Gets or sets the base address of the wrapped process' main module.
        /// </summary>
        public IntPtr BaseAddress
        {
            [Pure] get { return _baseAddress; }
            set
            {
                _baseAddress = value;
                _fastBaseAddress = value.ToInt32();
            }
        }

        /// <summary>
        ///     Gets or sets the process this NativeMemory instance is wrapped around.
        /// </summary>
        public Process Process { [Pure] get; protected set; }

        /// <summary>
        ///     Gets the pattern scanner associated with this NativeMemory instance.
        /// </summary>
        public PatternScanner PatternScanner { get; }

        #region Implementation of IDisposable

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            if (IsDisposed)
                return;

            Injector?.Dispose();

            // Pretty much all we "have" to clean up.
            Process.LeaveDebugMode();

            IsDisposed = true;
        }

        #endregion

        /// <summary>
        ///     Allocates a chunk of memory of the specified size in the process.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="BlueRainException"></exception>
        public abstract AllocatedMemory Allocate(UIntPtr size);

        /// <summary>
        ///     Allocates a chunk of memory of the specified size in the process.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        public virtual AllocatedMemory Allocate(int size)
        {
            return Allocate((UIntPtr) size);
        }

        /// <summary>
        ///     Allocates a chunk of memory of the specified size in the process.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        public virtual AllocatedMemory Allocate(IntPtr size)
        {
            return Allocate((UIntPtr) (int) size);
        }

        /// <summary>
        ///     Called when the process this Memory instance is attach to exits.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        /// <param name="eventArgs">The <see cref="EventArgs" /> instance containing the event data.</param>
        /// <returns></returns>
        protected virtual async Task OnExited(int exitCode, EventArgs eventArgs)
        {
        }

        /// <summary>
        ///     Gets the module with the specified name from the process.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns></returns>
        public ProcessModule GetModule(string moduleName)
        {
            Requires.Condition(() => !string.IsNullOrEmpty(moduleName), nameof(moduleName));
            Requires.NotNull(Process, nameof(Process));

            var modules = Process.Modules.Cast<ProcessModule>();

            return modules.FirstOrDefault(s => s.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Converts the specified absolute address to a relative address.
        /// </summary>
        /// <param name="absoluteAddress">The absolute address.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">absoluteAddress may not be IntPtr.Zero.</exception>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr ToRelative(IntPtr absoluteAddress)
        {
            Requires.NotEqual(absoluteAddress, IntPtr.Zero, nameof(absoluteAddress));

            return absoluteAddress - _fastBaseAddress;
        }

        /// <summary>
        ///     Converts the specified relative address to an absolute address.
        /// </summary>
        /// <param name="relativeAddress">The relative address.</param>
        /// <returns></returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr ToAbsolute(IntPtr relativeAddress)
        {
            // In this case, we allow IntPtr zero - relative + base = base, so no harm done.
            return relativeAddress + _fastBaseAddress;
        }

        // We can declare string reading and writing right here - they won't differ based on whether we're internal or external.
        // The actual memory read/write methods depend on whether or not we're injected - we'll have to resort to RPM/WPM for external,
        // while injected libraries can simply use Marshal.Copy back and forth.
        // Theoretically these methods could be marked as [Pure] as they do not modify the state of the object itself, but rather
        // of the process they are currently "manipulating".

        /// <summary>
        ///     Reads a string with the specified encoding at the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="encoding">The encoding.</param>
        /// <param name="maximumLength">The maximum length.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="startIndex" /> is less than zero.-or-
        ///     <paramref name="startIndex" /> specifies a position that is not within this string.
        /// </exception>
        /// <exception cref="ArgumentNullException">Encoding may not be null.</exception>
        /// <exception cref="ArgumentException">Address may not be IntPtr.Zero.</exception>
        /// <exception cref="DecoderFallbackException">
        ///     A fallback occurred (see Character Encoding in the .NET Framework for
        ///     complete explanation)-and-<see cref="P:System.Text.Encoding.DecoderFallback" /> is set to
        ///     <see cref="T:System.Text.DecoderExceptionFallback" />.
        /// </exception>
        public virtual string ReadString(IntPtr address, Encoding encoding, int maximumLength = 512,
            bool isRelative = false)
        {
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));
            Requires.NotNull(encoding, nameof(encoding));

            var buffer = ReadBytes(address, maximumLength, isRelative);
            var ret = encoding.GetString(buffer);
            if (ret.IndexOf('\0') != -1)
                ret = ret.Remove(ret.IndexOf('\0'));
            return ret;
        }

        /// <summary>
        ///     Writes the specified string at the specified address using the specified encoding.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        /// <param name="encoding">The encoding.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Address may not be IntPtr.Zero.</exception>
        /// <exception cref="ArgumentNullException">Encoding may not be null.</exception>
        /// <exception cref="IndexOutOfRangeException">
        ///     <paramref name="index" /> is greater than or equal to the length of this
        ///     object or less than zero.
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        ///     A fallback occurred (see Character Encoding in the .NET Framework for
        ///     complete explanation)-and-<see cref="P:System.Text.Encoding.EncoderFallback" /> is set to
        ///     <see cref="T:System.Text.EncoderExceptionFallback" />.
        /// </exception>
        public virtual void WriteString(IntPtr address, string value, Encoding encoding, bool isRelative = false)
        {
            Requires.NotEqual(address, IntPtr.Zero, nameof(address));
            Requires.NotNull(encoding, nameof(encoding));

            if (value[value.Length - 1] != '\0')
                value += '\0';

            WriteBytes(address, encoding.GetBytes(value), isRelative);
        }

        /// <summary>
        ///     Reads a value of the specified type at the specified address. This method is used if multiple-pointer dereferences
        ///     are required.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <param name="addresses">The addresses.</param>
        /// <returns></returns>
        /// <exception cref="MemoryReadException">
        ///     Thrown if the ReadProcessMemory operation fails, or doesn't return the
        ///     specified amount of bytes.
        /// </exception>
        /// <exception cref="MissingMethodException">
        ///     The class specified by <paramref name="T" /> does not have an accessible
        ///     default constructor.
        /// </exception>
        /// <exception cref="ArgumentException">Address may not be zero, and count may not be zero.</exception>
        /// <exception cref="OverflowException">
        ///     On a 64-bit platform, the value of this instance is too large or too small to
        ///     represent as a 32-bit signed integer.
        /// </exception>
        public virtual T Read<T>(bool isRelative = false, params IntPtr[] addresses) where T : struct
        {
            Requires.Condition(() => addresses.Length > 0, nameof(addresses));
            Requires.NotEqual(addresses[0], IntPtr.Zero, nameof(addresses));

            // We can just read right away if it's a single address - avoid the hassle.
            if (addresses.Length == 1)
                return Read<T>(addresses[0], isRelative);

            var tempPtr = Read<IntPtr>(addresses[0], isRelative);

            for (var i = 1; i < addresses.Length - 1; i++)
                tempPtr = Read<IntPtr>(tempPtr + addresses[i].ToInt32(), isRelative);

            return Read<T>(tempPtr, isRelative);
        }

        /// <summary>
        ///     Writes the specified value at the specified address. This method is used if multiple-pointer dereferences are
        ///     required.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <param name="value">The value.</param>
        /// <param name="addresses">The addresses.</param>
        /// <returns></returns>
        /// <exception cref="MissingMethodException">
        ///     The class specified by <paramref name="T" /> does not have an accessible
        ///     default constructor.
        /// </exception>
        /// <exception cref="MemoryReadException">
        ///     Thrown if the ReadProcessMemory operation fails, or doesn't return the
        ///     specified amount of bytes.
        /// </exception>
        /// <exception cref="MemoryWriteException">WriteProcessMemory failed.</exception>
        /// <exception cref="OverflowException">
        ///     The array is multidimensional and contains more than
        ///     <see cref="F:System.Int32.MaxValue" /> elements.
        /// </exception>
        public virtual void Write<T>(bool isRelative, T value = default(T), params IntPtr[] addresses) where T : struct
        {
            Requires.Condition(() => addresses.Length > 0, nameof(addresses));
            Requires.NotEqual(addresses[0], IntPtr.Zero, nameof(addresses));

            // If a single addr is passed, just write it right away.
            if (addresses.Length == 1)
            {
                Write(addresses[0], value, isRelative);
                return;
            }

            // Same thing as sequential reads - we read until we find the last addr, then we write to it.
            var tempPtr = Read<IntPtr>(addresses[0]);

            for (var i = 1; i < addresses.Length - 1; i++)
                tempPtr = Read<IntPtr>(tempPtr + addresses[i].ToInt32());

            Write(tempPtr + addresses.Last().ToInt32(), value, isRelative);
        }

        #region Memory Reading / Writing Methods

        /// <summary>
        ///     Reads the specified amount of bytes from the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="count">The count.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public abstract byte[] ReadBytes(IntPtr address, int count, bool isRelative = false);

        /// <summary>
        ///     Writes the specified bytes at the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public abstract void WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false);

        /// <summary>
        ///     Reads a value of the specified type at the specified address.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public abstract T Read<T>(IntPtr address, bool isRelative = false) where T : struct;

        /// <summary>
        ///     Reads the specified amount of values of the specified type at the specified address.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="count">The count.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public abstract T[] Read<T>(IntPtr address, int count, bool isRelative = false) where T : struct;

        /// <summary>
        ///     Writes the specified value at the specfied address.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="value">The value.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public abstract void Write<T>(IntPtr address, T value, bool isRelative = false) where T : struct;

        #endregion

        #region P/Invokes

        [DllImport("kernel32.dll")]
        protected static extern uint SuspendThread(SafeMemoryHandle hThread);

        [DllImport("kernel32.dll")]
        protected static extern SafeMemoryHandle OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle,
            uint dwThreadId);

        [DllImport("kernel32.dll")]
        protected static extern SafeMemoryHandle OpenProcess(
            ProcessAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        #endregion
    }
}