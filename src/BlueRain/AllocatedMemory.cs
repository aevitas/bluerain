// Copyright (C) 2013-2015 aevitas
// See the file LICENSE for copying permission.

using System;

namespace BlueRain
{
	public class AllocatedMemory : IDisposable
	{
		public IntPtr Address { get; private set; }
		public uint Size { get; private set; }

		#region Implementation of IDisposable

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <exception cref="NotImplementedException"></exception>
		public void Dispose()
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
