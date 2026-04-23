using Michelegz.NINA.StarSentinel.Properties;
using Namotion.Reflection;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Image.ImageData;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Settings = Michelegz.NINA.StarSentinel.Properties.Settings;

namespace Michelegz.NINA.StarSentinel {
    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    /// 
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "StarSentinel_Options" where StarSentinel corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class StarSentinel : PluginBase, INotifyPropertyChanged {
        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;
        private ICommand resetSettingsCommand;

        [ImportingConstructor]
        
        public StarSentinel(IProfileService profileService, IOptionsVM options) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            // This helper class can be used to store plugin settings that are dependent on the current profile
            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;

        }
        
        public override Task Teardown() {
            // Make sure to unregister an event when the object is no longer in use. Otherwise garbage collection will be prevented.

            return base.Teardown();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public uint ExposureTolerance {
            get {
                uint exposureTolerance;
                if (!profileService.ActiveProfile.PluginSettings.TryGetValue(Guid.Parse(this.Identifier), nameof(ExposureTolerance), out exposureTolerance)) {
                    exposureTolerance = Properties.Settings.Default.ExposureTolerance;
                    pluginSettings.SetValueUInt32(nameof(ExposureTolerance), exposureTolerance);
                }
                return exposureTolerance;
            }
            set {
                if (ExposureTolerance != value) {
                    pluginSettings.SetValueUInt32(nameof(ExposureTolerance), Math.Clamp(value, 0, 100));
                    RaisePropertyChanged();
                }
            }
        }

        public uint FovTolerance {
            get {
                uint fovTolerance;
                if (!profileService.ActiveProfile.PluginSettings.TryGetValue(Guid.Parse(this.Identifier), nameof(FovTolerance), out fovTolerance)) {
                    fovTolerance = Properties.Settings.Default.FovTolerance;
                    pluginSettings.SetValueUInt32(nameof(FovTolerance), fovTolerance);
                }
                return fovTolerance;
            }
            set {
                if (FovTolerance != value) {
                    pluginSettings.SetValueUInt32(nameof(FovTolerance), Math.Clamp(value, 0, 100));
                    RaisePropertyChanged();
                }
            }
        }


        public uint ReferencePercentile {
            get {
                uint referencePercentile;
                if (!profileService.ActiveProfile.PluginSettings.TryGetValue(Guid.Parse(this.Identifier), nameof(ReferencePercentile), out referencePercentile)) {
                    referencePercentile = Properties.Settings.Default.ReferencePercentile;
                    pluginSettings.SetValueUInt32(nameof(ReferencePercentile), referencePercentile);
                }
                return referencePercentile;
            }
            set {
                if (ReferencePercentile != value) {
                    pluginSettings.SetValueUInt32(nameof(ReferencePercentile), Math.Clamp(value, 0, 100));
                    RaisePropertyChanged();
                }
            }
        }

        public uint InitialSamples {
            get {
                uint initialSamples;
                if (!profileService.ActiveProfile.PluginSettings.TryGetValue(Guid.Parse(this.Identifier), nameof(InitialSamples), out initialSamples)) {
                    initialSamples = Properties.Settings.Default.InitialSamples;
                    pluginSettings.SetValueUInt32(nameof(InitialSamples), initialSamples);
                }
                return initialSamples;
            }
            set {
                if (InitialSamples != value) {
                    pluginSettings.SetValueUInt32(nameof(InitialSamples), Math.Clamp(value, 1, 50));
                    RaisePropertyChanged();
                }
            }
        }


        public ICommand ResetSettingsCommand => resetSettingsCommand ??= new DelegateCommand(_ => ResetSettings());

        public void ResetSettings() {
            try {
                    Properties.Settings.Default.Reset();
                    CoreUtil.SaveSettings(Properties.Settings.Default);
                    RaisePropertyChanged(nameof(ExposureTolerance));
                    RaisePropertyChanged(nameof(FovTolerance));
                    RaisePropertyChanged(nameof(ReferencePercentile));
                    RaisePropertyChanged(nameof(InitialSamples));

            } catch (Exception ex) {
                Logger.Error(ex);
            }

        }

        private class DelegateCommand : ICommand {
            private readonly Action<object> execute;
            private readonly Func<object, bool> canExecute;

            public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null) {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this.canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true;

            public void Execute(object parameter) => execute(parameter);

            public event EventHandler CanExecuteChanged {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }

    }


}
