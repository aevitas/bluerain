// Copyright (C) 2013-2016 aevitas
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

        /// <summary>
        ///     Allocates a chunk of memory of the specified size in the process.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
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
                ret[i] = Read<T>(address + MarshalCache<T>.Size*i, isRelative);

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

            return Marshal.PtrToStructure<T>(address);
        }

        #region P/Invokes

        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void MoveMemory(void* dst, void* src, int size);

        #endregion
    }
}