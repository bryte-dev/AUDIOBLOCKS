using AudioBlocks.App.Audio;
using AudioBlocks.App.Controls;
using AudioBlocks.App.Effects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS0618 // DataObject/DataFormats.Text/DragDrop.DoDragDrop are correct for Avalonia 11.x

namespace AudioBlocks.App
{
    public partial class MainWindow : Window
    {
        private readonly AudioEngine engine;
        private readonly DispatcherTimer vuTimer;
        private AudioSettingsWindow? settingsWindow;
        private EffectsLibraryWindow? libraryWindow;
        private int selectedEffectIndex = -1;
        private bool libraryOpen;

        private static readonly Geometry PlayIcon = Geometry.Parse("M8,5V19l11-7Z");
        private static readonly Geometry PauseIcon = Geometry.Parse("M6,19h4V5H6ZM14,5V19h4V5Z");

        private int pendingDragIndex = -1;
        private Point? dragPressPoint;
        private const double DragThreshold = 6.0;
        private const string DragPrefix = "AudioBlocks:Effect:";

        public MainWindow()
        {
            InitializeComponent();
            engine = new AudioEngine();

            engine.OnLog += msg => Dispatcher.UIThread.Post(() =>
            {
                string cur = LogBox.Text ?? "";
                string next = cur + (string.IsNullOrEmpty(cur) ? "" : Environment.NewLine) + msg;
                if (next.Length > 2000) next = next[^2000..];
                LogBox.Text = next;
                LogBox.CaretIndex = next.Length;
            });

            engine.OnMonitoringChanged += running => Dispatcher.UIThread.Post(() =>
            {
                if (ToggleMonitoringButton.Content is StackPanel sp)
                    foreach (var child in sp.Children)
                        if (child is TextBlock tb) { tb.Text = running ? "Stop Monitoring" : "Start Monitoring"; break; }
                StatusLabel.Text = running ? $"Monitoring -- {engine.CalculatedLatencyMs:0.0} ms" : "Ready";
            });

            vuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            vuTimer.Tick += (_, _) =>
            {
                VuMeter.Level = engine.Level;
                VuMeter.Peak = engine.PeakLevel;
                VuMeter.Clipping = engine.PeakLevel > 0.98;

                float peakDb = engine.PeakLevel <= 0.0001f ? -96f : 20f * MathF.Log10(engine.PeakLevel);
                PeakDbLabel.Text = peakDb < -60f ? "-inf dB" : $"{peakDb:0.0} dB";
                PeakDbLabel.Foreground = engine.PeakLevel > 0.98
                    ? new SolidColorBrush(Color.Parse("#EF4444"))
                    : engine.PeakLevel > 0.5
                        ? new SolidColorBrush(Color.Parse("#F59E0B"))
                        : GetThemeBrush("DimText", "#6B7280");

                CpuLabel.Text = engine.CpuOverload ? $"! {engine.SmoothedProcessingMs:F1}ms" : $"{engine.SmoothedProcessingMs:F1}ms";
                LatencySmallLabel.Text = $"{engine.CalculatedLatencyMs:F1}ms";

                if (engine.Recorder.IsRecording || engine.Recorder.HasRecording)
                {
                    double sec = engine.Recorder.RecordedDurationMs(engine.SampleRate) / 1000.0;
                    RecordTimeLabel.Text = $"{(int)(sec / 60):00}:{sec % 60:00.0}";
                }
                if (engine.Recorder.IsPlaying)
                    PlaybackProgress.Value = engine.Recorder.PlaybackProgress;

                UpdateEffectMeters();
            };
            vuTimer.Start();

            MasterFader.Value = 1.0;
            MasterFader.ValueChanged += (_, v) =>
            {
                float vol = (float)v;
                engine.Effects.MasterVolume = vol;
                float db = vol <= 0 ? -96f : 20f * MathF.Log10(vol);
                MasterValueLabel.Text = MathF.Abs(db) < 0.05f ? "0 dB" : $"{db:+0.0;-0.0} dB";
            };

            MetronomeVolumeFader.Value = 0.5;
            MetronomeVolumeFader.ValueChanged += (_, v) =>
            {
                engine.Metronome.Volume = (float)v;
                MetronomeVolLabel.Text = $"{(int)(v * 100)}%";
            };

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

            ToggleLibraryButton.Click += (_, _) => ToggleLibrary();
            ToggleLibraryButton.DoubleTapped += (_, _) => OpenLibraryWindow();

            AddGainBtn.Click += (_, _) => AddEffect(new GainEffect());
            AddCompressorBtn.Click += (_, _) => AddEffect(new CompressorEffect());
            AddGateBtn.Click += (_, _) => AddEffect(new NoiseGateEffect());
            AddEqBtn.Click += (_, _) => AddEffect(new EqEffect());
            AddDistortionBtn.Click += (_, _) => AddEffect(new DistortionEffect());
            AddDelayBtn.Click += (_, _) => AddEffect(new DelayEffect());
            AddReverbBtn.Click += (_, _) => AddEffect(new ReverbEffect());
            AddChorusBtn.Click += (_, _) => AddEffect(new ChorusEffect());
            AddFuzzBtn.Click += (_, _) => AddEffect(new FuzzEffect());
            AddGraphicEqBtn.Click += (_, _) => AddEffect(new GraphicEqEffect());

            PresetCleanBtn.Click += (_, _) => ApplyPreset(Preset.Clean);
            PresetCrunchBtn.Click += (_, _) => ApplyPreset(Preset.Crunch);
            PresetLeadBtn.Click += (_, _) => ApplyPreset(Preset.Lead);
            PresetAmbientBtn.Click += (_, _) => ApplyPreset(Preset.Ambient);

            MoveUpButton.Click += (_, _) => { if (selectedEffectIndex > 0) { engine.Effects.MoveEffect(selectedEffectIndex, selectedEffectIndex - 1); selectedEffectIndex--; } };
            MoveDownButton.Click += (_, _) => { if (selectedEffectIndex >= 0 && selectedEffectIndex < engine.Effects.Count - 1) { engine.Effects.MoveEffect(selectedEffectIndex, selectedEffectIndex + 1); selectedEffectIndex++; } };
            RebuildGraphButton.Click += (_, _) => { engine.RebuildAudioGraph(); StatusLabel.Text = "Graph rebuilt"; };

            RecordButton.Click += (_, _) => OnRecordClick();
            PlayButton.Click += (_, _) => OnPlayClick();
            StopButton.Click += (_, _) => OnStopClick();
            ExportButton.Click += async (_, _) => await OnExportClick();

            MetronomeToggle.IsCheckedChanged += (_, _) =>
            {
                engine.Metronome.Enabled = MetronomeToggle.IsChecked == true;
                StatusLabel.Text = engine.Metronome.Enabled ? $"Metronome ON -- {engine.Metronome.BPM} BPM" : "Metronome OFF";
            };
            BpmDownButton.Click += (_, _) => { engine.Metronome.BPM -= 5; BpmLabel.Text = $"{engine.Metronome.BPM}"; };
            BpmUpButton.Click += (_, _) => { engine.Metronome.BPM += 5; BpmLabel.Text = $"{engine.Metronome.BPM}"; };

            engine.Metronome.OnBeat += beat => Dispatcher.UIThread.Post(() =>
                BeatIndicator.Text = $"{beat}/{engine.Metronome.BeatsPerBar}");

            SetupDropZone();

            engine.Recorder.OnStateChanged += () => Dispatcher.UIThread.Post(SyncTransportUI);
            engine.Effects.OnEffectsChanged += () => Dispatcher.UIThread.Post(UpdateEffectsPanel);
            UpdateEffectsPanel();
            SyncTransportUI();
        }

