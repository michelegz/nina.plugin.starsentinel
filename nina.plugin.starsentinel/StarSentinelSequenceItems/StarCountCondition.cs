using Accord.Statistics.Running;
using Microsoft.Win32;
using Newtonsoft.Json;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NINA.Core.Enum;
using System.ComponentModel;

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

        private int sampleSize = 5;
        private int relStarCountThreshold = 20; // percentage
        private int absStarCountThreshold = 10; // absolute number of stars
        private bool loopCondition = true;
        private double averageStarCount;
        private int relativeStarCount;
        private int historySize;

        [ImportingConstructor]
        public StarCountCondition(
            IProfileService profileService,
            IImageSaveMediator imageSaveMediator
            )

            {
            this.profileService = profileService;
            this.imageSaveMediator = imageSaveMediator;
            this.history = new Queue<int>();
            this.historySize = sampleSize * 2;



            //this.beforeImageSavedHandler = ImageSaveMediator_BeforeImageSaved;
            this.imageSaveMediator.ImageSaved += OnImageSaved;
            this.PropertyChanged += PropertyChangeListener;


        }


        private void PropertyChangeListener(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "Status") {
                if (Status == SequenceEntityStatus.DISABLED) {
                    loopCondition = true;
                } else if (Status == SequenceEntityStatus.CREATED) {
                    loopCondition = true;
                }
            }
        }



        [JsonProperty]
        public int SampleSize {
            get => sampleSize;
            set {
                sampleSize = Math.Clamp(value, 1, int.MaxValue);
                this.historySize = sampleSize * 2;
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
        public String RelativeStarCountText {
            get {

                if (history.Count >= historySize)
                    return
                relativeStarCount.ToString() + "%";

                else return "--";

            }
        }


        private void OnImageSaved(object sender, ImageSavedEventArgs e) {
            try {
                int starCount = e.StarDetectionAnalysis?.DetectedStars ?? 0;

                history.Enqueue(starCount);

                if (history.Count > historySize)
                    history.Dequeue();

                if (history.Count < 2 * sampleSize)
                    return;

                var arr = history.ToArray();

                double avgRecent = arr.Skip(arr.Length - sampleSize).Take(sampleSize).Average();
                double avgPrev = arr.Skip(arr.Length - 2 * sampleSize).Take(sampleSize).Average();

                if (avgPrev <= 0)
                    return;

                double relative =  avgRecent/ avgPrev * 100.0;

                relativeStarCount = (int)relative;
                RaisePropertyChanged(nameof(RelativeStarCountText));

                bool isBad =
                    relative < relStarCountThreshold ||
                    avgRecent < absStarCountThreshold;

                loopCondition = !isBad;

                if(isBad) {
                    // log the event of bad star count here, if needed
                //    history.Clear();
                }

            } catch {
                // evita crash silenzioso: meglio loggare in futuro
                loopCondition = true;
            }
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