using System;
using BlueRain.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlueRain.Tests
{
    [TestClass]
    public class RequiresTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void RequiresConditionThrows()
        {
            var ptr = new IntPtr(0x1000);

            // This doesn't even do anything.
            Requires.Condition(() => ptr == IntPtr.Zero, "ptr");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void RequiresNotEqualFails()
        {
            Requires.NotEqual(1, 1, "a");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RequiresNotNullFails()
        {
            string a = null;
            Requires.NotNull(a, "lol");
        }
    }
}