        // =========================================================
        // DRAG & DROP — Avalonia 11.x API (DataObject + DataFormats.Text)
        // =========================================================

        private void SetupDropZone()
        {
            DragDrop.SetAllowDrop(DropZone, true);
            DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
            DropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }

        private void BeginPendingDrag(int index, PointerPressedEventArgs e, Control relativeTo)
        {
            pendingDragIndex = index;
            dragPressPoint = e.GetPosition(relativeTo);
        }

        private async void TryStartDrag(PointerEventArgs e, Control relativeTo)
        {
            if (!dragPressPoint.HasValue || pendingDragIndex < 0) return;

            var pos = e.GetPosition(relativeTo);
            double dx = Math.Abs(pos.X - dragPressPoint.Value.X);
            double dy = Math.Abs(pos.Y - dragPressPoint.Value.Y);

            if (dx < DragThreshold && dy < DragThreshold) return;

            int idx = pendingDragIndex;
            dragPressPoint = null;
            pendingDragIndex = -1;

            var dataObject = new DataObject();
            dataObject.Set(DataFormats.Text, $"AudioBlocks:Reorder:{idx}");

            await DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);
        }

        private void CancelPendingDrag()
        {
            pendingDragIndex = -1;
            dragPressPoint = null;
        }

        private (bool isReorder, bool isNewEffect, string? effectType, int fromIndex) ParseDragData(IDataObject data)
        {
            // Try standard GetText first
            string? text = data.GetText();

            // Fallback: scan all available formats for a string value
            if (text == null)
            {
                foreach (var fmt in data.GetDataFormats())
                {
                    if (data.Get(fmt) is string s)
                    {
                        text = s;
                        break;
                    }
                }
            }

            // Cross-window fallback: use the static pending state from EffectsLibraryWindow
            if (text == null && EffectsLibraryWindow.PendingEffectType != null)
                text = $"{DragPrefix}{EffectsLibraryWindow.PendingEffectType}";

            if (text == null) return (false, false, null, -1);

            if (text.StartsWith("AudioBlocks:Reorder:") &&
                int.TryParse(text.AsSpan("AudioBlocks:Reorder:".Length), out int idx))
                return (true, false, null, idx);

            if (text.StartsWith(DragPrefix))
                return (false, true, text[DragPrefix.Length..], -1);

            return (false, false, null, -1);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            var (isReorder, isNewEffect, _, _) = ParseDragData(e.Data);

            if (isReorder)
                e.DragEffects = DragDropEffects.Move;
            else if (isNewEffect)
                e.DragEffects = DragDropEffects.Copy;
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Handled = true;

            var pos = e.GetPosition(EffectsPanel);
            int targetIdx = GetDropIndex(pos.Y);
            HighlightDropTarget(targetIdx, isNewEffect);
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            ClearDropHighlights();
            e.Handled = true;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            ClearDropHighlights();
            e.Handled = true;

            var pos = e.GetPosition(EffectsPanel);
            int toIdx = GetDropIndex(pos.Y);

            var (isReorder, isNewEffect, effectType, fromIdx) = ParseDragData(e.Data);

            if (isReorder && fromIdx >= 0 && fromIdx < engine.Effects.Count)
            {
                if (toIdx != fromIdx)
                {
                    toIdx = Math.Clamp(toIdx, 0, engine.Effects.Count - 1);
                    engine.Effects.MoveEffect(fromIdx, toIdx);
                    selectedEffectIndex = toIdx;
                    StatusLabel.Text = $"Effect moved to position {toIdx + 1}";
                }
                return;
            }

            if (isNewEffect && effectType != null)
            {
                var newEffect = EffectsLibraryWindow.CreateEffect(effectType);
                if (newEffect != null)
                {
                    int insertAt = Math.Clamp(toIdx, 0, engine.Effects.Count);
                    engine.Effects.InsertEffect(insertAt, newEffect);
                    selectedEffectIndex = insertAt;
                    StatusLabel.Text = $"{newEffect.Name} added at position {insertAt + 1}";
                }
            }
        }

