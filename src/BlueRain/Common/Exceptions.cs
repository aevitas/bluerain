// Copyright (C) 2013-2016 aevitas
// See the file LICENSE for copying permission.

using System;

// These custom exception types are provided to enable framework implementers to catch operations specific to BlueRain.
// When writing extensions, you should always try to throw these whenever possible.

namespace BlueRain.Common
{
	/// <summary>
	///     Base exception type thrown by BlueRain.
	/// </summary>
	public class BlueRainException : Exception
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="BlueRainException" /> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public BlueRainException(string message)
			: base(message)
		{
		}
	}

	/// <summary>
	///     Exception thrown when a reading operation fails.
	/// </summary>
	public class MemoryReadException : BlueRainException
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="MemoryReadException" /> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public MemoryReadException(string message)
			: base(message)
		{
		}


		/// <summary>
		///     Initializes a new instance of the <see cref="MemoryReadException" /> class.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		public MemoryReadException(IntPtr address, int count)
			: this($"ReadProcessMemory failed! Could not read {count} bytes from {address.ToString("X")}!"
			    )
		{
		}
	}

	/// <summary>
	///     Exception thrown when a writing operation fails.
	/// </summary>
	public class MemoryWriteException : BlueRainException
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="MemoryWriteException" /> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public MemoryWriteException(string message)
			: base(message)
		{
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="MemoryWriteException" /> class.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		public MemoryWriteException(IntPtr address, int count)
			: this($"WriteProcessMemory failed! Could not write {count} bytes at {address.ToString("X")}!")
		{
		}
	}

	/// <summary>
	///     Exception thrown when an operation related to injection fails.
	/// </summary>
	public class InjectionException : BlueRainException
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="InjectionException" /> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public InjectionException(string message) : base(message)
		{
		}
	}
}