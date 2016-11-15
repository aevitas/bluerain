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
            var x = 5;
            var ret = Memory.Read<int>(new IntPtr(&x));

            Assert.IsTrue(ret == x);
        }

        [TestMethod]
        public void Alloc()
        {
            var size = 512;

            var alloc = Memory.Allocate((UIntPtr)size);

            Assert.IsTrue(alloc.Address != IntPtr.Zero);
            Assert.IsTrue(alloc.Size == size);

            alloc.Dispose();

            Assert.IsFalse(alloc.IsAllocated);
        }

        [TestMethod]
        public void AllocatedReadWrite()
        {
            var size = 4;

            var alloc = Memory.Allocate((UIntPtr)size);

            alloc.Write(new IntPtr(0x0), 10);

            Assert.AreEqual(alloc.Read<int>(new IntPtr(0x0)), 10);
        }

        [TestMethod]
        public void StringReadWrite()
        {
            var s = "test string";

            var alloc = Memory.Allocate((UIntPtr)s.Length);

            alloc.WriteString(new IntPtr(0x0), s, Encoding.UTF8);

            Assert.IsTrue(alloc.ReadString(new IntPtr(0x0), Encoding.UTF8) == s);
        }

        [TestMethod]
        public void UnicodeStringReadWrite()
        {
            var text = "some test string";
            var bytes = Encoding.Unicode.GetBytes(text.ToCharArray());
            var s = Encoding.Unicode.GetString(bytes);

            using (var alloc = Memory.Allocate((UIntPtr)bytes.Length))
            {
                alloc.WriteString((IntPtr)0x0, s, Encoding.Unicode);

                Assert.IsTrue(alloc.ReadString((IntPtr)0x0, Encoding.Unicode) == s);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(MemoryReadException))]
        public void RelativeRead()
        {
            Memory.ReadBytes(new IntPtr(0x1500), 10, true);
        }

        [TestMethod]
        public void MultipleReads()
        {
            var vec = new Vector3 { X = 1, Y = 2, Z = 3 };
            var ret = Memory.Read<float>(new IntPtr(&vec), 3);

            Assert.IsTrue(Math.Abs(ret[0] - vec.X) < 0.0001f);
            Assert.IsTrue(Math.Abs(ret[1] - vec.Y) < 0.0001f);
            Assert.IsTrue(Math.Abs(ret[2] - vec.Z) < 0.0001f);
        }

        [TestMethod]
        [ExpectedException(typeof(MemoryWriteException))]
        public void RelativeWrite()
        {
            Memory.WriteBytes(new IntPtr(0x1000), new byte[10], true);
        }

        [TestMethod]
        [ExpectedException(typeof(MemoryReadException))]
        public void WriteMultipleVals()
        {
            var x = 10;

            // This will throw a read exception because it has to read to deref.
            Memory.Write(true, 1, new IntPtr(&x), new IntPtr(0x0), new IntPtr(0x0));
        }

        [TestMethod]
        [ExpectedException(typeof(MemoryReadException))]
        public void ReadMultipleVals()
        {
            Memory.Read<int>(true, new IntPtr(0x1000), new IntPtr(0x20), new IntPtr(0x1000));
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
        public void DisposeMemory()
        {
            var epm = new ExternalProcessMemory(Process.GetCurrentProcess());

            epm.Dispose();

            Assert.IsTrue(epm.IsDisposed);

            epm.Dispose();
        }

        internal enum TestEnum
        {
            A,
            B,
            C,
            D,
            E
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