        // =========================================================
        // EFFECTS PANEL — AudioBlocks Identity
        // =========================================================

        /// <summary>Returns (accentColor, glowColor, categoryLabel) for the logo color scheme.</summary>
        private static (string accent, string glow, string label) GetBlockCategory(IAudioEffect effect) => effect switch
        {
            GainEffect or CompressorEffect or NoiseGateEffect => ("#7CB342", "#337CB342", "DYNAMICS"),
            EqEffect or DistortionEffect or FuzzEffect or GraphicEqEffect => ("#26C6DA", "#3326C6DA", "TONE"),
            DelayEffect or ReverbEffect or ChorusEffect => ("#FFB300", "#33FFB300", "TIME"),
            _ => ("#A0A6B0", "#33A0A6B0", "FX")
        };

        private IBrush GetThemeBrush(string key, string fallbackColor)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush)
            {
                return brush;
            }
            return new SolidColorBrush(Color.Parse(fallbackColor));
        }

        private Control CreateFlowTerminal(string label, bool isInput)
        {
            var color = isInput ? "#7CB342" : "#FF6B6B";
            var icon = isInput
                ? "M12,1C7.03,1,3,5.03,3,10v4c0,1.66,1.34,3,3,3h1V10H5c0-3.87,3.13-7,7-7s7,3.13,7,7v7h-2V10h2v4c0,1.66-1.34,3-3,3h-1"
                : "M14,3.23v2.06c2.89,.86,5,3.54,5,6.71s-2.11,5.85-5,6.71v2.06c4.01-.91,7-4.49,7-8.77s-2.99-7.86-7-8.77Zm-4,1.77H6C4.9,5,4,5.9,4,7v10c0,1.1,.9,2,2,2h4l5,5V0Z";

            return new Border
            {
                Background = new SolidColorBrush(Color.Parse(color), 0.08),
                BorderBrush = new SolidColorBrush(Color.Parse(color), 0.3),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new PathIcon { Data = Geometry.Parse(icon), Width = 12, Height = 12,
                            Foreground = new SolidColorBrush(Color.Parse(color)) },
                        new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse(color)),
                            VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };
        }

        private Control CreateSignalConnector()
        {
            var lineBrush = GetThemeBrush("SignalLine", "#2E3440");
            var dotBrush = GetThemeBrush("SignalDot", "#4DD0E1");

            var grid = new Grid
            {
                Height = 22,
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tag = "connector"
            };

            grid.Children.Add(new Rectangle
            {
                Width = 2,
                Fill = lineBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            grid.Children.Add(new Ellipse
            {
                Width = 7, Height = 7,
                Fill = dotBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "signal-dot"
            });

            return grid;
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int index, string catColor)) continue;
                bool sel = index == selectedEffectIndex;
                var catBrush = new SolidColorBrush(Color.Parse(catColor));

                b.Background = GetThemeBrush(sel ? "CardSelected" : "CardDefault", sel ? "#2A3040" : "#1E2128");
                b.BorderBrush = sel ? catBrush : null;
                b.BorderThickness = new Thickness(sel ? 1.5 : 0);
                b.BoxShadow = sel ? BoxShadows.Parse($"0 0 14 0 {catColor}55") : default;
            }
        }

        private void UpdateEffectsPanel()
        {
            EffectsPanel.Children.Clear();
            var all = engine.Effects.GetAllEffects();
            EmptyChainHint.IsVisible = all.Count == 0;

            if (all.Count == 0) return;

            // ── INPUT terminal ──
            EffectsPanel.Children.Add(CreateFlowTerminal("INPUT", true));
            EffectsPanel.Children.Add(CreateSignalConnector());

            for (int idx = 0; idx < all.Count; idx++)
            {
                var effect = all[idx];
                int ci = idx;
                bool sel = ci == selectedEffectIndex;
                var (catColor, catGlow, catLabel) = GetBlockCategory(effect);
                var catBrush = new SolidColorBrush(Color.Parse(catColor));

                // ── BLOCK CARD ──
                var card = new Border
                {
                    Background = GetThemeBrush(sel ? "CardSelected" : "CardDefault", sel ? "#2A3040" : "#1E2128"),
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    BorderBrush = sel ? catBrush : null,
                    BorderThickness = new Thickness(sel ? 1.5 : 0),
                    BoxShadow = sel ? BoxShadows.Parse($"0 0 14 0 {catColor}55") : default,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = (ci, catColor) // Store index + category color
                };

                card.PointerPressed += (_, e) =>
                {
                    selectedEffectIndex = ci;
                    UpdateSelectionVisuals();
                    if (e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
                        BeginPendingDrag(ci, e, card);
                };
                card.PointerMoved += (_, e) => TryStartDrag(e, card);
                card.PointerReleased += (_, _) => CancelPendingDrag();

                // Outer grid: [accent bar 4px] [content]
                var blockGrid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("4,*") };

                // Category accent bar (left stripe)
                blockGrid.Children.Add(new Border
                {
                    Background = catBrush,
                    CornerRadius = new CornerRadius(10, 0, 0, 10),
                    [Grid.ColumnProperty] = 0
                });

                // Content panel
                var content = new StackPanel
                {
                    Spacing = 8,
                    Margin = new Thickness(10, 10, 12, 10),
                    [Grid.ColumnProperty] = 1
                };

                // ── HEADER ──
                var header = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,Auto,*,Auto") };

                // Drag handle
                header.Children.Add(new PathIcon
                {
                    Width = 10, Height = 14,
                    Foreground = GetThemeBrush("DimText", "#6B7280"),
                    Data = Geometry.Parse("M4,2A2,2,0,1,1,2,4,2,2,0,0,1,4,2Zm6,0A2,2,0,1,1,8,4,2,2,0,0,1,10,2ZM4,8A2,2,0,1,1,2,10,2,2,0,0,1,4,8Zm6,0A2,2,0,1,1,8,10,2,2,0,0,1,10,8ZM4,14a2,2,0,1,1-2,2A2,2,0,0,1,4,14Zm6,0a2,2,0,1,1-2,2A2,2,0,0,1,10,14Z"),
                    Cursor = new Cursor(StandardCursorType.DragMove),
                    Margin = new Thickness(0, 0, 6, 0),
                    [Grid.ColumnProperty] = 0
                });

                // Checkbox
                var chk = new CheckBox { IsChecked = effect.Enabled, [Grid.ColumnProperty] = 1 };
                chk.IsCheckedChanged += (_, _) => effect.Enabled = chk.IsChecked == true;
                header.Children.Add(chk);

                // Category badge (colored dot + label)
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.Parse(catColor), 0.15),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 1),
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    [Grid.ColumnProperty] = 2,
                    Child = new TextBlock
                    {
                        Text = catLabel,
                        FontSize = 9,
                        FontWeight = FontWeight.Bold,
                        Foreground = catBrush
                    }
                };
                header.Children.Add(badge);

                // Effect name
                header.Children.Add(new TextBlock
                {
                    Text = $"{idx + 1}. {effect.Name}",
                    Foreground = GetThemeBrush("FgText", "#F0F0F0"),
                    FontWeight = FontWeight.SemiBold, FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    [Grid.ColumnProperty] = 3
                });

                // Remove button
                var rm = new Button
                {
                    Content = "✕", FontSize = 10, Width = 26, Height = 26,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    CornerRadius = new CornerRadius(6),
                    [Grid.ColumnProperty] = 4
                };
                rm.Click += (_, _) =>
                {
                    engine.Effects.RemoveEffect(effect);
                    if (selectedEffectIndex >= engine.Effects.Count) selectedEffectIndex = engine.Effects.Count - 1;
                };
                header.Children.Add(rm);
                content.Children.Add(header);

                // ── KNOBS ──
                var knobs = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

                if (effect is GainEffect g)
                    knobs.Children.Add(MakeKnob("Boost", 0, 4, g.Gain, v => g.Gain = v, v => $"{(v <= 0 ? -96 : 20 * Math.Log10(v)):+0.0;-0.0}dB", catColor));
                else if (effect is DistortionEffect d)
                { knobs.Children.Add(MakeKnob("Drive", 0, 1, d.Drive, v => d.Drive = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Tone", 0, 1, d.Tone, v => d.Tone = v, v => $"{v * 100:0}%", "#FFB74D")); knobs.Children.Add(MakeKnob("Mix", 0, 1, d.Mix, v => d.Mix = v, v => $"{v * 100:0}%", "#81C784")); knobs.Children.Add(MakeKnob("Level", 0, 1, d.Level, v => d.Level = v, v => $"{v * 100:0}%", "#B388FF")); }
                else if (effect is FuzzEffect fz)
                { knobs.Children.Add(MakeKnob("Fuzz", 0, 1, fz.Fuzz, v => fz.Fuzz = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Tone", 0, 1, fz.Tone, v => fz.Tone = v, v => $"{v * 100:0}%", "#FFB74D")); knobs.Children.Add(MakeKnob("Gate", 0, 0.1, fz.Gate, v => fz.Gate = v, v => $"{v * 1000:0.0}", "#81C784")); knobs.Children.Add(MakeKnob("Level", 0, 1, fz.Level, v => fz.Level = v, v => $"{v * 100:0}%", "#B388FF")); knobs.Children.Add(MakeKnob("Mix", 0, 1, fz.Mix, v => fz.Mix = v, v => $"{v * 100:0}%", catColor)); }
                else if (effect is ReverbEffect r)
                { knobs.Children.Add(MakeKnob("Mix", 0, 1, r.Mix, v => r.Mix = v, v => $"{v * 100:0}%", catColor)); knobs.Children.Add(MakeKnob("Decay", 0, 1, r.Decay, v => r.Decay = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Damp", 0, 1, r.Damping, v => r.Damping = v, v => $"{v * 100:0}%", "#FFB74D")); }
                else if (effect is CompressorEffect c)
                { knobs.Children.Add(MakeKnob("Thresh", 0, 1, c.Threshold, v => c.Threshold = v, v => $"{(v <= 0.001 ? -60 : 20 * Math.Log10(v)):0.0}dB", "#FF6B6B")); knobs.Children.Add(MakeKnob("Ratio", 1, 20, c.Ratio, v => c.Ratio = v, v => $"{v:0.0}:1", catColor)); knobs.Children.Add(MakeKnob("Atk", 0, 1, c.Attack, v => c.Attack = v, v => $"{0.1 + v * 99.9:0.0}ms", "#81C784")); knobs.Children.Add(MakeKnob("Rel", 0, 1, c.Release, v => c.Release = v, v => $"{5 + v * 995:0}ms", "#FFB74D")); knobs.Children.Add(MakeKnob("Makeup", 0.5, 3, c.Makeup, v => c.Makeup = v, v => $"{(v <= 0 ? -96 : 20 * Math.Log10(v)):+0.0;-0.0}dB", "#B388FF")); }
                else if (effect is NoiseGateEffect ng)
                { knobs.Children.Add(MakeKnob("Thresh", 0, 0.2, ng.Threshold, v => ng.Threshold = v, v => $"{v * 100:0.0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Atk", 0, 1, ng.Attack, v => ng.Attack = v, v => $"{0.05 + v * 10:0.0}ms", "#81C784")); knobs.Children.Add(MakeKnob("Rel", 0, 1, ng.Release, v => ng.Release = v, v => $"{5 + v * 500:0}ms", "#FFB74D")); }
                else if (effect is DelayEffect dl)
                { knobs.Children.Add(MakeKnob("Time", 0, 1, dl.Time, v => dl.Time = v, v => $"{50 + v * 950:0}ms", catColor)); knobs.Children.Add(MakeKnob("FB", 0, 1, dl.Feedback, v => dl.Feedback = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Mix", 0, 1, dl.Mix, v => dl.Mix = v, v => $"{v * 100:0}%", "#81C784")); }
                else if (effect is ChorusEffect ch)
                { knobs.Children.Add(MakeKnob("Rate", 0, 1, ch.Rate, v => ch.Rate = v, v => $"{0.1 + v * 4.9:0.0}Hz", "#B388FF")); knobs.Children.Add(MakeKnob("Depth", 0, 1, ch.Depth, v => ch.Depth = v, v => $"{v * 100:0}%", catColor)); knobs.Children.Add(MakeKnob("Mix", 0, 1, ch.Mix, v => ch.Mix = v, v => $"{v * 100:0}%", "#81C784")); }
                else if (effect is EqEffect eq)
                { knobs.Children.Add(MakeKnob("Low", -1, 1, eq.Low, v => eq.Low = v, v => $"{v:+0.0;-0.0}", "#FF6B6B")); knobs.Children.Add(MakeKnob("Mid", -1, 1, eq.Mid, v => eq.Mid = v, v => $"{v:+0.0;-0.0}", "#FFB74D")); knobs.Children.Add(MakeKnob("High", -1, 1, eq.High, v => eq.High = v, v => $"{v:+0.0;-0.0}", catColor)); }
                else if (effect is GraphicEqEffect geq)
                {
                    var eqControl = new Controls.GraphicEqControl
                    {
                        Height = 150,
                        Effect = geq,
                        Margin = new Thickness(0, 4)
                    };
                    eqControl.GainChanged += (band, db) => { };
                    content.Children.Add(eqControl);
                }

                content.Children.Add(knobs);

                // ── METERS ──
                if (effect is NoiseGateEffect ngM)
                {
                    var bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, Margin = new Thickness(0, 2, 0, 0) };
                    var lbl = new TextBlock { FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = GetThemeBrush("MutedText", "#A0A6B0") };
                    content.Children.Add(bar); content.Children.Add(lbl);
                    bar.Tag = ngM; lbl.Tag = ngM;
                }
                else if (effect is CompressorEffect cM)
                {
                    var lbl = new TextBlock { FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = GetThemeBrush("GrMeter", "#FF6B6B") };
                    content.Children.Add(lbl); lbl.Tag = cM;
                }
                else if (effect is EqEffect eqM)
                {
                    var panel = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*"), Height = 20, Margin = new Thickness(0, 2, 0, 0) };
                    var bL = new ProgressBar { Minimum = 0, Maximum = 0.5, Height = 6, Margin = new Thickness(1, 0), [Grid.ColumnProperty] = 0 };
                    var bM = new ProgressBar { Minimum = 0, Maximum = 0.5, Height = 6, Margin = new Thickness(1, 0), [Grid.ColumnProperty] = 1 };
                    var bH = new ProgressBar { Minimum = 0, Maximum = 0.5, Height = 6, Margin = new Thickness(1, 0), [Grid.ColumnProperty] = 2 };
                    panel.Children.Add(bL); panel.Children.Add(bM); panel.Children.Add(bH);
                    content.Children.Add(panel);
                    bL.Tag = ("eq_low", eqM); bM.Tag = ("eq_mid", eqM); bH.Tag = ("eq_high", eqM);
                }

                blockGrid.Children.Add(content);
                card.Child = blockGrid;
                EffectsPanel.Children.Add(card);

                // ── CONNECTOR between blocks ──
                if (idx < all.Count - 1)
                    EffectsPanel.Children.Add(CreateSignalConnector());
            }

            // ── OUTPUT terminal ──
            EffectsPanel.Children.Add(CreateSignalConnector());
            EffectsPanel.Children.Add(CreateFlowTerminal("OUTPUT", false));
        }

        private void UpdateEffectMeters()
        {
            foreach (var child in EffectsPanel.Children)
            {
                // Block cards have Tag = (int, string); navigate through Grid → StackPanel
                if (child is not Border b || b.Tag is not (int, string)) continue;
                if (b.Child is not Grid g) continue;
                var sp = g.Children.OfType<StackPanel>().FirstOrDefault();
                if (sp == null) continue;

                foreach (var ctrl in sp.Children)
                {
                    if (ctrl is ProgressBar pb && pb.Tag is NoiseGateEffect ngm) pb.Value = ngm.CurrentGateGain;
                    else if (ctrl is TextBlock tb && tb.Tag is NoiseGateEffect ngm2)
                        tb.Text = ngm2.CurrentGateGain > 0.95f ? "OPEN" : ngm2.CurrentGateGain < 0.05f ? "CLOSED" : $"GR: {ngm2.GainReductionDb:0.0} dB";
                    else if (ctrl is TextBlock tb2 && tb2.Tag is CompressorEffect cm)
                        tb2.Text = cm.GainReductionDb < -0.1f ? $"GR: {cm.GainReductionDb:0.0} dB" : "--";
                    else if (ctrl is Grid eg)
                        foreach (var ec in eg.Children)
                            if (ec is ProgressBar eb && eb.Tag is (string band, EqEffect eq))
                                eb.Value = band switch { "eq_low" => eq.LowLevel, "eq_mid" => eq.MidLevel, "eq_high" => eq.HighLevel, _ => 0 };
                }
            }

            // Pulse signal dots when monitoring
            if (engine.IsMonitoring && engine.Level > 0.001f)
            {
                double pulse = 0.4 + 0.6 * Math.Abs(Math.Sin(Environment.TickCount64 / 300.0));
                foreach (var child in EffectsPanel.Children)
                    if (child is Grid cg && cg.Tag is "connector")
                        foreach (var dot in cg.Children)
                            if (dot is Ellipse e && e.Tag is "signal-dot")
                                e.Opacity = pulse;
            }
            else
            {
                foreach (var child in EffectsPanel.Children)
                    if (child is Grid cg && cg.Tag is "connector")
                        foreach (var dot in cg.Children)
                            if (dot is Ellipse e && e.Tag is "signal-dot")
                                e.Opacity = 0.3;
            }
        }

        // ── Drop index calculation (skip non-block children) ──
        private int GetDropIndex(double y)
        {
            int blockIdx = 0;
            foreach (var child in EffectsPanel.Children)
            {
                if (child.Tag is not (int, string)) continue;
                double childMid = child.Bounds.Y + child.Bounds.Height / 2;
                if (y < childMid) return blockIdx;
                blockIdx++;
            }
            return blockIdx;
        }

        private void HighlightDropTarget(int targetIdx, bool isInsert)
        {
            int blockIdx = 0;
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int index, string catColor)) continue;

                if (blockIdx == targetIdx)
                {
                    b.BorderBrush = new SolidColorBrush(Color.Parse(isInsert ? "#4DD0E1" : catColor));
                    b.BorderThickness = new Thickness(0, 2.5, 0, 0);
                }
                else
                {
                    bool sel = index == selectedEffectIndex;
                    var selBrush = sel ? new SolidColorBrush(Color.Parse(catColor)) : null;
                    b.BorderBrush = selBrush;
                    b.BorderThickness = new Thickness(sel ? 1.5 : 0);
                }
                blockIdx++;
            }
        }

        private void ClearDropHighlights()
        {
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int index, string catColor)) continue;
                bool sel = index == selectedEffectIndex;
                var selBrush = sel ? new SolidColorBrush(Color.Parse(catColor)) : null;
                b.BorderBrush = selBrush;
                b.BorderThickness = new Thickness(sel ? 1.5 : 0);
                b.BoxShadow = sel ? BoxShadows.Parse($"0 0 14 0 {catColor}55") : default;
            }
        }

        // =========================================================
        // TRANSPORT
        // =========================================================

        private void OnRecordClick()
        {
            if (engine.Recorder.IsRecording) engine.Recorder.StopRecording();
            else
            {
                if (!engine.IsMonitoring) { StatusLabel.Text = "Start monitoring first"; return; }
                engine.Recorder.StartRecording();
            }
        }

        private void OnPlayClick()
        {
            if (engine.Recorder.IsPlaying) engine.Recorder.StopPlayback();
            else
            {
                if (!engine.IsMonitoring) { StatusLabel.Text = "Start monitoring first"; return; }
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
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
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
            bool ok = is16 ? engine.Recorder.ExportWav16(path, engine.SampleRate) : engine.Recorder.ExportWav(path, engine.SampleRate);
            StatusLabel.Text = ok ? $"Exported: {path}" : "Export failed";
        }

        private void SyncTransportUI()
        {
            bool rec = engine.Recorder.IsRecording;
            bool play = engine.Recorder.IsPlaying;
            bool hasRec = engine.Recorder.HasRecording;

            if (RecordButton.Content is Ellipse recCircle)
                recCircle.Fill = rec ? new SolidColorBrush(Color.Parse("#FF0000")) : new SolidColorBrush(Color.Parse("#EF4444"));
            if (PlayButton.Content is PathIcon playIcon)
                playIcon.Data = play ? PauseIcon : PlayIcon;

            PlayButton.IsEnabled = hasRec || play;
            ExportButton.IsEnabled = hasRec && !rec;
            if (!play) PlaybackProgress.Value = 0;
            if (!rec && !play && !hasRec) RecordTimeLabel.Text = "00:00.0";
        }

        // =========================================================
        // LIBRARY
        // =========================================================

        private void ToggleLibrary()
        {
            if (libraryWindow != null) { libraryWindow.Activate(); return; }
            libraryOpen = !libraryOpen;
            LibraryPanel.IsVisible = libraryOpen;
        }

        public void OpenLibraryWindow()
        {
            if (libraryWindow != null) { libraryWindow.Activate(); return; }
            libraryWindow = new EffectsLibraryWindow(engine);
            libraryWindow.Closed += (_, _) => libraryWindow = null;
            libraryWindow.Show(this);
            libraryOpen = false;
            LibraryPanel.IsVisible = false;
        }

        private void AddEffect(IAudioEffect effect)
        {
            engine.Effects.AddEffect(effect);
            StatusLabel.Text = $"{effect.Name} added";
        }

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

        private static KnobControl MakeKnob(string label, double min, double max, double value, Action<float> onChange, Func<double, string> fmt, string color)
        {
            var k = new KnobControl { Width = 68, Height = 88, Minimum = min, Maximum = max, Value = value, Label = label, DisplayValue = fmt(value), KnobColor = new SolidColorBrush(Color.Parse(color)), Margin = new Thickness(4, 2) };
            k.ValueChanged += (_, v) => { onChange((float)v); k.DisplayValue = fmt(v); };
            return k;
        }

        // =========================================================
        // TRANSPORT
        // =========================================================
    }
}

#pragma warning restore CS0618
