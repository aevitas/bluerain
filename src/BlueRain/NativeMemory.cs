// Copyright (C) 2013-2015 aevitas
// See the file COPYING for copying permission.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
		protected NativeMemory(Process process)
		{
			Requires.NotNull(process, "process");

			Process = process;

			Process.EnableRaisingEvents = true;
			Process.Exited += async (sender, args) =>
			{
				// Just pass the exit code and the EventArgs to the handler.
				await OnExited(Process.ExitCode, args);
			};
		}

		/// <summary>
		///     Gets or sets the base address of the wrapped process' main module.
		/// </summary>
		protected IntPtr BaseAddress
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
		///     Called when the process this Memory instance is attach to exits.
		/// </summary>
		/// <param name="exitCode">The exit code.</param>
		/// <param name="eventArgs">The <see cref="EventArgs" /> instance containing the event data.</param>
		/// <returns></returns>
		protected virtual async Task OnExited(int exitCode, EventArgs eventArgs)
		{
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
			Requires.NotEqual(absoluteAddress, IntPtr.Zero, "absoluteAddress");

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
		///		A fallback occurred (see Character Encoding in the .NET Framework for
		///		complete explanation)-and-<see cref="P:System.Text.Encoding.DecoderFallback" /> is set to 
		///		<see cref="T:System.Text.DecoderExceptionFallback" />.
		/// </exception>
		public virtual async Task<string> ReadString(IntPtr address, Encoding encoding, int maximumLength = 512,
			bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");
			Requires.NotNull(encoding, "encoding");

			var buffer = await ReadBytes(address, maximumLength, isRelative);
			var ret = encoding.GetString(buffer);
			if (ret.IndexOf('\0') != -1)
			{
				ret = ret.Remove(ret.IndexOf('\0'));
			}
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
		public virtual async Task WriteString(IntPtr address, string value, Encoding encoding, bool isRelative = false)
		{
			Requires.NotEqual(address, IntPtr.Zero, "address");
			Requires.NotNull(encoding, "encoding");

			if (value[value.Length - 1] != '\0')
			{
				value += '\0';
			}

			await WriteBytes(address, encoding.GetBytes(value), isRelative);
		}

		#region Memory Reading / Writing Methods

		/// <summary>
		///     Reads the specified amount of bytes from the specified address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public abstract Task<byte[]> ReadBytes(IntPtr address, int count, bool isRelative = false);

		/// <summary>
		///     Writes the specified bytes at the specified address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="bytes">The bytes.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public abstract Task WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false);

		/// <summary>
		///     Reads a value of the specified type at the specified address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public abstract Task<T> Read<T>(IntPtr address, bool isRelative = false) where T : struct;

		/// <summary>
		///     Reads the specified amount of values of the specified type at the specified address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public abstract Task<T[]> Read<T>(IntPtr address, int count, bool isRelative = false) where T : struct;

		/// <summary>
		///     Reads a value of the specified type at the specified address. This method is used if multiple-pointer dereferences
		///     are required.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <param name="addresses">The addresses.</param>
		/// <returns></returns>
		public abstract Task<T> Read<T>(bool isRelative = false, params IntPtr[] addresses);

		/// <summary>
		///     Writes the specified value at the specfied address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="value">The value.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public abstract Task Write<T>(IntPtr address, T value, bool isRelative = false) where T : struct;

		/// <summary>
		///     Writes the specified value at the specified address. This method is used if multiple-pointer dereferences are
		///     required.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <param name="value">The value.</param>
		/// <param name="addresses">The addresses.</param>
		/// <returns></returns>
		public abstract Task Write<T>(bool isRelative, T value = default(T), params IntPtr[] addresses) where T : struct;

		#endregion

		#region P/Invokes

		[DllImport("kernel32.dll")]
		protected static extern uint SuspendThread(SafeMemoryHandle hThread);

		[DllImport("kernel32.dll")]
		protected static extern SafeMemoryHandle OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

		#endregion

		#region Implementation of IDisposable

		public virtual void Dispose()
		{
			// Pretty much all we "have" to clean up.
			Process.LeaveDebugMode();
		}

		#endregion
	}
}