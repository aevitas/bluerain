using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BlueRain.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueRain.Tests
{
	[TestClass]
	public unsafe class ExternalMemoryTests
	{
		private static readonly ExternalProcessMemory Memory = new ExternalProcessMemory(Process.GetCurrentProcess());

		[TestMethod]
		public void ReadInt32()
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
		public void MultipleReads()
		{
			var vec = new Vector3() { X = 1, Y = 2, Z = 3 };
			var ret = Memory.Read<float>(new IntPtr(&vec), 3);

			Assert.IsTrue(Math.Abs(ret[0] - vec.X) < 0.0001f);
			Assert.IsTrue(Math.Abs(ret[1] - vec.Y) < 0.0001f);
			Assert.IsTrue(Math.Abs(ret[2] - vec.Z) < 0.0001f);
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
			int x = 10;

			// This will throw a read exception because it has to read to deref.
			Memory.Write(true, 1, new IntPtr(&x), new IntPtr(0x0), new IntPtr(0x0));
		}

		[TestMethod]
		[ExpectedException(typeof(BlueRainReadException))]
		public void ReadMultipleVals()
		{
			Memory.Read<int>(true, new IntPtr(0x1000), new IntPtr(0x20), new IntPtr(0x1000));
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

			// This doesn't even do anything.
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

		[TestMethod]
		public void DisposeMemory()
		{
			var epm = new ExternalProcessMemory(Process.GetCurrentProcess());

			epm.Dispose();

			Assert.IsTrue(epm.IsDisposed);

			epm.Dispose();
		}


		// Simple struct of 12 bytes used for sequential read/write tests.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct Vector3
		{
			public float X { get; set; }
			public float Y { get; set; }
			public float Z { get; set; }
		}
	}
}
