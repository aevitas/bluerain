// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BlueRain
{
	/// <summary>
	/// Represents a chunk of memory allocated by an ExternalProcessMemory instance.
	/// </summary>
	public class AllocatedMemory : IDisposable
	{
		/// <summary>
		/// Gets a value indicating whether this chunk is allocated.
		/// </summary>
		public bool IsAllocated { get; private set; }

		/// <summary>
		/// Gets the memory instance that allocated this chunk of memory.
		/// </summary>
		public ExternalProcessMemory Memory { get; private set; }

		/// <summary>
		/// Gets the address returned by VirtualAllocEx when this chunk of memory was allocated.
		/// </summary>
		public IntPtr Address { get; private set; }

		/// <summary>
		/// Gets the size of this allocated chunk.
		/// </summary>
		public uint Size { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AllocatedMemory"/> class.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="size">The size.</param>
		/// <param name="memory">The memory.</param>
		public AllocatedMemory(IntPtr address, uint size, ExternalProcessMemory memory)
		{
			Address = address;
			Size = size;
			Memory = memory;
			IsAllocated = true;
		}

		#region Reading / Writing Methods

		/// <summary>
		/// Reads a value of the specified type at the specified offset, relative to the address of the allocated chunk.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="offset">The offset.</param>
		/// <returns></returns>
		public T Read<T>(IntPtr offset) where T : struct
		{
			return Memory.Read<T>(Address + offset.ToInt32());
		}

		/// <summary>
		/// Writes the specified value at the specified offset, relative to the address of the allocated chunk.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="offset">The offset.</param>
		/// <param name="value">The value.</param>
		public void Write<T>(IntPtr offset, T value) where T : struct
		{
			Memory.Write(Address + offset.ToInt32(), value);
		}

		/// <summary>
		/// Reads a string from the allocated chunk, starting at the specified offset and using the specified encoding.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="encoding">The encoding.</param>
		/// <returns></returns>
		public string ReadString(IntPtr offset, Encoding encoding)
		{
			return Memory.ReadString(Address + offset.ToInt32(), encoding);
		}

		/// <summary>
		/// Writes the specified null-terminated string to the allocated chunk, starting at the specified offset, using the specified encoding.
		/// </summary>
		/// <param name="offset">The offset.</param>
		/// <param name="value">The value.</param>
		/// <param name="encoding">The encoding.</param>
		public void WriteString(IntPtr offset, string value, Encoding encoding)
		{
			Memory.WriteString(Address + offset.ToInt32(), value, encoding);
		}

		#endregion

		#region Implementation of IDisposable

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <exception cref="NotImplementedException"></exception>
		public void Dispose()
		{
			VirtualFreeEx(Memory.ProcessHandle, Address, (UIntPtr) Size, FreeType.Release);
			IsAllocated = false;
		}

		#endregion

		#region P/Invokes

		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		private static extern bool VirtualFreeEx(SafeMemoryHandle hProcess, IntPtr lpAddress,
			UIntPtr dwSize, FreeType dwFreeType);

		#endregion
	}
}