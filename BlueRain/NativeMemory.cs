// Copyright (C) 2013-2015 aevitas
// See the file COPYING for copying permission.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BlueRain.Common;

namespace BlueRain
{
    /// <summary>
    /// Base class for all internal and external process manipulation types.
    /// </summary>
    public abstract class NativeMemory
    {
        /// <summary>
        /// Gets or sets the base address of the wrapped process' main module.
        /// </summary>
        protected IntPtr BaseAddress { get; set; }

        /// <summary>
        /// Gets or sets the process this NativeMemory instance is wrapped around.
        /// </summary>
        public Process Process { get; protected set; }

        protected NativeMemory(Process process)
        {
            Requires.NotNull(process);

            Process = process;

            Process.EnableRaisingEvents = true;
            Process.Exited += async (sender, args) =>
            {
                // Just pass the exit code and the EventArgs to the handler.
                await OnExited(Process.ExitCode, args);
            };
        }

        protected virtual async Task OnExited(int exitCode, EventArgs eventArgs)
        {
        }
    }
}
