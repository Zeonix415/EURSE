using System;

namespace Eurse {
    sealed class IntroTimeline {
        internal const double CreditsStart = 2.85;
        internal const double CreditLength = 2.13;
        internal const double MountAt = 10.6;
        internal const double FadeDuration = 1.04;
        private const double PresentationEnd = 11.48;
        private double lastTick;
        internal double Progress { get; private set; }
        internal double FadeStart { get; private set; }
        internal double Finish { get { return FadeStart + FadeDuration; } }

        internal IntroTimeline() {
            FadeStart = double.PositiveInfinity;
        }

        internal void Advance(double time, double prepared) {
            double elapsed = Math.Max(0, time - lastTick);
            lastTick = time;
            double presentation = Math.Max(0, Math.Min(1, (time - CreditsStart) / (PresentationEnd - CreditsStart)));
            double guide = presentation + .13 * Math.Sin(Math.PI * presentation);
            double goal = Math.Min(guide, prepared);
            Progress += (goal - Progress) * (1 - Math.Exp(-elapsed * 7.5));
            if (goal >= 1 && Progress >= .996 && double.IsPositiveInfinity(FadeStart)) {
                Progress = 1;
                FadeStart = time + .32;
            }
        }
    }
}
