using AudioBlocks.App.Audio;
using AudioBlocks.App.Controls;
using AudioBlocks.App.Effects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioBlocks.App
{
    public partial class MainWindow : Window
    {
        private readonly AudioEngine engine;
        private readonly DispatcherTimer vuTimer;
        private AudioSettingsWindow? settingsWindow;
        private int selectedEffectIndex = -1;
        private bool libraryOpen;

        public MainWindow()
        {
            InitializeComponent();
            engine = new AudioEngine();

            // ===== LOG =====
            engine.OnLog += msg => Dispatcher.UIThread.Post(() =>
            {
                string cur = LogBox.Text ?? "";
                string next = cur + (string.IsNullOrEmpty(cur) ? "" : Environment.NewLine) + msg;
                if (next.Length > 2000) next = next[^2000..];
                LogBox.Text = next;
                LogBox.CaretIndex = next.Length;
            });

            // ===== MONITORING STATE =====
            engine.OnMonitoringChanged += running => Dispatcher.UIThread.Post(() =>
            {
                ToggleMonitoringButton.Content = running ? "⏹  Stop Monitoring" : "▶  Start Monitoring";
                StatusLabel.Text = running ? $"Monitoring — {engine.CalculatedLatencyMs:0.0} ms" : "Ready";
            });

            // ===== VU TIMER =====
            vuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            vuTimer.Tick += (_, _) =>
            {
                VuMeter.Value = engine.Level;
                CpuLabel.Text = engine.CpuOverload ? $"⚠ {engine.SmoothedProcessingMs:F1}ms" : $"{engine.SmoothedProcessingMs:F1}ms";
                LatencySmallLabel.Text = $"{engine.CalculatedLatencyMs:F1}ms";

                // Recording time
                if (engine.Recorder.IsRecording || engine.Recorder.HasRecording)
                {
                    double sec = engine.Recorder.RecordedDurationMs(engine.SampleRate) / 1000.0;
                    int min = (int)(sec / 60);
                    RecordTimeLabel.Text = $"{min:00}:{sec % 60:00.0}";
                }

                // Playback progress
                if (engine.Recorder.IsPlaying)
                    PlaybackProgress.Value = engine.Recorder.PlaybackProgress;
            };
            vuTimer.Start();

            // ===== MASTER VOLUME =====
            MasterSlider.Value = 1.0;
            MasterSlider.ValueChanged += (_, e) =>
            {
                float vol = (float)e.NewValue;
                engine.Effects.MasterVolume = vol;
                float db = vol <= 0 ? -96f : 20f * MathF.Log10(vol);
                MasterValueLabel.Text = MathF.Abs(db) < 0.05f ? "0 dB" : $"{db:+0.0;-0.0} dB";
            };

            // ===== MONITORING =====
            ToggleMonitoringButton.Click += (_, _) =>
            {
                if (engine.IsMonitoring) engine.StopMonitoring();
                else
                {
                    if (engine.Driver != AudioDriver.ASIO && (engine.InputDevice == null || engine.OutputDevice == null))
                    { StatusLabel.Text = "Configure audio devices first"; return; }
                    engine.StartMonitoring();
                }
            };

            OpenAudioSettingsButton.Click += (_, _) =>
            {
                if (settingsWindow == null) { settingsWindow = new AudioSettingsWindow(engine); settingsWindow.Closed += (_, _) => settingsWindow = null; settingsWindow.Show(); }
                else settingsWindow.Activate();
            };

            // ===== LIBRARY =====
            ToggleLibraryButton.Click += (_, _) => ToggleLibrary();
            AddGainBtn.Click += (_, _) => AddEffect(new GainEffect());
            AddCompressorBtn.Click += (_, _) => AddEffect(new CompressorEffect());
            AddGateBtn.Click += (_, _) => AddEffect(new NoiseGateEffect());
            AddEqBtn.Click += (_, _) => AddEffect(new EqEffect());
            AddDistortionBtn.Click += (_, _) => AddEffect(new DistortionEffect());
            AddDelayBtn.Click += (_, _) => AddEffect(new DelayEffect());
            AddReverbBtn.Click += (_, _) => AddEffect(new ReverbEffect());
            AddChorusBtn.Click += (_, _) => AddEffect(new ChorusEffect());

            // ===== PRESETS =====
            PresetCleanBtn.Click += (_, _) => ApplyPreset(Preset.Clean);
            PresetCrunchBtn.Click += (_, _) => ApplyPreset(Preset.Crunch);
            PresetLeadBtn.Click += (_, _) => ApplyPreset(Preset.Lead);
            PresetAmbientBtn.Click += (_, _) => ApplyPreset(Preset.Ambient);

            // ===== REORDER =====
            MoveUpButton.Click += (_, _) => { if (selectedEffectIndex > 0) { engine.Effects.MoveEffect(selectedEffectIndex, selectedEffectIndex - 1); selectedEffectIndex--; } };
            MoveDownButton.Click += (_, _) => { if (selectedEffectIndex >= 0 && selectedEffectIndex < engine.Effects.Count - 1) { engine.Effects.MoveEffect(selectedEffectIndex, selectedEffectIndex + 1); selectedEffectIndex++; } };
            RebuildGraphButton.Click += (_, _) => { engine.RebuildAudioGraph(); StatusLabel.Text = "Graph rebuilt"; };

            // ===== TRANSPORT =====
            RecordButton.Click += (_, _) => OnRecordClick();
            PlayButton.Click += (_, _) => OnPlayClick();
            StopButton.Click += (_, _) => OnStopClick();
            ExportButton.Click += async (_, _) => await OnExportClick();

            // ===== METRONOME =====
            MetronomeToggle.IsCheckedChanged += (_, _) =>
            {
                engine.Metronome.Enabled = MetronomeToggle.IsChecked == true;
                StatusLabel.Text = engine.Metronome.Enabled ? $"Metronome ON — {engine.Metronome.BPM} BPM" : "Metronome OFF";
            };
            BpmDownButton.Click += (_, _) => { engine.Metronome.BPM -= 5; BpmLabel.Text = $"{engine.Metronome.BPM} BPM"; };
            BpmUpButton.Click += (_, _) => { engine.Metronome.BPM += 5; BpmLabel.Text = $"{engine.Metronome.BPM} BPM"; };

            engine.Metronome.OnBeat += beat => Dispatcher.UIThread.Post(() =>
            {
                int total = engine.Metronome.BeatsPerBar;
                char[] dots = new char[total * 2 - 1];
                for (int i = 0; i < total; i++)
                {
                    if (i > 0) dots[i * 2 - 1] = ' ';
                    dots[i * 2] = (i == beat - 1) ? '●' : '○';
                }
                BeatIndicator.Text = new string(dots);
            });

            // ===== RECORDER STATE =====
            engine.Recorder.OnStateChanged += () => Dispatcher.UIThread.Post(SyncTransportUI);

            // ===== EFFECTS =====
            engine.Effects.OnEffectsChanged += () => Dispatcher.UIThread.Post(UpdateEffectsPanel);
            UpdateEffectsPanel();
            SyncTransportUI();
        }

        // ===== TRANSPORT ACTIONS =====

        private void OnRecordClick()
        {
            if (engine.Recorder.IsRecording)
            {
                engine.Recorder.StopRecording();
            }
            else
            {
                if (!engine.IsMonitoring)
                {
                    StatusLabel.Text = "Start monitoring first";
                    return;
                }
                engine.Recorder.StartRecording();
            }
        }

        private void OnPlayClick()
        {
            if (engine.Recorder.IsPlaying)
            {
                engine.Recorder.StopPlayback();
            }
            else
            {
                if (!engine.IsMonitoring)
                {
                    StatusLabel.Text = "Start monitoring first";
                    return;
                }
                engine.Recorder.StartPlayback();
            }
        }

        private void OnStopClick()
        {
            if (engine.Recorder.IsRecording) engine.Recorder.StopRecording();
            if (engine.Recorder.IsPlaying) engine.Recorder.StopPlayback();
            PlaybackProgress.Value = 0;
        }

        private async System.Threading.Tasks.Task OnExportClick()
        {
            if (!engine.Recorder.HasRecording) { StatusLabel.Text = "Nothing to export"; return; }

            var sp = StorageProvider;
            var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Recording",
                DefaultExtension = "wav",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("WAV 32-bit float") { Patterns = new[] { "*.wav" } },
                    new FilePickerFileType("WAV 16-bit PCM") { Patterns = new[] { "*.wav" } }
                },
                SuggestedFileName = $"AudioBlocks_{DateTime.Now:yyyyMMdd_HHmmss}"
            });

            if (file == null) return;

            string path = file.Path.LocalPath;
            bool is16 = file.Name?.Contains("16") == true;

            bool ok = is16
                ? engine.Recorder.ExportWav16(path, engine.SampleRate)
                : engine.Recorder.ExportWav(path, engine.SampleRate);

            StatusLabel.Text = ok ? $"Exported → {path}" : "Export failed";
        }

        private void SyncTransportUI()
        {
            bool rec = engine.Recorder.IsRecording;
            bool play = engine.Recorder.IsPlaying;
            bool hasRec = engine.Recorder.HasRecording;

            RecordButton.Foreground = rec ? Brushes.Red : new SolidColorBrush(Color.Parse("#FF6B6B"));
            RecordButton.Content = rec ? "⏺" : "⏺";
            PlayButton.Content = play ? "⏸" : "▶";
            PlayButton.IsEnabled = hasRec || play;
            ExportButton.IsEnabled = hasRec && !rec;

            if (!play) PlaybackProgress.Value = 0;
            if (!rec && !play && !hasRec) RecordTimeLabel.Text = "00:00.0";
        }

        // ===== LIBRARY =====

        private void ToggleLibrary()
        {
            libraryOpen = !libraryOpen;
            LibraryPanel.IsVisible = libraryOpen;
            ToggleLibraryButton.Content = libraryOpen ? "🎛  Effects Library ‹" : "🎛  Effects Library ›";
        }

        private void AddEffect(IAudioEffect effect)
        {
            engine.Effects.AddEffect(effect);
            StatusLabel.Text = $"{effect.Name} added";
        }

        // ===== PRESETS =====
        private enum Preset { Clean, Crunch, Lead, Ambient }

        private void ApplyPreset(Preset preset)
        {
            foreach (var fx in engine.Effects.GetAllEffects())
                engine.Effects.RemoveEffect(fx);

            switch (preset)
            {
                case Preset.Clean:
                    engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.01f });
                    engine.Effects.AddEffect(new EqEffect { Low = -0.2f, Mid = 0.1f, High = 0.2f });
                    engine.Effects.AddEffect(new CompressorEffect { Threshold = 0.4f, Ratio = 3f, Makeup = 1.3f });
                    break;
                case Preset.Crunch:
                    engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.02f });
                    engine.Effects.AddEffect(new GainEffect { Gain = 1.5f });
                    engine.Effects.AddEffect(new DistortionEffect { Drive = 0.35f, Tone = 0.5f, Mix = 1f, Level = 0.65f });
                    engine.Effects.AddEffect(new EqEffect { Low = 0.1f, Mid = 0.2f, High = -0.1f });
                    break;
                case Preset.Lead:
                    engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.03f });
                    engine.Effects.AddEffect(new GainEffect { Gain = 2.0f });
                    engine.Effects.AddEffect(new DistortionEffect { Drive = 0.6f, Tone = 0.65f, Mix = 1f, Level = 0.5f });
                    engine.Effects.AddEffect(new EqEffect { Low = -0.1f, Mid = 0.3f, High = 0.1f });
                    engine.Effects.AddEffect(new DelayEffect { Time = 0.35f, Feedback = 0.3f, Mix = 0.2f });
                    engine.Effects.AddEffect(new ReverbEffect { Mix = 0.15f, Decay = 0.4f, Damping = 0.5f });
                    break;
                case Preset.Ambient:
                    engine.Effects.AddEffect(new EqEffect { Low = 0.2f, Mid = -0.2f, High = 0.3f });
                    engine.Effects.AddEffect(new ChorusEffect { Rate = 0.3f, Depth = 0.6f, Mix = 0.4f });
                    engine.Effects.AddEffect(new DelayEffect { Time = 0.6f, Feedback = 0.5f, Mix = 0.4f });
                    engine.Effects.AddEffect(new ReverbEffect { Mix = 0.5f, Decay = 0.8f, Damping = 0.6f });
                    break;
            }
            StatusLabel.Text = $"Preset: {preset}";
        }

        // ===== EFFECTS PANEL WITH KNOBS =====
        private void UpdateEffectsPanel()
        {
            EffectsPanel.Children.Clear();
            var all = engine.Effects.GetAllEffects();
            EmptyChainHint.IsVisible = all.Count == 0;

            for (int idx = 0; idx < all.Count; idx++)
            {
                var effect = all[idx];
                int ci = idx;
                bool sel = ci == selectedEffectIndex;

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.Parse(sel ? "#2A3040" : "#22252B")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10),
                    BorderBrush = sel ? new SolidColorBrush(Color.Parse("#FF6B6B")) : null,
                    BorderThickness = new Thickness(sel ? 1 : 0),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                card.PointerPressed += (_, _) => { selectedEffectIndex = ci; UpdateEffectsPanel(); };

                var content = new StackPanel { Spacing = 8 };

                var header = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto") };
                var chk = new CheckBox { IsChecked = effect.Enabled, [Grid.ColumnProperty] = 0 };
                chk.IsCheckedChanged += (_, _) => effect.Enabled = chk.IsChecked == true;
                header.Children.Add(chk);
                header.Children.Add(new TextBlock { Text = $"{idx + 1}. {effect.Name}", Foreground = Brushes.White, FontWeight = FontWeight.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), [Grid.ColumnProperty] = 1 });
                var rm = new Button { Content = "✕", FontSize = 10, Width = 24, Height = 24, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 2 };
                rm.Click += (_, _) => { engine.Effects.RemoveEffect(effect); if (selectedEffectIndex >= engine.Effects.Count) selectedEffectIndex = engine.Effects.Count - 1; };
                header.Children.Add(rm);
                content.Children.Add(header);

                var knobs = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

                if (effect is GainEffect g)
                    knobs.Children.Add(MakeKnob("Boost", 0, 4, g.Gain, v => g.Gain = v, v => $"{(v <= 0 ? -96 : 20 * Math.Log10(v)):+0.0;-0.0}dB", "#4DD0E1"));
                else if (effect is DistortionEffect d)
                { knobs.Children.Add(MakeKnob("Drive", 0, 1, d.Drive, v => d.Drive = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Tone", 0, 1, d.Tone, v => d.Tone = v, v => $"{v * 100:0}%", "#FFB74D")); knobs.Children.Add(MakeKnob("Mix", 0, 1, d.Mix, v => d.Mix = v, v => $"{v * 100:0}%", "#81C784")); knobs.Children.Add(MakeKnob("Level", 0, 1, d.Level, v => d.Level = v, v => $"{v * 100:0}%", "#B388FF")); }
                else if (effect is ReverbEffect r)
                { knobs.Children.Add(MakeKnob("Mix", 0, 1, r.Mix, v => r.Mix = v, v => $"{v * 100:0}%", "#4DD0E1")); knobs.Children.Add(MakeKnob("Decay", 0, 1, r.Decay, v => r.Decay = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Damp", 0, 1, r.Damping, v => r.Damping = v, v => $"{v * 100:0}%", "#FFB74D")); }
                else if (effect is CompressorEffect c)
                { knobs.Children.Add(MakeKnob("Thresh", 0, 1, c.Threshold, v => c.Threshold = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Ratio", 1, 20, c.Ratio, v => c.Ratio = v, v => $"{v:0.0}:1", "#4DD0E1")); knobs.Children.Add(MakeKnob("Attack", 0, 1, c.Attack, v => c.Attack = v, v => $"{v * 100:0}%", "#81C784")); knobs.Children.Add(MakeKnob("Release", 0, 1, c.Release, v => c.Release = v, v => $"{v * 100:0}%", "#FFB74D")); knobs.Children.Add(MakeKnob("Makeup", 0.5, 3, c.Makeup, v => c.Makeup = v, v => $"{(v <= 0 ? -96 : 20 * Math.Log10(v)):+0.0;-0.0}dB", "#B388FF")); }
                else if (effect is NoiseGateEffect ng)
                { knobs.Children.Add(MakeKnob("Thresh", 0, 0.2, ng.Threshold, v => ng.Threshold = v, v => $"{v * 100:0.0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Attack", 0, 1, ng.Attack, v => ng.Attack = v, v => $"{v * 100:0}%", "#81C784")); knobs.Children.Add(MakeKnob("Release", 0, 1, ng.Release, v => ng.Release = v, v => $"{v * 100:0}%", "#FFB74D")); }
                else if (effect is DelayEffect dl)
                { knobs.Children.Add(MakeKnob("Time", 0, 1, dl.Time, v => dl.Time = v, v => $"{50 + v * 950:0}ms", "#4DD0E1")); knobs.Children.Add(MakeKnob("Feedback", 0, 1, dl.Feedback, v => dl.Feedback = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Mix", 0, 1, dl.Mix, v => dl.Mix = v, v => $"{v * 100:0}%", "#81C784")); }
                else if (effect is ChorusEffect ch)
                { knobs.Children.Add(MakeKnob("Rate", 0, 1, ch.Rate, v => ch.Rate = v, v => $"{0.1 + v * 4.9:0.0}Hz", "#B388FF")); knobs.Children.Add(MakeKnob("Depth", 0, 1, ch.Depth, v => ch.Depth = v, v => $"{v * 100:0}%", "#4DD0E1")); knobs.Children.Add(MakeKnob("Mix", 0, 1, ch.Mix, v => ch.Mix = v, v => $"{v * 100:0}%", "#81C784")); }
                else if (effect is EqEffect eq)
                { knobs.Children.Add(MakeKnob("Low", -1, 1, eq.Low, v => eq.Low = v, v => $"{v:+0.0;-0.0}", "#FF6B6B")); knobs.Children.Add(MakeKnob("Mid", -1, 1, eq.Mid, v => eq.Mid = v, v => $"{v:+0.0;-0.0}", "#FFB74D")); knobs.Children.Add(MakeKnob("High", -1, 1, eq.High, v => eq.High = v, v => $"{v:+0.0;-0.0}", "#4DD0E1")); }

                content.Children.Add(knobs);
                card.Child = content;
                EffectsPanel.Children.Add(card);
            }
        }

        private static KnobControl MakeKnob(string label, double min, double max, double value, Action<float> onChange, Func<double, string> fmt, string color)
        {
            var k = new KnobControl { Width = 56, Height = 72, Minimum = min, Maximum = max, Value = value, Label = label, DisplayValue = fmt(value), KnobColor = new SolidColorBrush(Color.Parse(color)), Margin = new Thickness(6, 0) };
            k.ValueChanged += (_, v) => { onChange((float)v); k.DisplayValue = fmt(v); };
            return k;
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            engine.Recorder.StopRecording();
            engine.Recorder.StopPlayback();
            engine.StopAsioTest();
            engine.StopMonitoring();
            settingsWindow?.Close();
            settingsWindow = null;
        }
    }
}
