// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlueRain.Common;

namespace BlueRain
{
	/// <summary>
	///     Provides memory manipulation functionality for local processes; ie. injected.
	/// </summary>
	public sealed unsafe class LocalProcessMemory : NativeMemory
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="LocalProcessMemory" /> class.
		/// </summary>
		/// <param name="process">The process.</param>
		/// <param name="createInjector">if set to <c>true</c> creates an injector for module loading support.</param>
		public LocalProcessMemory(Process process, bool createInjector = false)
			: base(process, createInjector)
		{
		}

		public override AllocatedMemory Allocate(UIntPtr size)
		{
			var alloc = Marshal.AllocHGlobal((int) size);
			return new AllocatedMemory(alloc, (uint) size, this);
		}

		/// <summary>
		///     Reads the specified amount of bytes from the specified address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public override byte[] ReadBytes(IntPtr address, int count, bool isRelative = false)
		{
			if (isRelative)
				address = ToAbsolute(address);

			var buffer = new byte[count];
			var ptr = (byte*) address;

			for (var i = 0; i < count; i++)
				buffer[i] = ptr[i];

			return buffer;
		}

		/// <summary>
		///     Writes the specified bytes at the specified address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="bytes">The bytes.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		public override void WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false)
		{
			if (isRelative)
				address = ToAbsolute(address);

			var ptr = (byte*) address;
			for (var i = 0; i < bytes.Length; i++)
				ptr[i] = bytes[i];
		}

		/// <summary>
		///     Reads a value of the specified type at the specified address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public override T Read<T>(IntPtr address, bool isRelative = false)
		{
			if (isRelative)
				address = ToAbsolute(address);

			return ReadInternal<T>(address);
		}

		/// <summary>
		///     Reads the specified amount of values of the specified type at the specified address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		/// <returns></returns>
		public override T[] Read<T>(IntPtr address, int count, bool isRelative = false)
		{
			if (isRelative)
				address = ToAbsolute(address);

			var ret = new T[count];
			for (var i = 0; i < count; i++)
				ret[i] = Read<T>(address + (MarshalCache<T>.Size*i), isRelative);

			return ret;
		}

		/// <summary>
		///     Writes the specified value at the specfied address.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="address">The address.</param>
		/// <param name="value">The value.</param>
		/// <param name="isRelative">if set to <c>true</c> [is relative].</param>
		public override void Write<T>(IntPtr address, T value, bool isRelative = false)
		{
			if (isRelative)
				address = ToAbsolute(address);

			Marshal.StructureToPtr(value, address, false);
		}

		private T ReadInternal<T>(IntPtr address) where T : struct
		{
			var type = MarshalCache<T>.RealType;

			if (!MarshalCache<T>.TypeRequiresMarshal)
			{
				var val = default(T);
				// If the (value) type doesn't require marshalling, we can simply use MoveMemory to move the entire
				// thing in one swoop. This is significantly faster than having it go through the Marshal.
				var ptr = MarshalCache<T>.GetUnsafePtr(ref val);
				MoveMemory(ptr, (void*) address, MarshalCache<T>.Size);

				return val;
			}

			// Have the marshal deal with it for now.
			return Marshal.PtrToStructure<T>(address);

			// Scrapped until it can be properly unit tested.

			//object ret = null;
			//var typeCode = Type.GetTypeCode(type);

			//// Special case simple value types:
			//// - Boolean
			//// - Byte, SByte
			//// - Char
			//// - Decimal
			//// - Int32, UInt32
			//// - Int64, UInt64
			//// - Int16, UInt16
			//// - IntPtr, UIntPtr
			//// As of .NET 4.5, the (Type)(object)result pattern used below
			//// is recognized and optimized by both 32-bit and 64-bit JITs.
			//// Therefore, do not change this to a switch - JIT won't apply same optimization (it won't box!).
			//if (typeCode == TypeCode.Boolean)
			//	ret = *(byte*)address != 0;
			//if (typeCode == TypeCode.Byte)
			//	ret = *(byte*)address;
			//if (typeCode == TypeCode.SByte)
			//	ret = *(sbyte*)address;
			//if (typeCode == TypeCode.Char)
			//	ret = *(char*)address;
			//if (typeCode == TypeCode.Decimal)
			//	ret = *(decimal*)address;
			//if (typeCode == TypeCode.Int32)
			//	ret = *(int*)address;
			//if (typeCode == TypeCode.UInt32)
			//	ret = *(uint*)address;
			//if (typeCode == TypeCode.Int64)
			//	ret = *(long*)address;
			//if (typeCode == TypeCode.UInt64)
			//	ret = *(ulong*)address;
			//if (typeCode == TypeCode.Int16)
			//	ret = *(short*)address;
			//if (typeCode == TypeCode.UInt16)
			//	ret = *(ushort*)address;

			//return (T)ret;
		}

		#region P/Invokes

		[DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
		private static extern void MoveMemory(void* dst, void* src, int size);

		#endregion
	}
}