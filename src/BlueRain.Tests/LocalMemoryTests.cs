using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueRain.Tests
{
	[TestClass]
	public unsafe class LocalMemoryTests
	{
		public static LocalProcessMemory Memory = new LocalProcessMemory(Process.GetCurrentProcess(), false);

		[TestMethod]
		public void WriteAndReadBytes()
		{
			var target = 5;
			var bytes = new byte[] {0, 1, 2, 3};
			Memory.WriteBytes(new IntPtr(&target), bytes);

			var read = Memory.ReadBytes(new IntPtr(&target), 4);

			Assert.IsTrue(read.SequenceEqual(bytes));
		}

		[TestMethod]
		[ExpectedException(typeof (AccessViolationException))]
		public void RelativeRead()
		{
			Memory.Read<int>(new IntPtr(0x1000), true);
		}

		[TestMethod]
		[ExpectedException(typeof (AccessViolationException))]
		public void RelativeWrite()
		{
			var a = 5;
			Memory.Write(new IntPtr(0x1000), 10, true);
		}

		[TestMethod]
		public void ReadWriteStruct()
		{
			var vec = new Vector3 {X = 10, Y = 20, Z = 30};

			Memory.Write(new IntPtr(&vec), new Vector3 {X = 20, Y = 30, Z = 40});

			var read = Memory.Read<float>(new IntPtr(&vec), 3);

			Assert.IsTrue(Math.Abs(read[0] - 20) < 0.01f);
			Assert.IsTrue(Math.Abs(read[1] - 30) < 0.01f);
			Assert.IsTrue(Math.Abs(read[2] - 40) < 0.01f);
		}

		[TestMethod]
		public void ReadDirectCasts()
		{
			var vec = new ReadingTestStruct();

			// I'm going to hell for this..
			var offset = 0;
			Assert.AreEqual(Memory.Read<bool>(new IntPtr(&vec)), vec.Bool);
			offset += sizeof (bool);
			Assert.AreEqual(Memory.Read<byte>(new IntPtr(&vec) + offset), vec.Byte);
			offset += sizeof (byte);
			Assert.AreEqual(Memory.Read<sbyte>(new IntPtr(&vec) + offset), vec.Sbyte);
			offset += sizeof (sbyte);
			Assert.AreEqual(Memory.Read<char>(new IntPtr(&vec) + offset), vec.Char);
			offset += sizeof (char);
			Assert.AreEqual(Memory.Read<int>(new IntPtr(&vec) + offset), vec.Byte);
			offset += sizeof (int);
			Assert.AreEqual(Memory.Read<uint>(new IntPtr(&vec) + offset), vec.Uint);
			offset += sizeof (uint);
			Assert.AreEqual(Memory.Read<long>(new IntPtr(&vec) + offset), vec.Long);
			offset += sizeof (long);
			Assert.AreEqual(Memory.Read<ulong>(new IntPtr(&vec) + offset), vec.Ulong);
			offset += sizeof (ulong);
			Assert.AreEqual(Memory.Read<short>(new IntPtr(&vec) + offset), vec.Short);
			offset += sizeof (short);
			Assert.AreEqual(Memory.Read<ushort>(new IntPtr(&vec) + offset), vec.Ushort);
			offset += sizeof (ushort);
		}

		// Simple struct of 12 bytes used for sequential read/write tests.
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct Vector3
		{
			public float X { get; set; }
			public float Y { get; set; }
			public float Z { get; set; }
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct ReadingTestStruct
		{
			public readonly bool Bool;
			public readonly byte Byte;
			public readonly sbyte Sbyte;
			public readonly char Char;
			public readonly int Int;
			public readonly uint Uint;
			public readonly long Long;
			public readonly ulong Ulong;
			public readonly short Short;
			public readonly ushort Ushort;
		}
	}
}