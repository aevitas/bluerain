// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;
using System.Linq;
using BlueRain.Common;

namespace BlueRain
{
    /// <summary>
    ///     Provides support for pattern finding in process memory.
    /// </summary>
    public class PatternScanner
    {
        private readonly NativeMemory _memory;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PatternScanner" /> class.
        /// </summary>
        /// <param name="memory">The memory instance.</param>
        public PatternScanner(NativeMemory memory)
        {
            Requires.NotNull(memory, nameof(memory));

            _memory = memory;
        }

        /// <summary>
        ///     Finds the specified pattern and mask at the specified address.
        /// </summary>
        /// <param name="startAddress">The start address.</param>
        /// <param name="size">The size.</param>
        /// <param name="patternBytes">The pattern bytes.</param>
        /// <param name="mask">The mask.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">PatternBytes is null.</exception>
        /// <exception cref="System.ArgumentException">Data and mask must be of the same size!</exception>
        /// <exception cref="MemoryReadException">Could not read memory in FindPattern!</exception>
        public IntPtr FindPattern(IntPtr startAddress, int size, byte[] patternBytes, string mask)
        {
            if (!patternBytes.Any())
                throw new ArgumentNullException(nameof(patternBytes));

            if (patternBytes.Length != mask.Length)
                throw new ArgumentException("Data and mask must be of the same size!");

            var buffer = _memory.ReadBytes(startAddress, size);

            if (!buffer.Any())
                throw new MemoryReadException("Could not read memory in FindPattern!");

            var ptr = FindPatternInternal(buffer, patternBytes, mask);

            if (ptr == IntPtr.Zero)
                return IntPtr.Zero;

            return startAddress + (int) ptr;
        }

        private IntPtr FindPatternInternal(byte[] buffer, byte[] patternBytes, string mask)
        {
            Requires.NotNull(buffer, nameof(buffer));

            if (!buffer.Any())
                throw new ArgumentNullException(nameof(buffer));

            var patternLength = patternBytes.Length;
            var dataLength = buffer.Length - patternLength;

            for (var i = 0; i < dataLength; i++)
            {
                var found = true;
                for (var y = 0; y < patternLength; y++)
                {
                    if ((mask[y] == 'x' && patternBytes[y] != buffer[i + y]) ||
                        (mask[y] == '!' && patternBytes[y] == buffer[i + y]))
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return (IntPtr) i;
                }
            }

            return IntPtr.Zero;
        }
    }
}