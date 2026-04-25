using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Settings = Michelegz.NINA.StarSentinel.Properties.Settings;


namespace Michelegz.NINA.StarSentinel
{
    // Mediator between plugin class and trigger class
    public class StarSentinelMediator
    {
        private StarSentinelMediator()
        { }

        private static readonly Lazy<StarSentinelMediator> lazy = new Lazy<StarSentinelMediator>(() => new StarSentinelMediator());
        public static StarSentinelMediator Instance { get => lazy.Value; }

        public void RegisterPlugin(StarSentinel plugin)
        {
            this.Plugin = plugin;
        }

        public StarSentinel Plugin { get; private set; }
    }

    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    ///
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "StarSentinel_Options" where StarSentinel corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class StarSentinel : PluginBase, INotifyPropertyChanged
    {
        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;
        private readonly Guid pluginIdentifier;
        private ICommand resetSettingsCommand;

        [ImportingConstructor]
        public StarSentinel(IProfileService profileService, IOptionsVM options)
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            StarSentinelMediator.Instance.RegisterPlugin(this);

            this.profileService = profileService;
            this.pluginIdentifier = Guid.Parse(this.Identifier);
            // This helper class can be used to store plugin settings that are dependent on the current profile
            this.pluginSettings = new PluginOptionsAccessor(profileService, this.pluginIdentifier);
        }

        public override Task Teardown()
        {
            // Make sure to unregister an event when the object is no longer in use. Otherwise garbage collection will be prevented.

            return base.Teardown();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private uint GetSettingUInt32(string name, uint defaultValue)
        {
            var value = pluginSettings.GetValueUInt32(name, uint.MaxValue);
            if (value == uint.MaxValue)
            {
                value = defaultValue;
                pluginSettings.SetValueUInt32(name, value);
            }

            return value;
        }

        public uint ExposureTolerance
        {
            get => GetSettingUInt32(nameof(ExposureTolerance), Properties.Settings.Default.ExposureTolerance);
            set
            {
                if (ExposureTolerance != value)
                {
                    pluginSettings.SetValueUInt32(nameof(ExposureTolerance), Math.Clamp(value, 10, 50));
                    RaisePropertyChanged();
                }
            }
        }

        public uint FovTolerance
        {
            get => GetSettingUInt32(nameof(FovTolerance), Properties.Settings.Default.FovTolerance);
            set
            {
                if (FovTolerance != value)
                {
                    pluginSettings.SetValueUInt32(nameof(FovTolerance), Math.Clamp(value, 5, 50));
                    RaisePropertyChanged();
                }
            }
        }

        public uint ReferencePercentile
        {
            get => GetSettingUInt32(nameof(ReferencePercentile), Properties.Settings.Default.ReferencePercentile);
            set
            {
                if (ReferencePercentile != value)
                {
                    pluginSettings.SetValueUInt32(nameof(ReferencePercentile), Math.Clamp(value, 50, 99));
                    RaisePropertyChanged();
                }
            }
        }

        public uint InitialSamples
        {
            get => GetSettingUInt32(nameof(InitialSamples), Properties.Settings.Default.InitialSamples);
            set
            {
                if (InitialSamples != value)
                {
                    pluginSettings.SetValueUInt32(nameof(InitialSamples), Math.Clamp(value, 3, 50));
                    RaisePropertyChanged();
                }
            }
        }

        public ICommand ResetSettingsCommand => resetSettingsCommand ??= new DelegateCommand(_ => ResetSettings());

        public void ResetSettings()
        {
            try
            {
                Properties.Settings.Default.Reset();
                CoreUtil.SaveSettings(Properties.Settings.Default);

                pluginSettings.SetValueUInt32(nameof(ExposureTolerance), Properties.Settings.Default.ExposureTolerance);
                pluginSettings.SetValueUInt32(nameof(FovTolerance), Properties.Settings.Default.FovTolerance);
                pluginSettings.SetValueUInt32(nameof(ReferencePercentile), Properties.Settings.Default.ReferencePercentile);
                pluginSettings.SetValueUInt32(nameof(InitialSamples), Properties.Settings.Default.InitialSamples);

                RaisePropertyChanged(nameof(ExposureTolerance));
                RaisePropertyChanged(nameof(FovTolerance));
                RaisePropertyChanged(nameof(ReferencePercentile));
                RaisePropertyChanged(nameof(InitialSamples));
            } catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private class DelegateCommand : ICommand
        {
            private readonly Action<object> execute;
            private readonly Func<object, bool> canExecute;

            public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
                this.canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true;

            public void Execute(object parameter) => execute(parameter);

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }
    }
}