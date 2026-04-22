using Accord.Statistics.Running;
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
    [ExportMetadata("Icon", "Plugin_Test_SVG")]
    [ExportMetadata("Category", "Star Sentinel")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public class StarCountCondition : SequenceCondition {
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
        protected IImageHistoryVM imageHistoryVM;
        protected IImageStatisticsVM imageStatisticsVM;
        protected IImagingMediator imagingMediator;

        private readonly Func<object, BeforeImageSavedEventArgs, Task> beforeImageSavedHandler;

        private Queue<int> recentStarCounts;

        [ImportingConstructor]
        public StarCountCondition(
            IProfileService profileService,
            IImageSaveMediator imageSaveMediator,
            IImageHistoryVM imageHistoryVM,
            IImageStatisticsVM imageStatisticsVM,
            IImagingMediator imagingMediator
            )

            {
            this.profileService = profileService;
            this.imageSaveMediator = imageSaveMediator;
            this.imageHistoryVM = imageHistoryVM;
            this.imageStatisticsVM = imageStatisticsVM; 
            this.imagingMediator = imagingMediator;
            this.recentStarCounts = new Queue<int>();

            //this.beforeImageSavedHandler = ImageSaveMediator_BeforeImageSaved;
            this.imageSaveMediator.ImageSaved += OnImageSaved;


            IsTruthy = true;
        }


        private void OnImageSaved(object sender, ImageSavedEventArgs e) {
            int starCount = 0;

            try {
                
                starCount = e.StarDetectionAnalysis.DetectedStars;
                recentStarCounts.Enqueue(starCount);

            } catch {

                starCount = 0;
            }
        }


        private bool isTruthy;

        [JsonProperty]
        public bool IsTruthy {
            get => isTruthy;
            set {
                isTruthy = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Once this check returns false, the condition will cause its parent instruction set to skip the rest and proceed with the next set
        /// </summary>
        /// <param name="previousItem"></param>
        /// <param name="nextItem"></param>
        /// <returns></returns>
        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            return IsTruthy;
        }



        public override object Clone() {
            return new StarCountCondition(
                this.profileService,
                this.imageSaveMediator,
                this.imageHistoryVM,
                this.imageStatisticsVM,
                this.imagingMediator
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
            return $"Category: {Category}, Item: {nameof(StarCountCondition)}, IsTruthy: {IsTruthy}";
        }
    }
}