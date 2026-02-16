using AudioBlocks.App.Audio;
using AudioBlocks.App.Effects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using System.Linq;

namespace AudioBlocks.App
{
    public partial class MainWindow : Window
    {
        private readonly AudioEngine engine;
        private readonly DispatcherTimer vuTimer;

        private GainEffect gainEffect;
        private AudioSettingsWindow? settingsWindow;
        private EffectsLibraryWindow? effectsLibraryWindow;

        public MainWindow()
        {
            InitializeComponent();

            // ================= AUDIO ENGINE =================
            engine = new AudioEngine();

            // récupère l'effet Gain si présent
            gainEffect = engine.Effects.GetEffect<GainEffect>();

            // ================= VU METER =================
            vuTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            vuTimer.Tick += (_, _) =>
            {
                VuMeter.Value = engine.Level;
                CpuLabel.Text = engine.CpuOverload ? "CPU OVERLOAD" : "CPU OK";
            };
            vuTimer.Start();

            // ================= GAIN SLIDER =================
            if (gainEffect != null)
            {
                GainSlider.Value = gainEffect.Gain;
                GainSlider.ValueChanged += (_, e) =>
                {
                    gainEffect.Gain = (float)e.NewValue;
                };
            }

            // ================= MONITORING =================
            ToggleMonitoringButton.Click += (_, _) =>
            {
                if (engine.IsMonitoring)
                {
                    engine.StopMonitoring();
                    ToggleMonitoringButton.Content = "Monitoring OFF";
                    StatusLabel.Text = "Monitoring stopped";
                }
                else
                {
                    // Pour WASAPI on exige des MMDevice sélectionnés ; pour ASIO ce n'est pas nécessaire
                    if (engine.Driver != AudioDriver.ASIO && (engine.InputDevice == null || engine.OutputDevice == null))
                    {
                        StatusLabel.Text = "Please select input and output devices first";
                        return;
                    }

                    engine.StartMonitoring();
                    ToggleMonitoringButton.Content = "Monitoring ON";
                    StatusLabel.Text = $"Latency ≈ {engine.CalculatedLatencyMs:0.0} ms";
                }
            };

            // ================= AUDIO SETTINGS =================
            OpenAudioSettingsButton.Click += (_, _) =>
            {
                if (settingsWindow == null)
                {
                    settingsWindow = new AudioSettingsWindow(engine);
                    settingsWindow.Closed += (_, __) => settingsWindow = null;
                    settingsWindow.Show();
                }
                else
                {
                    settingsWindow.Activate();
                }
            };

            // ================= EFFECTS LIBRARY =================
            OpenEffectsLibraryButton.Click += (_, _) =>
            {
                if (effectsLibraryWindow == null)
                {
                    effectsLibraryWindow = new EffectsLibraryWindow(engine);
                    effectsLibraryWindow.Closed += (_, __) => effectsLibraryWindow = null;
                    effectsLibraryWindow.Show();
                }
                else
                {
                    effectsLibraryWindow.Activate();
                }
            };

            // ================= DYNAMIQUE DES EFFECTS =================
            engine.Effects.OnEffectsChanged += UpdateEffectsPanel;

            // initial update
            UpdateEffectsPanel();
        }

        private void UpdateEffectsPanel()
        {
            EffectsPanel.Children.Clear();

            foreach (var effect in engine.Effects.GetAllEffects())
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                // Enabled checkbox
                var enabledCheck = new CheckBox { IsChecked = effect.Enabled };
                enabledCheck.Checked += (_, __) => effect.Enabled = true;
                enabledCheck.Unchecked += (_, __) => effect.Enabled = false;
                stack.Children.Add(enabledCheck);

                // Nom de l'effet
                var nameText = new TextBlock { Text = effect.Name, Width = 80 };
                stack.Children.Add(nameText);

                // Contrôle spécifique Gain
                if (effect is GainEffect gain)
                {
                    var slider = new Slider { Minimum = 0, Maximum = 2, Value = gain.Gain, Width = 120 };
                    slider.ValueChanged += (_, e) => gain.Gain = (float)e.NewValue;
                    stack.Children.Add(slider);
                }

                // Contrôle Distortion
                if (effect is DistortionEffect dist)
                {
                    var slider = new Slider { Minimum = 0, Maximum = 1, Value = dist.Amount, Width = 120 };
                    slider.ValueChanged += (_, e) => dist.Amount = (float)e.NewValue;
                    stack.Children.Add(slider);
                }

                EffectsPanel.Children.Add(stack);
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            engine.StopMonitoring();
        }
    }
}
