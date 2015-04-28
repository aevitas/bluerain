using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BlueRain.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueRain.Tests
{
	[TestClass]
	public class ExternalMemoryTests
	{
		private static readonly ExternalProcessMemory Memory = new ExternalProcessMemory(Process.GetCurrentProcess());

		[TestMethod]
		public unsafe void ReadInt32()
		{
			int x = 5;
			var ret = Memory.Read<int>(new IntPtr(&x));

			Assert.IsTrue(ret == x);
		}

		[TestMethod]
		public void Alloc()
		{
			int size = 512;

			var alloc = Memory.Allocate((UIntPtr) size);

			Assert.IsTrue(alloc.Address != IntPtr.Zero);
			Assert.IsTrue(alloc.Size == size);

			alloc.Dispose();

			Assert.IsFalse(alloc.IsAllocated);
		}

		[TestMethod]
		public void AllocatedReadWrite()
		{
			int size = 4;

			var alloc = Memory.Allocate((UIntPtr) size);

			alloc.Write(new IntPtr(0x0), 10);

			Assert.AreEqual(alloc.Read<int>(new IntPtr(0x0)), 10);
		}

		[TestMethod]
		public void StringReadWrite()
		{
			string s = "test string";

			var alloc = Memory.Allocate((UIntPtr) s.Length);

			alloc.WriteString(new IntPtr(0x0), s, Encoding.UTF8);

			Assert.IsTrue(alloc.ReadString(new IntPtr(0x0), Encoding.UTF8) == s);
		}

		[TestMethod]
		[ExpectedException(typeof(BlueRainReadException))]
		public void RelativeRead()
		{
			Memory.ReadBytes(new IntPtr(0x1500), 10, true);
		}

		[TestMethod]
		[ExpectedException(typeof(BlueRainReadException))]
		public void MultipleReads()
		{
			Memory.Read<int>(new IntPtr(0x1000), 10);
		}

		[TestMethod]
		[ExpectedException(typeof(BlueRainWriteException))]
		public void RelativeWrite()
		{
			Memory.WriteBytes(new IntPtr(0x1000), new byte[10], true);
		}

		[TestMethod]
		[ExpectedException(typeof(BlueRainReadException))]
		public void WriteMultipleVals()
		{
			// This will throw a read exception because it has to read to deref.
			Memory.Write(true, 1, new IntPtr(0x10), new IntPtr(0x30), new IntPtr(0x1000));
		}

		[TestMethod]
		[ExpectedException(typeof(BlueRainReadException))]
		public void ReadMultipleVals()
		{
			Memory.Read<int>(true, new IntPtr(0x10), new IntPtr(0x20), new IntPtr(0x1000));
		}

		internal enum TestEnum
		{
			A,
			B,
			C,
			D,
			E
		}

		[TestMethod]
		public void ToRelativeAddress()
		{
			var ptr = Memory.ToRelative(Memory.BaseAddress + 0x5000);

			Assert.AreEqual(ptr, new IntPtr(0x5000));
		}

		[TestMethod]
		public void MarshalCacheTests()
		{
			var boolSize = MarshalCache<bool>.Size;
			var enumSize = MarshalCache<TestEnum>.Size;

			Assert.IsTrue(boolSize == 1);
			Assert.AreEqual(enumSize, Marshal.SizeOf(typeof(int)));
		}

		[TestMethod]
		[ExpectedException(typeof (ArgumentException))]
		public void RequiresConditionThrows()
		{
			var ptr = new IntPtr(0x1000);

			Requires.Condition(() => ptr == IntPtr.Zero, "ptr");
		}

		[TestMethod]
		[ExpectedException(typeof (ArgumentException))]
		public void RequiresNotEqualFails()
		{
			Requires.NotEqual(1, 1, "a");
		}

		[TestMethod]
		[ExpectedException(typeof (ArgumentNullException))]
		public void RequiresNotNullFails()
		{
			string a = null;
			Requires.NotNull(a, "lol");
		}
	}
}
