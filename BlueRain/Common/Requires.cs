using System;

namespace BlueRain.Common
{
    /// <summary>
    /// Runtime class to assert input values, and throws exceptions if the requirements aren't met.
    /// </summary>
    public static class Requires
    {
        /// <summary>
        /// Requires the specified value to be non-null, and throws an ArgumentNullException if this requirement is not met.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void NotNull<T>(T value, string parameterName) where T : class // We don't want to compare value types with null if we can avoid it.
        {
            if (value == null)
                throw new ArgumentNullException(parameterName);
        }

        /// <summary>
        /// Requires the specified value to be non-null, and throws an ArgumentNullReference using the type's full name if this requirement is not met.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        public static void NotNull<T>(T value) where T : class
        {
            NotNull(value, "Parameter of type: " + value.GetType().FullName);
        }
    }
}
