// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BlueRain
{
	/// <summary>
	///     Represents a chunk of memory allocated by an ExternalProcessMemory instance.
	/// </summary>
	public class AllocatedMemory : IDisposable
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="AllocatedMemory" /> class.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="size">The size.</param>
		/// <param name="memory">The memory.</param>
		public AllocatedMemory(IntPtr address, uint size, NativeMemory memory)
		{
			Address = address;
			Size = size;
			Memory = memory;
			IsAllocated = true;
		}

		/// <summary>
		///     Gets a value indicating whether this chunk is allocated.
		/// </summary>
		public bool IsAllocated { get; private set; }

		/// <summary>
		///     Gets the memory instance that allocated this chunk of memory.
		/// </summary>
		public NativeMemory Memory { get; }

		/// <summary>
		///     Gets the address returned by VirtualAllocEx when this chunk of memory was allocated.
		/// </summary>
		public IntPtr Address { get; }

		/// <summary>
		///     Gets the size of this allocated chunk.
		/// </summary>
		public uint Size { get; }

		#region Implementation of IDisposable

		/// <summary>
		///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			var epm = Memory as ExternalProcessMemory;

			if (epm != null)
				VirtualFreeEx(epm.ProcessHandle, Address, (UIntPtr) Size, FreeType.Release);

			var lpm = Memory as LocalProcessMemory;
			if (lpm != null)
				Marshal.FreeHGlobal(Address);

			IsAllocated = false;
		}

		#endregion

		#region P/Invokes

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		private static extern bool VirtualFreeEx(SafeMemoryHandle hProcess, IntPtr lpAddress,
			UIntPtr dwSize, FreeType dwFreeType);

		#endregion

		#region Reading / Writing Methods

		/// <summary>
		///     Reads a value of the specified type at the specified offset, relative to the address of the allocated chunk.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="offset">The offset.</param>
		/// <returns></returns>
		public T Read<T>(IntPtr offset) where T : struct
		{
			return Memory.Read<T>(Address + offset.ToInt32());
		}

		/// <summary>
		///     Reads the specified amount of bytes from the allocated chunk, starting at the specified offset.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="count">The count.</param>
		/// <returns></returns>
		public byte[] ReadBytes(IntPtr offset, int count)
		{
			return Memory.ReadBytes(Address + offset.ToInt32(), count);
		}

		/// <summary>
		///     Writes the specified value at the specified offset, relative to the address of the allocated chunk.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="offset">The offset.</param>
		/// <param name="value">The value.</param>
		public void Write<T>(IntPtr offset, T value) where T : struct
		{
			Memory.Write(Address + offset.ToInt32(), value);
		}

		/// <summary>
		///     Reads a string from the allocated chunk, starting at the specified offset and using the specified encoding.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="encoding">The encoding.</param>
		/// <returns></returns>
		public string ReadString(IntPtr offset, Encoding encoding)
		{
			return Memory.ReadString(Address + offset.ToInt32(), encoding);
		}

		/// <summary>
		///     Writes the specified null-terminated string to the allocated chunk, starting at the specified offset, using the
		///     specified encoding.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="value">The value.</param>
		/// <param name="encoding">The encoding.</param>
		public void WriteString(IntPtr offset, string value, Encoding encoding)
		{
			Memory.WriteString(Address + offset.ToInt32(), value, encoding);
		}

		/// <summary>
		///     Writes the specified bytes to the allocated chunk, starting at the specified offset.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="value">The value.</param>
		public void WriteBytes(IntPtr offset, byte[] value)
		{
			Memory.WriteBytes(Address + offset.ToInt32(), value);
		}

		#endregion
	}
}