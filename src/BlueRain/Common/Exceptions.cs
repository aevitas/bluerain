// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;

// These custom exception types are provided to enable framework implementers to catch operations specific to BlueRain.
// When writing extensions, you should always try to throw these whenever possible.

namespace BlueRain.Common
{
	/// <summary>
	/// Base exception type thrown by BlueRain.
	/// </summary>
	public class BlueRainException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="BlueRainException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public BlueRainException(string message)
			: base(message)
		{
		}
	}

	/// <summary>
	/// Exception thrown when a reading operation fails.
	/// </summary>
	public class BlueRainReadException : BlueRainException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="BlueRainReadException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public BlueRainReadException(string message)
			: base(message)
		{
		}


		/// <summary>
		/// Initializes a new instance of the <see cref="BlueRainReadException"/> class.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		public BlueRainReadException(IntPtr address, int count)
			: this(string.Format("ReadProcessMemory failed! Could not read {0} bytes from {1}!", count, address.ToString("X"))
				)
		{
		}
	}

	/// <summary>
	/// Exception thrown when a writing operation fails.
	/// </summary>
	public class BlueRainWriteException : BlueRainException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="BlueRainWriteException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public BlueRainWriteException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BlueRainWriteException"/> class.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="count">The count.</param>
		public BlueRainWriteException(IntPtr address, int count)
			: this(string.Format("WriteProcessMemory failed! Could not write {0} bytes at {1}!", count, address.ToString("X")))
		{
		}
	}

	/// <summary>
	/// Exception thrown when an operation related to injection fails.
	/// </summary>
	public class BlueRainInjectionException : BlueRainException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="BlueRainInjectionException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public BlueRainInjectionException(string message) : base(message)
		{
		}
	}
}