using Accord.Statistics.Running;
using Microsoft.Win32;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Imaging;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Michelegz.NINA.StarSentinel.StarSentinelCategory {
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
    public class StarCountCondition : SequenceCondition  {
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

        protected IProfileService profileService;
        protected IImageSaveMediator imageSaveMediator;

        private readonly Func<object, BeforeImageSavedEventArgs, Task> beforeImageSavedHandler;

        private Queue<int> history;

        private int maxBadFrames;
        private int relStarCountThreshold; // percentage
        private int absStarCountThreshold; // absolute number of stars
        private bool loopCondition = true;
        private int relativeStarCount;
        private int referenceStarCount;
        private int historySize;
        private int badFrames;
        private int minFramesForAnalysis;
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

        private ImagingContext? lastContext = null;


        [ImportingConstructor]
        public StarCountCondition(
            IProfileService profileService,
            IImageSaveMediator imageSaveMediator
            )

            {
            this.profileService = profileService;
            this.imageSaveMediator = imageSaveMediator;
            this.history = new Queue<int>();
            this.historySize = 1000; // for now it is used just to limit the memory usage
            this.MaxBadFrames = 10;
            this.RelStarCountThreshold = 20;
            this.AbsStarCountThreshold = 10;
            this.minFramesForAnalysis = 5;
            this.ReferenceStarCount = 0;
            this.imageSaveMediator.ImageSaved += OnImageSaved;
            //this.PropertyChanged += PropertyChangeListener;

        }

        /*
        private void PropertyChangeListener(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "Status") {
                if (Status == SequenceEntityStatus.DISABLED) {
                    loopCondition = true;
                } else if (Status == SequenceEntityStatus.CREATED) {
                    loopCondition = true;
                }
            }
        }
        */


        [JsonProperty]
        public int MaxBadFrames {
            get => maxBadFrames;
            set {
                maxBadFrames = Math.Clamp(value, 1, int.MaxValue);
                RaisePropertyChanged();
            }
        }


        [JsonProperty]
        public int RelStarCountThreshold {
            get => relStarCountThreshold;
            set {
                relStarCountThreshold = Math.Clamp(value, 0, 100)    ;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int AbsStarCountThreshold {
            get => absStarCountThreshold;
            set {
                absStarCountThreshold = Math.Clamp(value, 0, int.MaxValue) ;
                RaisePropertyChanged();
            }
        }


        [JsonProperty]
        public int RelativeStarCount {
            get => relativeStarCount;
            private set {
                relativeStarCount = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RelativeStarCountText));
            }
        }


        [JsonProperty]
        public String RelativeStarCountText {
            get {
                if (history.Count >= minFramesForAnalysis)
                    return RelativeStarCount.ToString() + "%";
                else return "--";
            }
        }

        [JsonProperty]
        public int ReferenceStarCount {
            get => referenceStarCount;
            private set {
                referenceStarCount = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ReferenceStarCountText));
            }
        }

        [JsonProperty]
        public string ReferenceStarCountText {
            get {
                if (history.Count >= minFramesForAnalysis)
                    return ReferenceStarCount.ToString();
                else return "--";
            }
        }

        [JsonProperty]
        public int BadFrames {
            get => badFrames;
            private set {
                badFrames = value;
                RaisePropertyChanged();
            }
        }
        private void OnImageSaved(object sender, ImageSavedEventArgs e) {
            try {

                // Check if the saved image is a light frame, if not skip the analysis
                if (e.MetaData?.Image?.ImageType != "LIGHT") {
                    Logger.Debug($"StarSentinel: Skipping non-light frame. Image type: {e.MetaData?.Image?.ImageType}");
                    return;
                }
                
                // =========================
                // CONTEXT EXTRACTION
                // =========================

                var coords = e.MetaData?.Target?.Coordinates;
                var cam = e.MetaData?.Camera;

                ImagingContext? currentContext = null;

                if (coords != null && e.MetaData.Image.ExposureTime > 0 && cam != null) {
                    double pixelScaleProxy = cam.PixelSize * cam.BinX; // proxy stabile senza focale

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

                    Logger.Debug(
                        $"StarSentinel: Context -> " +
                        $"Filter={currentContext.Filter}, " +
                        $"Exp={currentContext.Exposure}, " +
                        $"RA={currentContext.RA}, DEC={currentContext.Dec}, " +
                        $"Bin={currentContext.BinX}x{currentContext.BinY}, " +
                        $"Gain={currentContext.Gain}, " +
                        $"Sensor={currentContext.SensorType}, " +
                        $"ScaleProxy={currentContext.PixelScaleProxy}"
                    );

                    if (lastContext == null) {
                        lastContext = currentContext;
                        Logger.Info("StarSentinel: Initial context set.");
                    } else if (!IsSameContext(lastContext, currentContext)) {
                        Logger.Info("StarSentinel: Context change detected -> resetting history.");

                        ResetHistory();
                        lastContext = currentContext;

                        return; // IMPORTANT: skip this frame
                    }
                } else {
                    Logger.Debug("StarSentinel: Missing context info (coords/camera/duration), skipping context evaluation.");
                }


                // =========================
                // Star count analysis
                // =========================
                var count = e.StarDetectionAnalysis?.DetectedStars;

                if (count == null) {
                    Logger.Debug("StarSentinel: No star detection data available for this image.");
                    return;
                }

                int starCount = count.Value;

                Logger.Debug("StarSentinel: Detected star count for saved image: " + starCount);

                history.Enqueue(starCount);

                if (history.Count > historySize)
                    history.Dequeue();

                if (history.Count < minFramesForAnalysis) {
                    Logger.Debug($"StarSentinel: Collecting data... {history.Count}/{minFramesForAnalysis} frames collected for analysis.");
                    return;
                }

                var arr = history.OrderBy(x => x).ToArray();

                if (arr.Length < 5)
                    return;

                //calculate the 80th percentile as reference, to be more robust against outliers than the maximum
                double percentile = 0.8;
                int index = (int)Math.Floor(percentile * (arr.Length - 1));
                ReferenceStarCount = arr[index];

                Logger.Debug($"StarSentinel: Calculated reference star count at {percentile * 100} percentile: {ReferenceStarCount}");

                if (ReferenceStarCount <= 0)
                    return;

                double relative = ((double)starCount / ReferenceStarCount) * 100.0;
                relative = Math.Clamp(relative, 0, 1000); //just to prevent extreme outliers

                RelativeStarCount = (int)relative;

                bool isBad =
                    relative < RelStarCountThreshold ||
                    starCount < AbsStarCountThreshold;

                if (isBad) {
                    BadFrames++;
                    Logger.Info($"StarSentinel: Bad frame detected. Star count: {starCount}, Relative star count: {RelativeStarCount}%. Consecutive bad frames: {BadFrames}/{MaxBadFrames}.");

                } else if (BadFrames>0) {
                    BadFrames = 0;
                Logger.Info($"StarSentinel: Good frame detected. Star count: {starCount}, Relative star count: {RelativeStarCount}%. Consecutive bad frames reset to 0.");
                }

                if (BadFrames >= MaxBadFrames) {
                    loopCondition = false;
                    Logger.Info("StarSentinel: Too many consecutive bad frames detected. Loop condition set to false.");
                    return;
                }

                loopCondition = true;
            } catch {
                loopCondition = true;
            }
        }

        // This method resets the history and related counters when a context change is detected
        private void ResetHistory() {
            history.Clear();
            BadFrames = 0;
            ReferenceStarCount = 0;
            RelativeStarCount = 0;

            Logger.Info("StarSentinel: History reset completed.");
        }
        private bool IsSameContext(ImagingContext a, ImagingContext b) {
            if (a == null || b == null) {
                Logger.Debug("StarSentinel: One of the contexts is null, treating as different.");
                return false;
            }

            // =========================
            // HARDWARE CHECK (strict)
            // =========================

            if (a.Filter != b.Filter) {
                Logger.Debug($"StarSentinel: Filter mismatch. A={a.Filter}, B={b.Filter}");
                return false;
            }

            if (a.BinX != b.BinX || a.BinY != b.BinY) {
                Logger.Debug($"StarSentinel: Binning mismatch. A={a.BinX}x{a.BinY}, B={b.BinX}x{b.BinY}");
                return false;
            }

            if (a.Gain != b.Gain) {
                Logger.Debug($"StarSentinel: Gain mismatch. A={a.Gain}, B={b.Gain}");
                return false;
            }

            if (a.SensorType != b.SensorType) {
                Logger.Debug($"StarSentinel: Sensor type mismatch. A={a.SensorType}, B={b.SensorType}");    
                return false;
            }

            // =========================
            // EXPOSURE CHECK (RELATIVE)
            // =========================

            const double exposureTolerancePercent = 10.0; // tuning parameter

            double exposureRatio = (b.Exposure / a.Exposure) * 100.0;

            if (Math.Abs(exposureRatio - 100.0) > exposureTolerancePercent) {
                Logger.Debug(
                    $"StarSentinel: Exposure mismatch. " +
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

            const double fovTolerancePercent = 0.20; // 20%

            double threshold = fovDiagonal * fovTolerancePercent;

            // =========================
            // DECISION
            // =========================

            if (distance > threshold) {
                Logger.Debug(
                    $"StarSentinel: Context change detected (FOV rule). " +
                    $"Shift={distance:F5}°, Threshold={threshold:F5}° ({fovTolerancePercent * 100}% FOV)"
                );

                return false;
            }

            return true;
        }


        [JsonProperty]
        public bool LoopCondition {
            get => loopCondition;
        }

        /// <summary>
        /// Once this check returns false, the condition will cause its parent instruction set to skip the rest and proceed with the next set
        /// </summary>
        /// <param name="previousItem"></param>
        /// <param name="nextItem"></param>
        /// <returns></returns>
        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            return loopCondition;
        }

        public override object Clone() {
            return new StarCountCondition(
                this.profileService,
                this.imageSaveMediator
                ) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description
            };
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(StarCountCondition)}, Check: {LoopCondition}";
        }




        public void Dispose() {
            this.imageSaveMediator.ImageSaved -= OnImageSaved;
        }
    }
}