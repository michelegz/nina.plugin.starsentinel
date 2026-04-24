using NINA.Core.Enum;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Michelegz.NINA.StarSentinel.Helpers
{
    public record ImagingContext(
        string Filter,
        double Exposure,
        double RA,
        double Dec,
        int BinX,
        int BinY,
        int Gain,
        SensorType SensorType,
        double PixelScaleProxy,
        int FrameWidthPx,
        int FrameHeightPx
    );

    public class ImagingContextState
    {
        public Queue<int> History { get; } = new();
        public int BadFrames { get; set; }
    }

    public class ImagingContextRegistry
    {
        private readonly List<(ImagingContext Context, ImagingContextState State)> contexts = new();
        private ImagingContextState currentState;

        public ImagingContextState CurrentState => currentState;
        public IReadOnlyList<(ImagingContext Context, ImagingContextState State)> Contexts => contexts;

        public ImagingContextState GetOrCreateState(ImagingContext currentContext, double exposureTolerancePercent, double fovTolerancePercent)
        {
            var existing = FindExistingState(currentContext, exposureTolerancePercent, fovTolerancePercent);
            if (existing != null)
            {
                currentState = existing;
                return existing;
            }

            var state = new ImagingContextState();
            contexts.Add((currentContext, state));
            currentState = state;
            return state;
        }

        public bool TryMatchCurrentState(ImagingContext currentContext, double exposureTolerancePercent, double fovTolerancePercent)
        {
            var existing = FindExistingState(currentContext, exposureTolerancePercent, fovTolerancePercent);
            if (existing == null)
            {
                return false;
            }

            currentState = existing;
            return true;
        }

        public void Reset()
        {
            contexts.Clear();
            currentState = null;
        }

        private ImagingContextState FindExistingState(ImagingContext currentContext, double exposureTolerancePercent, double fovTolerancePercent)
        {
            foreach (var entry in contexts)
            {
                if (IsSameContext(entry.Context, currentContext, exposureTolerancePercent, fovTolerancePercent))
                {
                    return entry.State;
                }
            }

            return null;
        }

        public static bool IsSameContext(ImagingContext a, ImagingContext b, double exposureTolerancePercent, double fovTolerancePercent)
        {
            if (a == null || b == null)
            {
                return false;
            }

            if (a.Filter != b.Filter)
            {
                return false;
            }

            if (a.BinX != b.BinX || a.BinY != b.BinY)
            {
                return false;
            }

            if (a.Gain != b.Gain)
            {
                return false;
            }

            if (a.SensorType != b.SensorType)
            {
                return false;
            }

            if (!IsExposureMatch(a.Exposure, b.Exposure, exposureTolerancePercent))
            {
                return false;
            }

            return IsSkyPositionMatch(a, b, fovTolerancePercent);
        }

        private static bool IsExposureMatch(double aExposure, double bExposure, double exposureTolerancePercent)
        {
            if (aExposure <= 0 || bExposure <= 0)
            {
                return false;
            }

            double exposureRatio = (bExposure / aExposure) * 100.0;
            return Math.Abs(exposureRatio - 100.0) <= exposureTolerancePercent;
        }

        private static bool IsSkyPositionMatch(ImagingContext a, ImagingContext b, double fovTolerancePercent)
        {
            double deltaRa = (a.RA - b.RA) * Math.Cos(b.Dec * Math.PI / 180.0);
            double deltaDec = a.Dec - b.Dec;
            double distance = Math.Sqrt(deltaRa * deltaRa + deltaDec * deltaDec);

            double fovWidth = a.PixelScaleProxy * a.FrameWidthPx;
            double fovHeight = a.PixelScaleProxy * a.FrameHeightPx;
            double fovDiagonal = Math.Sqrt(fovWidth * fovWidth + fovHeight * fovHeight);
            double threshold = fovDiagonal * fovTolerancePercent;

            return distance <= threshold;
        }
    }
}
