using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Eurse {
    sealed class FramePacer : IDisposable {
        internal const int Message = 0x8001;
        private const uint HighResolution = 0x00000002;
        private const uint TimerAccess = 0x00100002;
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly Thread worker;
        private readonly AutoResetEvent presented = new AutoResetEvent(false);

        private sealed class WaitableTimer : WaitHandle {
            public WaitableTimer() {
                SafeWaitHandle = CreateWaitableTimerEx(IntPtr.Zero, null, HighResolution, TimerAccess);
            }
        }

        public FramePacer(IntPtr window, int framesPerSecond) {
            worker = new Thread(() => Run(window, framesPerSecond)) { IsBackground = true, Name = "EURSE intro clock" };
            worker.Start();
        }

        public void FramePainted() {
            presented.Set();
        }

        private void Run(IntPtr window, int framesPerSecond) {
            using (var timer = new WaitableTimer()) {
                bool precise = !timer.SafeWaitHandle.IsInvalid;
                var timerSignals = new WaitHandle[] { stop, timer };
                var paintSignals = new WaitHandle[] { stop, presented };
                var clock = Stopwatch.StartNew();
                double period = (double)Stopwatch.Frequency / framesPerSecond;
                double next = period;
                double renderCost = 0;

                while (true) {
                    double remaining = Math.Max(0, (next - clock.ElapsedTicks) / Stopwatch.Frequency);
                    long dueTime = -Math.Max(1, (long)(remaining * TimeSpan.TicksPerSecond));
                    if (precise && SetWaitableTimer(timer.SafeWaitHandle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false)) {
                        if (WaitHandle.WaitAny(timerSignals) == 0) return;
                    } else {
                        precise = false;
                        if (stop.WaitOne(Math.Max(1, (int)Math.Ceiling(remaining * 1000)))) return;
                    }

                    long started = clock.ElapsedTicks;
                    if (!PostMessage(window, Message, IntPtr.Zero, IntPtr.Zero)) return;
                    if (WaitHandle.WaitAny(paintSignals) == 0) return;

                    // Adapt to rendering cost instead of alternating between short and long gaps.
                    double elapsed = clock.ElapsedTicks - started;
                    renderCost += (elapsed - renderCost) * .12;
                    next = started + Math.Max(period, renderCost * 1.06);
                }
            }
        }

        public void Dispose() {
            stop.Set();
            worker.Join();
            presented.Dispose();
            stop.Dispose();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern SafeWaitHandle CreateWaitableTimerEx(IntPtr attributes, string name, uint flags, uint access);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period,
            IntPtr completion, IntPtr context, [MarshalAs(UnmanagedType.Bool)] bool resume);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}

