using AudioBlocks.App.Audio;
using AudioBlocks.App.Controls;
using AudioBlocks.App.Effects;
using Avalonia;
using Avalonia.Animation;
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

#pragma warning disable CS0618

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

        // ── Drag visual feedback ──
        private int dragSourceIndex = -1;
        private int currentDropTargetIndex = -1;

        private static readonly SolidColorBrush DropIndicatorBrush = new(Color.Parse("#4DD0E1"));

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

            OpenSettingsButton.Click += (_, _) =>
            {
                if (settingsWindow == null)
                {
                    settingsWindow = new AudioSettingsWindow(engine);
                    settingsWindow.Closed += (_, _) => settingsWindow = null;
                    settingsWindow.Show();
                }
                else settingsWindow.Activate();
            };

            ToggleLibraryButton.Click += (_, _) => ToggleLibrary();
            ToggleLibraryButton.DoubleTapped += (_, _) => OpenLibraryWindow();

            // ── Empty chain hint button ──
            EmptyOpenLibraryBtn.Click += (_, _) => { libraryOpen = true; LibraryPanel.IsVisible = true; };

            // ── Docked library drag items ──
            SetupDockedDragItem(DockDragGain);
            SetupDockedDragItem(DockDragCompressor);
            SetupDockedDragItem(DockDragGate);
            SetupDockedDragItem(DockDragEq);
            SetupDockedDragItem(DockDragDistortion);
            SetupDockedDragItem(DockDragFuzz);
            SetupDockedDragItem(DockDragGraphicEq);
            SetupDockedDragItem(DockDragDelay);
            SetupDockedDragItem(DockDragReverb);
            SetupDockedDragItem(DockDragChorus);

            PresetCleanBtn.Click += (_, _) => ApplyPreset(Preset.Clean);
            PresetCrunchBtn.Click += (_, _) => ApplyPreset(Preset.Crunch);
            PresetLeadBtn.Click += (_, _) => ApplyPreset(Preset.Lead);
            PresetAmbientBtn.Click += (_, _) => ApplyPreset(Preset.Ambient);

            SavePresetDockBtn.Click += (_, _) => { SaveDockPanel.IsVisible = true; PresetNameDockBox.Text = ""; PresetNameDockBox.Focus(); };
            SaveConfirmDockBtn.Click += (_, _) => SaveDockedPreset();
            PresetNameDockBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) SaveDockedPreset(); };
            RefreshDockedUserPresets();

            ExportPresetDockBtn.Click += async (_, _) =>
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export Preset",
                    DefaultExtension = "json",
                    FileTypeChoices = new[] { new FilePickerFileType("AudioBlocks Preset") { Patterns = new[] { "*.json" } } },
                    SuggestedFileName = "MyPreset"
                });
                if (file == null) return;
                var preset = PresetManager.CapturePreset(System.IO.Path.GetFileNameWithoutExtension(file.Name ?? "Preset"), engine.Effects);
                PresetManager.SaveToPath(preset, file.Path.LocalPath);
                StatusLabel.Text = $"Exported: {file.Name}";
            };
            ImportPresetDockBtn.Click += async (_, _) =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Preset",
                    FileTypeFilter = new[] { new FilePickerFileType("AudioBlocks Preset") { Patterns = new[] { "*.json" } } },
                    AllowMultiple = false
                });
                if (files.Count == 0) return;
                var preset = PresetManager.LoadFromPath(files[0].Path.LocalPath);
                if (preset != null)
                {
                    PresetManager.ApplyPreset(preset, engine.Effects);
                    StatusLabel.Text = $"Imported: {preset.Name}";
                }
            };

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
                StatusLabel.Text = engine.Metronome.Enabled ? $"Metronome ON -- {engine.Metronome.BPM:0.###} BPM" : "Metronome OFF";
            };
            BpmDownButton.Click += (_, _) => { engine.Metronome.BPM -= 1; BpmBox.Text = engine.Metronome.BPM.ToString("0.###"); };
            BpmUpButton.Click += (_, _) => { engine.Metronome.BPM += 1; BpmBox.Text = engine.Metronome.BPM.ToString("0.###"); };
            BpmBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (double.TryParse(BpmBox.Text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                        engine.Metronome.BPM = val;
                    BpmBox.Text = engine.Metronome.BPM.ToString("0.###");
                }
            };
            BpmBox.LostFocus += (_, _) =>
            {
                if (double.TryParse(BpmBox.Text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                    engine.Metronome.BPM = val;
                BpmBox.Text = engine.Metronome.BPM.ToString("0.###");
            };
            BpmBox.GotFocus += (_, _) => BpmBox.SelectAll();

            engine.Metronome.OnBeat += beat => Dispatcher.UIThread.Post(() =>
                BeatIndicator.Text = $"{beat}/{engine.Metronome.BeatsPerBar}");

            SetupDropZone();

            engine.Recorder.OnStateChanged += () => Dispatcher.UIThread.Post(SyncTransportUI);
            engine.Effects.OnEffectsChanged += () => Dispatcher.UIThread.Post(UpdateEffectsPanel);

            // ── Rebuild effect cards when theme changes ──
            ActualThemeVariantChanged += (_, _) => UpdateEffectsPanel();

            UpdateEffectsPanel();
            SyncTransportUI();

            // ── Clean shutdown ──
            Closing += (_, _) =>
            {
                vuTimer.Stop();
                settingsWindow?.Close();
                libraryWindow?.Close();
                engine.StopAudio();
            };
        }

        // =========================================================
        // DOCKED LIBRARY — Drag & Drop support
        // =========================================================

        private void SetupDockedDragItem(Border item)
        {
            string effectType = item.Tag?.ToString() ?? "";
            Point? pressPoint = null;
            bool dragStarted = false;

            item.PointerPressed += (_, e) =>
            {
                pressPoint = e.GetPosition(item);
                dragStarted = false;
                e.Handled = true;
            };

            item.PointerMoved += async (_, e) =>
            {
                if (!pressPoint.HasValue || dragStarted) return;

                var pos = e.GetPosition(item);
                double dx = Math.Abs(pos.X - pressPoint.Value.X);
                double dy = Math.Abs(pos.Y - pressPoint.Value.Y);

                if (dx > 5 || dy > 5)
                {
                    dragStarted = true;
                    pressPoint = null;

                    EffectsLibraryWindow.PendingEffectType = effectType;

                    var data = new DataObject();
                    data.Set(DataFormats.Text, $"{DragPrefix}{effectType}");

                    await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);

                    EffectsLibraryWindow.PendingEffectType = null;
                }
            };

            item.PointerReleased += (_, _) =>
            {
                if (pressPoint.HasValue && !dragStarted)
                {
                    pressPoint = null;
                    var fx = EffectsLibraryWindow.CreateEffect(effectType);
                    if (fx != null)
                    {
                        engine.Effects.AddEffect(fx);
                        StatusLabel.Text = $"{fx.Name} added";
                    }
                }
                pressPoint = null;
                dragStarted = false;
            };
        }

        // =========================================================
        // DROP ZONE — No insert/remove during drag = no jank
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
            if (Math.Abs(pos.X - dragPressPoint.Value.X) < DragThreshold &&
                Math.Abs(pos.Y - dragPressPoint.Value.Y) < DragThreshold) return;

            int idx = pendingDragIndex;
            dragPressPoint = null;
            pendingDragIndex = -1;

            // Mark the source block as being dragged (ghost effect)
            dragSourceIndex = idx;
            SetBlockOpacity(idx, 0.3);

            var dataObject = new DataObject();
            dataObject.Set(DataFormats.Text, $"AudioBlocks:Reorder:{idx}");
            await DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);

            // Cleanup
            SetBlockOpacity(dragSourceIndex, 1.0);
            dragSourceIndex = -1;
            currentDropTargetIndex = -1;
            ClearDropHighlights();
        }

        private void CancelPendingDrag() { pendingDragIndex = -1; dragPressPoint = null; }

        private void SetBlockOpacity(int index, double opacity)
        {
            foreach (var child in EffectsPanel.Children)
                if (child is Border b && b.Tag is (int i, string _) && i == index)
                { b.Opacity = opacity; break; }
        }

        private (bool isReorder, bool isNewEffect, string? effectType, int fromIndex) ParseDragData(IDataObject data)
        {
            string? text = data.GetText();
            if (text == null)
                foreach (var fmt in data.GetDataFormats())
                    if (data.Get(fmt) is string s) { text = s; break; }
            if (text == null && EffectsLibraryWindow.PendingEffectType != null)
                text = $"{DragPrefix}{EffectsLibraryWindow.PendingEffectType}";
            if (text == null) return (false, false, null, -1);
            if (text.StartsWith("AudioBlocks:Reorder:") && int.TryParse(text.AsSpan("AudioBlocks:Reorder:".Length), out int idx))
                return (true, false, null, idx);
            if (text.StartsWith(DragPrefix))
                return (false, true, text[DragPrefix.Length..], -1);
            return (false, false, null, -1);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            var (isReorder, isNewEffect, _, _) = ParseDragData(e.Data);
            if (!isReorder && !isNewEffect) { e.DragEffects = DragDropEffects.None; e.Handled = true; return; }
            e.DragEffects = isReorder ? DragDropEffects.Move : DragDropEffects.Copy;
            e.Handled = true;

            int effectCount = engine.Effects.Count;
            int targetIdx = GetDropIndex(e.GetPosition(EffectsPanel).Y);
            // Clamp to valid range — never beyond the last effect block
            targetIdx = Math.Clamp(targetIdx, 0, effectCount > 0 ? effectCount - 1 : 0);

            if (targetIdx != currentDropTargetIndex)
            {
                currentDropTargetIndex = targetIdx;
                HighlightDropTarget(targetIdx);
            }
        }

        private void OnDragLeave(object? sender, DragEventArgs e)
        {
            ClearDropHighlights();
            currentDropTargetIndex = -1;
            e.Handled = true;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            ClearDropHighlights();
            currentDropTargetIndex = -1;
            e.Handled = true;

            int effectCount = engine.Effects.Count;
            int toIdx = GetDropIndex(e.GetPosition(EffectsPanel).Y);
            var (isReorder, isNewEffect, effectType, fromIdx) = ParseDragData(e.Data);

            if (isReorder && fromIdx >= 0 && fromIdx < effectCount)
            {
                toIdx = Math.Clamp(toIdx, 0, effectCount - 1);
                if (toIdx != fromIdx)
                {
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
                    int insertAt = Math.Clamp(toIdx, 0, effectCount);
                    engine.Effects.InsertEffect(insertAt, newEffect);
                    selectedEffectIndex = insertAt;
                    StatusLabel.Text = $"{newEffect.Name} added at position {insertAt + 1}";
                }
            }
        }

        /// <summary>
        /// Highlight only via border/opacity — zero DOM mutations = zero jank.
        /// </summary>
        private void HighlightDropTarget(int targetIdx)
        {
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int index, string catResKey)) continue;
                bool isSource = index == dragSourceIndex;
                bool isTarget = index == targetIdx;

                if (isSource)
                {
                    b.Opacity = 0.3;
                }
                else if (isTarget)
                {
                    b.Opacity = 1.0;
                    b.BorderBrush = DropIndicatorBrush;
                    b.BorderThickness = new Thickness(2, 3, 2, 0);
                }
                else
                {
                    b.Opacity = 0.6;
                    bool sel = index == selectedEffectIndex;
                    b.BorderBrush = sel ? GetThemeBrush(catResKey, "#A0A6B0") : null;
                    b.BorderThickness = new Thickness(sel ? 1.5 : 0);
                }
            }
        }

        private void ClearDropHighlights()
        {
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int index, string catResKey)) continue;
                bool sel = index == selectedEffectIndex;
                var catBrush = GetThemeBrush(catResKey, "#A0A6B0");
                var catColorStr = GetThemeColor(catResKey, "#A0A6B0");
                b.Opacity = 1.0;
                b.BorderBrush = sel ? catBrush : null;
                b.BorderThickness = new Thickness(sel ? 1.5 : 0);
                b.BoxShadow = sel ? BoxShadows.Parse($"0 0 14 0 {catColorStr}55") : default;
            }
        }

        // =========================================================
        // EFFECTS PANEL
        // =========================================================

        /// <summary>
        /// Returns theme resource keys for the block category, not hardcoded hex.
        /// (accentKey, glowKey, label)
        /// </summary>
        private static (string accentKey, string glowKey, string label) GetBlockCategory(IAudioEffect effect) => effect switch
        {
            GainEffect or CompressorEffect or NoiseGateEffect => ("BlockGreen", "BlockGreenGlow", "DYNAMICS"),
            EqEffect or DistortionEffect or FuzzEffect or GraphicEqEffect => ("BlockCyan", "BlockCyanGlow", "TONE"),
            DelayEffect or ReverbEffect or ChorusEffect => ("BlockAmber", "BlockAmberGlow", "TIME"),
            _ => ("MutedText", "DimText", "FX")
        };

        private IBrush GetThemeBrush(string key, string fallbackColor)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush) return brush;
            return new SolidColorBrush(Color.Parse(fallbackColor));
        }

        /// <summary>
        /// Returns color as #RRGGBB (no alpha prefix) so callers can safely append alpha like "55".
        /// </summary>
        private string GetThemeColor(string key, string fallbackColor)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is ISolidColorBrush scb)
                return $"#{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
            return fallbackColor;
        }

        private Control CreateFlowTerminal(string label, bool isInput)
        {
            var colorStr = isInput ? GetThemeColor("BlockGreen", "#7CB342") : GetThemeColor("AccentBrush", "#FF6B6B");
            var color = Color.Parse(colorStr);
            var icon = isInput
                ? "M12,1C7.03,1,3,5.03,3,10v4c0,1.66,1.34,3,3,3h1V10H5c0-3.87,3.13-7,7-7s7,3.13,7,7v7h-2V10h2v4c0,1.66-1.34,3-3,3h-1"
                : "M14,3.23v2.06c2.89,.86,5,3.54,5,6.71s-2.11,5.85-5,6.71v2.06c4.01-.91,7-4.49,7-8.77s-2.99-7.86-7-8.77Zm-4,1.77H6C4.9,5,4,5.9,4,7v10c0,1.1,.9,2,2,2h4l5,5V0Z";

            return new Border
            {
                Background = new SolidColorBrush(color, 0.08),
                BorderBrush = new SolidColorBrush(color, 0.3),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 6), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    new PathIcon { Data = Geometry.Parse(icon), Width = 12, Height = 12, Foreground = new SolidColorBrush(color) },
                    new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center }
                }}
            };
        }

        private Control CreateSignalConnector()
        {
            var grid = new Grid { Height = 22, Width = 20, HorizontalAlignment = HorizontalAlignment.Center, Tag = "connector" };
            grid.Children.Add(new Rectangle { Width = 2, Fill = GetThemeBrush("SignalLine", "#2E3440"), HorizontalAlignment = HorizontalAlignment.Center });
            grid.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = GetThemeBrush("SignalDot", "#4DD0E1"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Tag = "signal-dot" });
            return grid;
        }

        /// <summary>
        /// Creates the cyan drop indicator bar shown between blocks during drag.
        /// </summary>
        private static Border CreateDropIndicator()
        {
            return new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.Parse("#4DD0E1")),
                Margin = new Thickness(8, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = "drop-indicator",
                BoxShadow = BoxShadows.Parse("0 0 8 0 #664DD0E1")
            };
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int index, string catResKey)) continue;
                bool sel = index == selectedEffectIndex;
                var catBrush = GetThemeBrush(catResKey, "#A0A6B0");
                var catColorStr = GetThemeColor(catResKey, "#A0A6B0");
                b.Background = GetThemeBrush(sel ? "CardSelected" : "CardDefault", sel ? "#2A3040" : "#1E2128");
                b.BorderBrush = sel ? catBrush : null;
                b.BorderThickness = new Thickness(sel ? 1.5 : 0);
                b.BoxShadow = sel ? BoxShadows.Parse($"0 0 14 0 {catColorStr}55") : default;
            }
        }

        private void UpdateEffectsPanel()
        {
            EffectsPanel.Children.Clear();
            var all = engine.Effects.GetAllEffects();
            EmptyChainHint.IsVisible = all.Count == 0;
            if (all.Count == 0) return;

            EffectsPanel.Children.Add(CreateFlowTerminal("INPUT", true));
            EffectsPanel.Children.Add(CreateSignalConnector());

            for (int idx = 0; idx < all.Count; idx++)
            {
                var effect = all[idx];
                int ci = idx; bool sel = ci == selectedEffectIndex;
                var (catResKey, catGlowKey, catLabel) = GetBlockCategory(effect);
                var catBrush = GetThemeBrush(catResKey, "#A0A6B0");
                var catColorStr = GetThemeColor(catResKey, "#A0A6B0");

                var card = new Border
                {
                    Background = GetThemeBrush(sel ? "CardSelected" : "CardDefault", sel ? "#2A3040" : "#1E2128"),
                    CornerRadius = new CornerRadius(10), ClipToBounds = true,
                    BorderBrush = sel ? catBrush : null, BorderThickness = new Thickness(sel ? 1.5 : 0),
                    BoxShadow = sel ? BoxShadows.Parse($"0 0 14 0 {catColorStr}55") : default,
                    Cursor = new Cursor(StandardCursorType.Hand), Tag = (ci, catResKey),
                    Transitions = new Transitions
                    {
                        new DoubleTransition { Property = Border.OpacityProperty, Duration = TimeSpan.FromMilliseconds(120) },
                        new ThicknessTransition { Property = Border.BorderThicknessProperty, Duration = TimeSpan.FromMilliseconds(120) },
                        new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(120) }
                    }
                };
                card.PointerPressed += (_, e) => { selectedEffectIndex = ci; UpdateSelectionVisuals(); if (e.GetCurrentPoint(card).Properties.IsLeftButtonPressed) BeginPendingDrag(ci, e, card); };
                card.PointerMoved += (_, e) => TryStartDrag(e, card);
                card.PointerReleased += (_, _) => CancelPendingDrag();

                var blockGrid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("4,*") };
                blockGrid.Children.Add(new Border { Background = catBrush, CornerRadius = new CornerRadius(10, 0, 0, 10), [Grid.ColumnProperty] = 0 });

                var content = new StackPanel { Spacing = 8, Margin = new Thickness(10, 10, 12, 10), [Grid.ColumnProperty] = 1 };
                var header = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,Auto,Auto,*,Auto") };

                header.Children.Add(new PathIcon { Width = 10, Height = 14, Foreground = GetThemeBrush("DimText", "#6B7280"), Data = Geometry.Parse("M4,2A2,2,0,1,1,2,4,2,2,0,0,1,4,2Zm6,0A2,2,0,1,1,8,4,2,2,0,0,1,10,2ZM4,8A2,2,0,1,1,2,10,2,2,0,0,1,4,8Zm6,0A2,2,0,1,1,8,10,2,2,0,0,1,10,8ZM4,14a2,2,0,1,1-2,2A2,2,0,0,1,4,14Zm6,0a2,2,0,1,1-2,2A2,2,0,0,1,10,14Z"), Cursor = new Cursor(StandardCursorType.DragMove), Margin = new Thickness(0, 0, 6, 0), [Grid.ColumnProperty] = 0 });

                var chk = new CheckBox { IsChecked = effect.Enabled, [Grid.ColumnProperty] = 1 };
                chk.IsCheckedChanged += (_, _) => effect.Enabled = chk.IsChecked == true;
                header.Children.Add(chk);

                header.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse(catColorStr), 0.15), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 2, Child = new TextBlock { Text = catLabel, FontSize = 9, FontWeight = FontWeight.Bold, Foreground = catBrush } });
                header.Children.Add(new TextBlock { Text = $"{idx + 1}. {effect.Name}", Foreground = GetThemeBrush("FgText", "#F0F0F0"), FontWeight = FontWeight.SemiBold, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), [Grid.ColumnProperty] = 3 });

                var rm = new Button { Content = "\u2715", FontSize = 10, Width = 26, Height = 26, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(6), [Grid.ColumnProperty] = 4 };
                rm.Click += (_, _) => { engine.Effects.RemoveEffect(effect); if (selectedEffectIndex >= engine.Effects.Count) selectedEffectIndex = engine.Effects.Count - 1; };
                header.Children.Add(rm);
                content.Children.Add(header);

                var knobs = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

                if (effect is GainEffect g) knobs.Children.Add(MakeKnob("Boost", 0, 4, g.Gain, v => g.Gain = v, v => $"{(v <= 0 ? -96 : 20 * Math.Log10(v)):+0.0;-0.0}dB", catColorStr));
                else if (effect is DistortionEffect d) { knobs.Children.Add(MakeKnob("Drive", 0, 1, d.Drive, v => d.Drive = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Tone", 0, 1, d.Tone, v => d.Tone = v, v => $"{v * 100:0}%", "#FFB74D")); knobs.Children.Add(MakeKnob("Mix", 0, 1, d.Mix, v => d.Mix = v, v => $"{v * 100:0}%", "#81C784")); knobs.Children.Add(MakeKnob("Level", 0, 1, d.Level, v => d.Level = v, v => $"{v * 100:0}%", "#B388FF")); }
                else if (effect is FuzzEffect fz) { knobs.Children.Add(MakeKnob("Fuzz", 0, 1, fz.Fuzz, v => fz.Fuzz = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Tone", 0, 1, fz.Tone, v => fz.Tone = v, v => $"{v * 100:0}%", "#FFB74D")); knobs.Children.Add(MakeKnob("Gate", 0, 0.1, fz.Gate, v => fz.Gate = v, v => $"{v * 1000:0.0}", "#81C784")); knobs.Children.Add(MakeKnob("Level", 0, 1, fz.Level, v => fz.Level = v, v => $"{v * 100:0}%", "#B388FF")); knobs.Children.Add(MakeKnob("Mix", 0, 1, fz.Mix, v => fz.Mix = v, v => $"{v * 100:0}%", catColorStr)); }
                else if (effect is ReverbEffect r) { knobs.Children.Add(MakeKnob("Mix", 0, 1, r.Mix, v => r.Mix = v, v => $"{v * 100:0}%", catColorStr)); knobs.Children.Add(MakeKnob("Decay", 0, 1, r.Decay, v => r.Decay = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Damp", 0, 1, r.Damping, v => r.Damping = v, v => $"{v * 100:0}%", "#FFB74D")); }
                else if (effect is CompressorEffect c) { knobs.Children.Add(MakeKnob("Thresh", 0, 1, c.Threshold, v => c.Threshold = v, v => $"{(v <= 0.001 ? -60 : 20 * Math.Log10(v)):0.0}dB", "#FF6B6B")); knobs.Children.Add(MakeKnob("Ratio", 1, 20, c.Ratio, v => c.Ratio = v, v => $"{v:0.0}:1", catColorStr)); knobs.Children.Add(MakeKnob("Atk", 0, 1, c.Attack, v => c.Attack = v, v => $"{0.1 + v * 99.9:0.0}ms", "#81C784")); knobs.Children.Add(MakeKnob("Rel", 0, 1, c.Release, v => c.Release = v, v => $"{5 + v * 995:0}ms", "#FFB74D")); knobs.Children.Add(MakeKnob("Makeup", 0.5, 3, c.Makeup, v => c.Makeup = v, v => $"{(v <= 0 ? -96 : 20 * Math.Log10(v)):+0.0;-0.0}dB", "#B388FF")); }
                else if (effect is NoiseGateEffect ng) { knobs.Children.Add(MakeKnob("Thresh", 0, 0.2, ng.Threshold, v => ng.Threshold = v, v => $"{v * 100:0.0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Atk", 0, 1, ng.Attack, v => ng.Attack = v, v => $"{0.05 + v * 10:0.0}ms", "#81C784")); knobs.Children.Add(MakeKnob("Rel", 0, 1, ng.Release, v => ng.Release = v, v => $"{5 + v * 500:0}ms", "#FFB74D")); }
                else if (effect is DelayEffect dl) { knobs.Children.Add(MakeKnob("Time", 0, 1, dl.Time, v => dl.Time = v, v => $"{50 + v * 950:0}ms", catColorStr)); knobs.Children.Add(MakeKnob("FB", 0, 1, dl.Feedback, v => dl.Feedback = v, v => $"{v * 100:0}%", "#FF6B6B")); knobs.Children.Add(MakeKnob("Mix", 0, 1, dl.Mix, v => dl.Mix = v, v => $"{v * 100:0}%", "#81C784")); }
                else if (effect is ChorusEffect ch) { knobs.Children.Add(MakeKnob("Rate", 0, 1, ch.Rate, v => ch.Rate = v, v => $"{0.1 + v * 4.9:0.0}Hz", "#B388FF")); knobs.Children.Add(MakeKnob("Depth", 0, 1, ch.Depth, v => ch.Depth = v, v => $"{v * 100:0}%", catColorStr)); knobs.Children.Add(MakeKnob("Mix", 0, 1, ch.Mix, v => ch.Mix = v, v => $"{v * 100:0}%", "#81C784")); }
                else if (effect is EqEffect eq) { knobs.Children.Add(MakeKnob("Low", -1, 1, eq.Low, v => eq.Low = v, v => $"{v:+0.0;-0.0}", "#FF6B6B")); knobs.Children.Add(MakeKnob("Mid", -1, 1, eq.Mid, v => eq.Mid = v, v => $"{v:+0.0;-0.0}", "#FFB74D")); knobs.Children.Add(MakeKnob("High", -1, 1, eq.High, v => eq.High = v, v => $"{v:+0.0;-0.0}", catColorStr)); }
                else if (effect is GraphicEqEffect geq) { var eqCtrl = new GraphicEqControl { Height = 150, Effect = geq, Margin = new Thickness(0, 4) }; eqCtrl.GainChanged += (_, _) => { }; content.Children.Add(eqCtrl); }

                content.Children.Add(knobs);

                if (effect is NoiseGateEffect ngM) { var bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 6, Margin = new Thickness(0, 2, 0, 0) }; var lbl = new TextBlock { FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = GetThemeBrush("MutedText", "#A0A6B0") }; content.Children.Add(bar); content.Children.Add(lbl); bar.Tag = ngM; lbl.Tag = ngM; }
                else if (effect is CompressorEffect cM) { var lbl = new TextBlock { FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = GetThemeBrush("GrMeter", "#FF6B6B") }; content.Children.Add(lbl); lbl.Tag = cM; }
                else if (effect is EqEffect eqM) { var panel = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,*,*"), Height = 20, Margin = new Thickness(0, 2, 0, 0) }; var bL = new ProgressBar { Minimum = 0, Maximum = 0.5, Height = 6, Margin = new Thickness(1, 0), [Grid.ColumnProperty] = 0 }; var bM2 = new ProgressBar { Minimum = 0, Maximum = 0.5, Height = 6, Margin = new Thickness(1, 0), [Grid.ColumnProperty] = 1 }; var bH = new ProgressBar { Minimum = 0, Maximum = 0.5, Height = 6, Margin = new Thickness(1, 0), [Grid.ColumnProperty] = 2 }; panel.Children.Add(bL); panel.Children.Add(bM2); panel.Children.Add(bH); content.Children.Add(panel); bL.Tag = ("eq_low", eqM); bM2.Tag = ("eq_mid", eqM); bH.Tag = ("eq_high", eqM); }

                blockGrid.Children.Add(content); card.Child = blockGrid; EffectsPanel.Children.Add(card);
                if (idx < all.Count - 1) EffectsPanel.Children.Add(CreateSignalConnector());
            }
            EffectsPanel.Children.Add(CreateSignalConnector());
            EffectsPanel.Children.Add(CreateFlowTerminal("OUTPUT", false));
        }

        private void UpdateEffectMeters()
        {
            foreach (var child in EffectsPanel.Children)
            {
                if (child is not Border b || b.Tag is not (int, string)) continue;
                if (b.Child is not Grid g) continue;
                var sp = g.Children.OfType<StackPanel>().FirstOrDefault();
                if (sp == null) continue;
                foreach (var ctrl in sp.Children)
                {
                    if (ctrl is ProgressBar pb && pb.Tag is NoiseGateEffect ngm) pb.Value = ngm.CurrentGateGain;
                    else if (ctrl is TextBlock tb && tb.Tag is NoiseGateEffect ngm2) tb.Text = ngm2.CurrentGateGain > 0.95f ? "OPEN" : ngm2.CurrentGateGain < 0.05f ? "CLOSED" : $"GR: {ngm2.GainReductionDb:0.0} dB";
                    else if (ctrl is TextBlock tb2 && tb2.Tag is CompressorEffect cm) tb2.Text = cm.GainReductionDb < -0.1f ? $"GR: {cm.GainReductionDb:0.0} dB" : "--";
                    else if (ctrl is Grid eg) foreach (var ec in eg.Children) if (ec is ProgressBar eb && eb.Tag is (string band, EqEffect eq)) eb.Value = band switch { "eq_low" => eq.LowLevel, "eq_mid" => eq.MidLevel, "eq_high" => eq.HighLevel, _ => 0 };
                }
            }
            if (engine.IsMonitoring && engine.Level > 0.001f)
            {
                double pulse = 0.4 + 0.6 * Math.Abs(Math.Sin(Environment.TickCount64 / 300.0));
                foreach (var child in EffectsPanel.Children) if (child is Grid cg && cg.Tag is "connector") foreach (var dot in cg.Children) if (dot is Ellipse e && e.Tag is "signal-dot") e.Opacity = pulse;
            }
            else foreach (var child in EffectsPanel.Children) if (child is Grid cg && cg.Tag is "connector") foreach (var dot in cg.Children) if (dot is Ellipse e && e.Tag is "signal-dot") e.Opacity = 0.3;
        }

        private int GetDropIndex(double y)
        {
            int blockIdx = 0;
            foreach (var child in EffectsPanel.Children)
            {
                if (child.Tag is not (int, string)) continue;
                if (y < child.Bounds.Y + child.Bounds.Height / 2) return blockIdx;
                blockIdx++;
            }
            return blockIdx;
        }

        // =========================================================
        // TRANSPORT
        // =========================================================

        private void OnRecordClick() { if (engine.Recorder.IsRecording) engine.Recorder.StopRecording(); else { if (!engine.IsMonitoring) { StatusLabel.Text = "Start monitoring first"; return; } engine.Recorder.StartRecording(); } }
        private void OnPlayClick() { if (engine.Recorder.IsPlaying) engine.Recorder.StopPlayback(); else { if (!engine.IsMonitoring) { StatusLabel.Text = "Start monitoring first"; return; } engine.Recorder.StartPlayback(); } }
        private void OnStopClick() { if (engine.Recorder.IsRecording) engine.Recorder.StopRecording(); if (engine.Recorder.IsPlaying) engine.Recorder.StopPlayback(); PlaybackProgress.Value = 0; }

        private async System.Threading.Tasks.Task OnExportClick()
        {
            if (!engine.Recorder.HasRecording) { StatusLabel.Text = "Nothing to export"; return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export Recording", DefaultExtension = "wav", FileTypeChoices = new[] { new FilePickerFileType("WAV 32-bit float") { Patterns = new[] { "*.wav" } }, new FilePickerFileType("WAV 16-bit PCM") { Patterns = new[] { "*.wav" } } }, SuggestedFileName = $"AudioBlocks_{DateTime.Now:yyyyMMdd_HHmmss}" });
            if (file == null) return;
            string path = file.Path.LocalPath; bool is16 = file.Name?.Contains("16") == true;
            bool ok = is16 ? engine.Recorder.ExportWav16(path, engine.SampleRate) : engine.Recorder.ExportWav(path, engine.SampleRate);
            StatusLabel.Text = ok ? $"Exported: {path}" : "Export failed";
        }

        private void SyncTransportUI()
        {
            bool rec = engine.Recorder.IsRecording, play = engine.Recorder.IsPlaying, hasRec = engine.Recorder.HasRecording;
            if (RecordButton.Content is Ellipse recCircle) recCircle.Fill = rec ? new SolidColorBrush(Color.Parse("#FF0000")) : new SolidColorBrush(Color.Parse("#EF4444"));
            if (PlayButton.Content is PathIcon playIcon) playIcon.Data = play ? PauseIcon : PlayIcon;
            PlayButton.IsEnabled = hasRec || play; ExportButton.IsEnabled = hasRec && !rec;
            if (!play) PlaybackProgress.Value = 0; if (!rec && !play && !hasRec) RecordTimeLabel.Text = "00:00.0";
        }

        // =========================================================
        // LIBRARY
        // =========================================================

        private void ToggleLibrary() { if (libraryWindow != null) { libraryWindow.Activate(); return; } libraryOpen = !libraryOpen; LibraryPanel.IsVisible = libraryOpen; }

        public void OpenLibraryWindow()
        {
            if (libraryWindow != null) { libraryWindow.Activate(); return; }
            libraryWindow = new EffectsLibraryWindow(engine);
            libraryWindow.Closed += (_, _) => libraryWindow = null;
            libraryWindow.Show(this); libraryOpen = false; LibraryPanel.IsVisible = false;
        }

        private void AddEffect(IAudioEffect effect) { engine.Effects.AddEffect(effect); StatusLabel.Text = $"{effect.Name} added"; }

        private enum Preset { Clean, Crunch, Lead, Ambient }
        private void ApplyPreset(Preset preset)
        {
            foreach (var fx in engine.Effects.GetAllEffects()) engine.Effects.RemoveEffect(fx);
            switch (preset)
            {
                case Preset.Clean: engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.01f }); engine.Effects.AddEffect(new EqEffect { Low = -0.2f, Mid = 0.1f, High = 0.2f }); engine.Effects.AddEffect(new CompressorEffect { Threshold = 0.4f, Ratio = 3f, Makeup = 1.3f }); break;
                case Preset.Crunch: engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.02f }); engine.Effects.AddEffect(new GainEffect { Gain = 1.5f }); engine.Effects.AddEffect(new DistortionEffect { Drive = 0.35f, Tone = 0.5f, Mix = 1f, Level = 0.65f }); engine.Effects.AddEffect(new EqEffect { Low = 0.1f, Mid = 0.2f, High = -0.1f }); break;
                case Preset.Lead: engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.03f }); engine.Effects.AddEffect(new GainEffect { Gain = 2.0f }); engine.Effects.AddEffect(new DistortionEffect { Drive = 0.6f, Tone = 0.65f, Mix = 1f, Level = 0.5f }); engine.Effects.AddEffect(new EqEffect { Low = -0.1f, Mid = 0.3f, High = 0.1f }); engine.Effects.AddEffect(new DelayEffect { Time = 0.35f, Feedback = 0.3f, Mix = 0.2f }); engine.Effects.AddEffect(new ReverbEffect { Mix = 0.15f, Decay = 0.4f, Damping = 0.5f }); break;
                case Preset.Ambient: engine.Effects.AddEffect(new EqEffect { Low = 0.2f, Mid = -0.2f, High = 0.3f }); engine.Effects.AddEffect(new ChorusEffect { Rate = 0.3f, Depth = 0.6f, Mix = 0.4f }); engine.Effects.AddEffect(new DelayEffect { Time = 0.6f, Feedback = 0.5f, Mix = 0.4f }); engine.Effects.AddEffect(new ReverbEffect { Mix = 0.5f, Decay = 0.8f, Damping = 0.6f }); break;
            }
            StatusLabel.Text = $"Preset: {preset}";
            RefreshDockedUserPresets();
        }

        private void SaveDockedPreset() { var name = PresetNameDockBox.Text?.Trim(); if (string.IsNullOrEmpty(name)) return; PresetManager.Save(PresetManager.CapturePreset(name, engine.Effects)); SaveDockPanel.IsVisible = false; StatusLabel.Text = $"Saved: {name}"; RefreshDockedUserPresets(); }

        private void RefreshDockedUserPresets()
        {
            UserPresetsDockPanel.Children.Clear();
            foreach (var name in PresetManager.GetAll())
            {
                var presetName = name;
                var grid = new Grid { Margin = new Thickness(0, 1) }; grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                var loadBtn = new Button { Content = presetName, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left }; loadBtn.Classes.Add("lib-item"); Grid.SetColumn(loadBtn, 0);
                var delBtn = new Button { Content = "\u2715", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.Parse("#FF6B6B")), VerticalAlignment = VerticalAlignment.Center, MinWidth = 0, MinHeight = 0 }; Grid.SetColumn(delBtn, 1);
                loadBtn.Click += (_, _) => { var data = PresetManager.Load(presetName); if (data != null) { PresetManager.ApplyPreset(data, engine.Effects); StatusLabel.Text = $"Loaded: {presetName}"; } };
                delBtn.Click += (_, _) => { PresetManager.Delete(presetName); StatusLabel.Text = $"Deleted: {presetName}"; RefreshDockedUserPresets(); };
                grid.Children.Add(loadBtn); grid.Children.Add(delBtn); UserPresetsDockPanel.Children.Add(grid);
            }
        }

        private static KnobControl MakeKnob(String label, double min, double max, double value, Action<float> onChange, Func<double, string> fmt, string color)
        {
            var k = new KnobControl { Width = 68, Height = 88, Minimum = min, Maximum = max, Value = value, Label = label, DisplayValue = fmt(value), KnobColor = new SolidColorBrush(Color.Parse(color)), Margin = new Thickness(4, 2) };
            k.ValueChanged += (_, v) => { onChange((float)v); k.DisplayValue = fmt(v); };
            return k;
        }
    }
}

#pragma warning restore CS0618
