using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Michelegz.NINA.StarSentinel.StarSentinelCategory
{
    /// <summary>
    /// This Class shows the basic principle on how to add a new Sequence Trigger to the N.I.N.A. sequencer via the plugin interface
    /// For ease of use this class inherits the abstract SequenceTrigger which already handles most of the running logic, like logging, exception handling etc.
    /// A complete custom implementation by just implementing ISequenceTrigger is possible too
    /// The following MetaData can be set to drive the initial values
    /// --> Name - The name that will be displayed for the item
    /// --> Description - a brief summary of what the item is doing. It will be displayed as a tooltip on mouseover in the application
    /// --> Icon - a string to the key value of a Geometry inside N.I.N.A.'s geometry resources
    ///
    /// If the item has some preconditions that should be validated, it shall also extend the IValidatable interface and add the validation logic accordingly.
    /// </summary>
    [ExportMetadata("Name", "Star Count Loop Condition")]
    [ExportMetadata("Description", "This condition is true while StarSentinel detects enough stars")]
    [ExportMetadata("Icon", "StarSentinel_Icon")]
    [ExportMetadata("Category", "Star Sentinel")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public class StarCountCondition : SequenceCondition
    {
        /// <summary>
        /// The constructor marked with [ImportingConstructor] will be used to import and construct the object
        /// General device interfaces can be added to the constructor parameters and will be automatically injected on instantiation by the plugin loader
        /// </summary>
        /// <remarks>
        /// Available interfaces to be injected:
        ///     - IProfileService,
        ///     - ICameraMediator,
        ///     - ITelescopeMediator,
        ///     - IFocuserMediator,
        ///     - IFilterWheelMediator,
        ///     - IGuiderMediator,
        ///     - IRotatorMediator,
        ///     - IFlatDeviceMediator,
        ///     - IWeatherDataMediator,
        ///     - IImagingMediator,
        ///     - IApplicationStatusMediator,
        ///     - INighttimeCalculator,
        ///     - IPlanetariumFactory,
        ///     - IImageHistoryVM,
        ///     - IDeepSkyObjectSearchVM,
        ///     - IDomeMediator,
        ///     - IImageSaveMediator,
        ///     - ISwitchMediator,
        ///     - ISafetyMonitorMediator,
        ///     - IApplicationMediator
        ///     - IApplicationResourceDictionary
        ///     - IFramingAssistantVM
        ///     - IList<IDateTimeProvider>
        /// </remarks>
        ///

        private readonly IProfileService profileService;
        private readonly IImageSaveMediator imageSaveMediator;

        // Keep a fixed reference to the event handler to ensure
        // that Unsubscribe (-=) target exactly the same delegate instance as Subscribe (+=).
        private readonly EventHandler<ImageSavedEventArgs> imageSavedHandler;

        private bool isSubscribed = false;

        private int maxBadFrames;
        private int relStarCountThreshold; // percentage
        private int absStarCountThreshold; // absolute number of stars
        private bool loopCondition = true;
        private int relativeStarCount;
        private int referenceStarCount;
        private int historySize = 1000;

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

        private readonly List<(ImagingContext Context, ContextState State)> contexts = new();

        private ContextState? currentState;

        private class ContextState
        {
            public Queue<int> History { get; } = new();
            public int BadFrames { get; set; }
        }

        private const String logPrefix = "StarSentinel: ";

        [ImportingConstructor]
        public StarCountCondition(IProfileService profileService, IImageSaveMediator imageSaveMediator)
        {
            this.profileService = profileService;
            this.imageSaveMediator = imageSaveMediator;

            // Initialize the handler once during construction
            this.imageSavedHandler = OnImageSaved;

            // Listen for status changes (Enabled/Disabled/Running) to manage the lifecycle
            this.PropertyChanged += PropertyChangeListener;

            // Default values
            this.MaxBadFrames = 10;
            this.RelStarCountThreshold = 20;
        }

        private void PropertyChangeListener(object sender, PropertyChangedEventArgs e)
        {
            //If the user disables the condition in the N.I.N.A. UI,
            // we must immediately detach the event to prevent background processing.

            if (e.PropertyName == "Status" && Status == SequenceEntityStatus.DISABLED)
            {
                Unsubscribe();
            }
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            /* * Lazy Subscription: We subscribe only when the sequencer actually
             * evaluates this condition. This avoids UI clones from subscribing.
             */
            if (!isSubscribed)
            {
                Subscribe();
            }

            return loopCondition;
        }

        private void Subscribe()
        {
            if (!isSubscribed && imageSaveMediator != null)
            {
                imageSaveMediator.ImageSaved += imageSavedHandler;
                isSubscribed = true;
                Logger.Debug($"[StarSentinel] Subscribed instance {GetHashCode()}");
            }
        }

        private void Unsubscribe()
        {
            if (isSubscribed && imageSaveMediator != null)
            {
                imageSaveMediator.ImageSaved -= imageSavedHandler;
                isSubscribed = false;
                Logger.Debug($"[StarSentinel] Unsubscribed instance {GetHashCode()}");
            }
        }

        public void Dispose()
        {
            // Essential cleanup when the item is removed from the sequencer
            Unsubscribe();
            this.PropertyChanged -= PropertyChangeListener;
        }

        public StarCountCondition(StarCountCondition copyMe)
            : this(copyMe.profileService, copyMe.imageSaveMediator)
        {
            CopyMetaData(copyMe);
            MaxBadFrames = copyMe.MaxBadFrames;
            RelStarCountThreshold = copyMe.RelStarCountThreshold;
            AbsStarCountThreshold = copyMe.AbsStarCountThreshold;
        }

        public override object Clone()
        {
            /* * When N.I.N.A. clones the object for the UI, the new instance
             * starts with isSubscribed = false. It will only subscribe if it's
             * actually executed in a sequence.
             */
            return new StarCountCondition(this);
        }

        [JsonProperty]
        public int MaxBadFrames
        {
            get => maxBadFrames;
            set
            {
                maxBadFrames = Math.Clamp(value, 1, int.MaxValue);
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int RelStarCountThreshold
        {
            get => relStarCountThreshold;
            set
            {
                relStarCountThreshold = Math.Clamp(value, 0, 100);
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int AbsStarCountThreshold
        {
            get => absStarCountThreshold;
            set
            {
                absStarCountThreshold = Math.Clamp(value, 0, int.MaxValue);
                RaisePropertyChanged();
            }
        }

        private string FormatStatText(int value, bool addPercent = false)
        {
            if (currentState?.History == null)
            {
                return "--";
            }

            if (currentState.History.Count < StarSentinelMediator.Instance.Plugin.InitialSamples)
            {
                return "--";
            }

            return addPercent ? $"{value}%" : value.ToString();
        }

        [JsonProperty]
        public int RelativeStarCount
        {
            get => relativeStarCount;
            private set
            {
                relativeStarCount = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RelativeStarCountText));
            }
        }

        [JsonProperty]
        public string RelativeStarCountText => FormatStatText(RelativeStarCount, true);

        [JsonProperty]
        public int ReferenceStarCount
        {
            get => referenceStarCount;
            private set
            {
                referenceStarCount = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ReferenceStarCountText));
            }
        }

        [JsonProperty]
        public string ReferenceStarCountText => FormatStatText(ReferenceStarCount);

        [JsonProperty]
        public int BadFrames
        {
            get => currentState?.BadFrames ?? 0;
        }

        private void UpdateBadFrames(int value)
        {
            if
                (currentState == null)
            {
                return;
            }
            currentState.BadFrames = value;
            RaisePropertyChanged(nameof(BadFrames));
        }

        private void SetLoopCondition(bool value)
        {
            if (loopCondition != value)
            {
                loopCondition = value;
                RaisePropertyChanged(nameof(LoopCondition));
            }
        }

        private void OnImageSaved(object sender, ImageSavedEventArgs e)
        {
            try
            {
                // =========================
                // FILTER: only LIGHT frames
                // =========================
                if (e.MetaData?.Image?.ImageType != "LIGHT")
                {
                    Logger.Debug(logPrefix + $" Skipping non-light frame. Type: {e.MetaData?.Image?.ImageType}");
                    return;
                }

                // =========================
                // CONTEXT EXTRACTION + MATCHING
                // =========================
                var coords = e.MetaData?.Target?.Coordinates;
                var cam = e.MetaData?.Camera;

                ImagingContext? currentContext = null;

                if (coords != null && e.MetaData.Image.ExposureTime > 0 && cam != null)
                {
                    double pixelScaleProxy = cam.PixelSize * cam.BinX;

                    currentContext = new ImagingContext(
                        (e.Filter ?? "Unknown").Trim().ToLowerInvariant(),
                        Math.Round(e.MetaData.Image.ExposureTime, 2),
                        Math.Round(coords.RA, 4),
                        Math.Round(coords.Dec, 4),
                        cam.BinX,
                        cam.BinY,
                        cam.Gain,
                        cam.SensorType,
                        pixelScaleProxy,
                        e.Image.PixelWidth,
                        e.Image.PixelHeight
                    );

                    Logger.Debug(logPrefix +
                        $" Context -> Filter={currentContext.Filter}, Exp={currentContext.Exposure}, " +
                        $"RA={currentContext.RA}, DEC={currentContext.Dec}, Bin={currentContext.BinX}x{currentContext.BinY}, " +
                        $"Gain={currentContext.Gain}, Sensor={currentContext.SensorType}, ScaleProxy={currentContext.PixelScaleProxy}");

                    // Prefer the currently active context first, then search all known contexts.
                    ContextState? matchedState = null;

                    if (currentState != null)
                    {
                        var activeIndex = contexts.FindIndex(c => c.State == currentState);
                        if (activeIndex >= 0)
                        {
                            var activeEntry = contexts[activeIndex];
                            if (IsSameContext(activeEntry.Context, currentContext))
                            {
                                matchedState = activeEntry.State;
                                Logger.Debug(logPrefix + $" Active context matched at index {activeIndex}.");
                            } else
                            {
                                Logger.Debug(logPrefix + $" Active context did not match at index {activeIndex}.");
                            }
                        } else
                        {
                            Logger.Debug(logPrefix + " Current active state is not present in stored contexts.");
                        }
                    }

                    if (matchedState == null)
                    {
                        Logger.Debug(logPrefix + $" Searching {contexts.Count} existing contexts for a match.");
                        int j;
                        for (j = 0; j < contexts.Count; j++)
                        {
                            if (IsSameContext(contexts[j].Context, currentContext))
                            {
                                matchedState = contexts[j].State;
                                Logger.Debug(logPrefix + $" Existing context matched at index {j}.");
                                break;
                            }
                        }
                    }

                    if (matchedState != null)
                    {
                        currentState = matchedState;
                        RaisePropertyChanged(nameof(BadFrames));
                    } else
                    {
                        var newState = new ContextState();
                        contexts.Add((currentContext, newState));
                        currentState = newState;
                        RaisePropertyChanged(nameof(BadFrames));

                        Logger.Debug(logPrefix + " No matching context found. Creating a new context.");
                        Logger.Info(logPrefix + $" New context created. Total contexts: {contexts.Count}");
                    }
                } else
                {
                    Logger.Debug(logPrefix + $" Missing context info (coords/camera/exposure), skipping context evaluation.");
                }

                if (currentState == null)
                {
                    Logger.Debug(logPrefix + $" No active context state.");
                    return;
                }

                // =========================
                // STAR COUNT
                // =========================
                var count = e.StarDetectionAnalysis?.DetectedStars;

                if (count == null)
                {
                    Logger.Debug(logPrefix + $" No star detection data available.");
                    return;
                }

                int starCount = count.Value;

                Logger.Debug(logPrefix + $" Detected stars: {starCount}");

                // =========================
                // HISTORY UPDATE
                // =========================
                currentState.History.Enqueue(starCount);

                if (currentState.History.Count > historySize)
                {
                    currentState.History.Dequeue();
                }

                // =========================
                // INITIAL BUFFER
                // =========================
                if (currentState.History.Count < StarSentinelMediator.Instance.Plugin.InitialSamples)
                {
                    Logger.Debug(logPrefix +
                        $" Collecting data... {currentState.History.Count}/" +
                        $"{StarSentinelMediator.Instance.Plugin.InitialSamples}");
                    return;
                }

                // =========================
                // REFERENCE (percentile)
                // =========================
                var arr = currentState.History.OrderBy(x => x).ToArray();

                if (arr.Length < 5)
                {
                    return;
                }

                double percentile = StarSentinelMediator.Instance.Plugin.ReferencePercentile / 100.0;
                int index = (int)Math.Floor(percentile * (arr.Length - 1));

                ReferenceStarCount = arr[index];

                Logger.Debug(logPrefix +
                    $" Reference ({percentile * 100:F0}th percentile): {ReferenceStarCount}");

                if (ReferenceStarCount <= 0)
                {
                    return;
                }

                // =========================
                // RELATIVE STAR COUNT
                // =========================
                double relative = ((double)starCount / ReferenceStarCount) * 100.0;
                relative = Math.Clamp(relative, 0, 1000);

                RelativeStarCount = (int)relative;

                // =========================
                // BAD FRAME LOGIC
                // =========================
                bool isBad =
                    relative < RelStarCountThreshold ||
                    starCount < AbsStarCountThreshold;

                if (isBad)
                {
                    UpdateBadFrames(currentState.BadFrames + 1);

                    Logger.Info(logPrefix +
                        $" Bad frame. Stars={starCount}, Rel={RelativeStarCount}%, " +
                        $"BadFrames={currentState.BadFrames}/{MaxBadFrames}");
                } else if (currentState.BadFrames > 0)
                {
                    UpdateBadFrames(0);

                    Logger.Info(logPrefix +
                        $" Good frame. Stars={starCount}, Rel={RelativeStarCount}%. Reset bad frames.");
                }

                // =========================
                // LOOP CONDITION
                // =========================
                if (currentState.BadFrames >= MaxBadFrames)
                {
                    SetLoopCondition(false);

                    Logger.Info(logPrefix +
                        $" Too many bad frames -> loopCondition = FALSE");

                    return;
                }

                SetLoopCondition(true);

                // =========================
                // DEBUG: ACTIVE CONTEXTS
                // =========================
                Logger.Debug(logPrefix + $" OnImageSaved HASH={GetHashCode()} THREAD={Environment.CurrentManagedThreadId}");
                Logger.Debug(logPrefix + $" Active contexts: {contexts.Count}");

                int i = 0;
                foreach (var c in contexts)
                {
                    Logger.Debug(logPrefix +
                        $" [{i}] Filter={c.Context.Filter}, Exp={c.Context.Exposure}, " +
                        $"RA={c.Context.RA}, DEC={c.Context.Dec}, " +
                        $"History={c.State.History.Count}, BadFrames={c.State.BadFrames}");

                    i++;
                }
            } catch (Exception ex)
            {
                Logger.Error(logPrefix + $" Exception in OnImageSaved: {ex}");
                SetLoopCondition(true);
            }
        }

        private bool IsSameContext(ImagingContext a, ImagingContext b)
        {
            if (a == null || b == null)
            {
                Logger.Debug(logPrefix + $" One of the contexts is null, treating as different.");
                return false;
            }

            // =========================
            // HARDWARE CHECK (strict)
            // =========================

            if (a.Filter != b.Filter)
            {
                Logger.Debug(logPrefix + $" Filter mismatch. A={a.Filter}, B={b.Filter}");
                return false;
            }

            if (a.BinX != b.BinX || a.BinY != b.BinY)
            {
                Logger.Debug(logPrefix + $" Binning mismatch. A={a.BinX}x{a.BinY}, B={b.BinX}x{b.BinY}");
                return false;
            }

            if (a.Gain != b.Gain)
            {
                Logger.Debug(logPrefix + $" Gain mismatch. A={a.Gain}, B={b.Gain}");
                return false;
            }

            if (a.SensorType != b.SensorType)
            {
                Logger.Debug(logPrefix + $" Sensor type mismatch. A={a.SensorType}, B={b.SensorType}");
                return false;
            }

            // =========================
            // EXPOSURE CHECK (RELATIVE)
            // =========================

            double exposureTolerancePercent = StarSentinelMediator.Instance.Plugin.ExposureTolerance;

            double exposureRatio = (b.Exposure / a.Exposure) * 100.0;

            if (Math.Abs(exposureRatio - 100.0) > exposureTolerancePercent)
            {
                Logger.Debug(
                    logPrefix + $" Exposure mismatch. " +
                    $"A={a.Exposure}s, B={b.Exposure}s, Ratio={exposureRatio:F1}%"
                );

                return false;
            }

            // =========================
            // SKY SHIFT
            // =========================

            double deltaRa =
                (a.RA - b.RA) * Math.Cos(b.Dec * Math.PI / 180.0);

            double deltaDec = a.Dec - b.Dec;

            double distance = Math.Sqrt(deltaRa * deltaRa + deltaDec * deltaDec);

            // =========================
            // REAL FOV (FROM IMAGE SIZE)
            // =========================

            // pixel scale (deg/pixel)
            double pixelScale = a.PixelScaleProxy;

            double fovWidth = pixelScale * a.FrameWidthPx;
            double fovHeight = pixelScale * a.FrameHeightPx;

            double fovDiagonal = Math.Sqrt(
                fovWidth * fovWidth +
                fovHeight * fovHeight
            );

            // =========================
            // TOLERANCE (% OF FOV)
            // =========================

            double fovTolerancePercent = StarSentinelMediator.Instance.Plugin.FovTolerance / 100.0;

            double threshold = fovDiagonal * fovTolerancePercent;

            // =========================
            // DECISION
            // =========================

            if (distance > threshold)
            {
                Logger.Debug(
                    logPrefix + $" Context change detected (FOV rule). " +
                    $"Shift={distance:F5}°, Threshold={threshold:F5}° ({fovTolerancePercent * 100}% FOV)"
                );

                return false;
            }

            return true;
        }

        [JsonProperty]
        public bool LoopCondition
        {
            get => loopCondition;
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(StarCountCondition)}, Check: {LoopCondition}";
        }
    }
}