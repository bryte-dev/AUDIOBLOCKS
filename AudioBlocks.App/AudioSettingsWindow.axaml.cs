using AudioBlocks.App.Audio;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AudioBlocks.App
{
    public partial class AudioSettingsWindow : Window
    {
        private readonly AudioEngine engine;
        private readonly DispatcherTimer vuTimer;

        private string[] inputDeviceIds = Array.Empty<string>();
        private string[] outputDeviceIds = Array.Empty<string>();
        private string[] asioDriverNames = Array.Empty<string>();
        private int asioInputCount;
        private int asioOutputCount;

        public AudioSettingsWindow(AudioEngine mainEngine)
        {
            InitializeComponent();
            engine = mainEngine;

            // ===== THEME =====
            ThemeComboBox.SelectedIndex = Application.Current?.RequestedThemeVariant == ThemeVariant.Light ? 1 : 0;
            ThemeComboBox.SelectionChanged += (_, _) =>
            {
                if (Application.Current is not null)
                    Application.Current.RequestedThemeVariant = ThemeComboBox.SelectedIndex == 1
                        ? ThemeVariant.Light
                        : ThemeVariant.Dark;
            };

            engine.OnCpuOverloadChanged += overload =>
                Dispatcher.UIThread.Post(() => CpuWarningLabel.Text = overload ? "CPU overload" : "");

            engine.OnMonitoringChanged += _ =>
                Dispatcher.UIThread.Post(SyncControlStates);

            engine.OnLog += msg =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    string cur = StatusLabel.Text ?? "";
                    string next = cur + (string.IsNullOrEmpty(cur) ? "" : Environment.NewLine) + msg;
                    if (next.Length > 3000)
                        next = next.Substring(next.Length - 3000);
                    StatusLabel.Text = next;
                    StatusLabel.CaretIndex = next.Length;
                });
            };

            EnumerateDevices();
            SetInitialSelection();
            PopulateAsioList();

            AsioDriverComboBox.SelectionChanged += (_, _) =>
            {
                var name = GetSelectedAsioDriver();
                if (!string.IsNullOrEmpty(name))
                    ProbeAndFillAsioChannels(name);
            };

            DriverComboBox.SelectionChanged += (_, _) => SyncControlStates();

            vuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            vuTimer.Tick += (_, _) =>
            {
                VuMeter.Value = engine.Level;
                LatencyLabel.Text = $"Latency: {engine.CalculatedLatencyMs:F1} ms";
                ProcessingLabel.Text = $"{engine.SmoothedProcessingMs:F2} ms";
            };
            vuTimer.Start();

            ApplyButton.Click += (_, _) => ApplyAndStart();
            StartSineButton.Click += (_, _) => { engine.StartAudio(); SyncControlStates(); };
            StopSineButton.Click += (_, _) => { engine.StopAudio(); SyncControlStates(); };
            OpenAsioControlPanelButton.Click += (_, _) => OpenAsioControlPanel();
            TestRoutingButton.Click += (_, _) => TestRouting();

            this.Closing += (_, _) => engine.StopAsioTest();
            SyncControlStates();
        }

        private void EnumerateDevices()
        {
            var inIds = new List<string>();
            var outIds = new List<string>();

            try
            {
                foreach (var dev in engine.GetInputDevices())
                {
                    try { InputDeviceComboBox.Items.Add(dev.FriendlyName); }
                    catch (COMException) { InputDeviceComboBox.Items.Add("(unavailable)"); }
                    finally { inIds.Add(dev.ID); dev.Dispose(); }
                }
                foreach (var dev in engine.GetOutputDevices())
                {
                    try { OutputDeviceComboBox.Items.Add(dev.FriendlyName); }
                    catch (COMException) { OutputDeviceComboBox.Items.Add("(unavailable)"); }
                    finally { outIds.Add(dev.ID); dev.Dispose(); }
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Device enumeration error: {ex.Message}";
            }

            inputDeviceIds = inIds.ToArray();
            outputDeviceIds = outIds.ToArray();
        }

        private void SetInitialSelection()
        {
            if (engine.InputDevice != null)
            {
                int idx = Array.IndexOf(inputDeviceIds, engine.InputDevice.ID);
                if (idx >= 0) InputDeviceComboBox.SelectedIndex = idx;
            }
            else if (InputDeviceComboBox.Items.Count > 0)
                InputDeviceComboBox.SelectedIndex = 0;

            if (engine.OutputDevice != null)
            {
                int idx = Array.IndexOf(outputDeviceIds, engine.OutputDevice.ID);
                if (idx >= 0) OutputDeviceComboBox.SelectedIndex = idx;
            }
            else if (OutputDeviceComboBox.Items.Count > 0)
                OutputDeviceComboBox.SelectedIndex = 0;

            DriverComboBox.SelectedIndex = engine.Driver switch
            {
                AudioDriver.WASAPI_Exclusive => 1, AudioDriver.ASIO => 2, _ => 0
            };
            SampleRateComboBox.SelectedIndex = engine.SampleRate switch
            {
                48000 => 1, 96000 => 2, _ => 0
            };
            BufferSizeComboBox.SelectedIndex = engine.BufferSize switch
            {
                64 => 0, 128 => 1, 512 => 3, _ => 2
            };
        }

        private void PopulateAsioList()
        {
            try
            {
                asioDriverNames = AudioEngine.GetAsioDrivers().ToArray();
                AsioDriverComboBox.Items.Clear();
                foreach (var n in asioDriverNames)
                    AsioDriverComboBox.Items.Add(n);

                if (asioDriverNames.Length > 0)
                {
                    AsioDriverComboBox.SelectedIndex = 0;
                    ProbeAndFillAsioChannels(asioDriverNames[0]);
                }
            }
            catch (Exception ex)
            {
                asioDriverNames = Array.Empty<string>();
                StatusLabel.Text = $"ASIO enum error: {ex.Message}";
            }
        }

        private void ProbeAndFillAsioChannels(string driverName)
        {
            try
            {
                var (inCnt, outCnt) = engine.ProbeAsioChannels(driverName);
                asioInputCount = inCnt > 0 ? inCnt : 2;
                asioOutputCount = outCnt > 0 ? outCnt : 2;
            }
            catch
            {
                asioInputCount = 2;
                asioOutputCount = 2;
            }

            AsioInLeftComboBox.Items.Clear();
            AsioInRightComboBox.Items.Clear();
            AsioOutLeftComboBox.Items.Clear();
            AsioOutRightComboBox.Items.Clear();

            for (int i = 0; i < asioInputCount; i++)
            {
                AsioInLeftComboBox.Items.Add($"In {i + 1}");
                AsioInRightComboBox.Items.Add($"In {i + 1}");
            }
            for (int i = 0; i < asioOutputCount; i++)
            {
                AsioOutLeftComboBox.Items.Add($"Out {i + 1}");
                AsioOutRightComboBox.Items.Add($"Out {i + 1}");
            }

            AsioInLeftComboBox.SelectedIndex = 0;
            AsioInRightComboBox.SelectedIndex = AsioInRightComboBox.Items.Count >= 2 ? 1 : 0;

            if (AsioOutLeftComboBox.Items.Count >= 4)
            {
                AsioOutLeftComboBox.SelectedIndex = 2;
                AsioOutRightComboBox.SelectedIndex = 3;
            }
            else
            {
                AsioOutLeftComboBox.SelectedIndex = 0;
                AsioOutRightComboBox.SelectedIndex = AsioOutRightComboBox.Items.Count >= 2 ? 1 : 0;
            }

            DriverNoteText.Text = $"'{driverName}' — {asioInputCount} in / {asioOutputCount} out";
        }

        private string? GetSelectedAsioDriver()
        {
            if (AsioDriverComboBox.SelectedIndex >= 0 && AsioDriverComboBox.SelectedIndex < asioDriverNames.Length)
                return asioDriverNames[AsioDriverComboBox.SelectedIndex];
            return null;
        }

        private void SyncControlStates()
        {
            bool isAsio = DriverComboBox.SelectedIndex == 2;
            bool monitoring = engine.IsMonitoring;

            AsioDriverComboBox.IsVisible = isAsio;
            AsioInLeftComboBox.IsVisible = isAsio;
            AsioInRightComboBox.IsVisible = isAsio;
            AsioOutLeftComboBox.IsVisible = isAsio;
            AsioOutRightComboBox.IsVisible = isAsio;
            OpenAsioControlPanelButton.IsVisible = isAsio;
            TestRoutingButton.IsVisible = isAsio;
            InputDeviceComboBox.IsVisible = !isAsio;
            OutputDeviceComboBox.IsVisible = !isAsio;

            DriverComboBox.IsEnabled = !monitoring;
            AsioDriverComboBox.IsEnabled = isAsio && !monitoring;
            AsioInLeftComboBox.IsEnabled = isAsio && !monitoring;
            AsioInRightComboBox.IsEnabled = isAsio && !monitoring;
            AsioOutLeftComboBox.IsEnabled = isAsio && !monitoring;
            AsioOutRightComboBox.IsEnabled = isAsio && !monitoring;
            InputDeviceComboBox.IsEnabled = !isAsio && !monitoring;
            OutputDeviceComboBox.IsEnabled = !isAsio && !monitoring;
            SampleRateComboBox.IsEnabled = !monitoring;
            BufferSizeComboBox.IsEnabled = !monitoring;
            TestRoutingButton.IsEnabled = isAsio;
            OpenAsioControlPanelButton.IsEnabled = isAsio;

            ApplyButton.Content = monitoring ? "Stop" : "Apply \u0026 Start";

            if (!isAsio)
            {
                DriverNoteText.Text = DriverComboBox.SelectedIndex == 1
                    ? "WASAPI Exclusive — low latency, device locked"
                    : "WASAPI Shared — system mixer, higher latency";
            }
        }

        private void ApplyAndStart()
        {
            if (engine.IsMonitoring)
            {
                engine.StopMonitoring();
                SyncControlStates();
                return;
            }

            try
            {
                engine.Driver = DriverComboBox.SelectedIndex switch
                {
                    1 => AudioDriver.WASAPI_Exclusive, 2 => AudioDriver.ASIO, _ => AudioDriver.WASAPI_Shared
                };

                if (engine.Driver == AudioDriver.ASIO)
                {
                    var drvName = GetSelectedAsioDriver();
                    if (string.IsNullOrEmpty(drvName))
                    {
                        StatusLabel.Text = "Select an ASIO driver first.";
                        engine.Driver = AudioDriver.WASAPI_Shared;
                        DriverComboBox.SelectedIndex = 0;
                        SyncControlStates();
                        return;
                    }
                    engine.SetAsioDriver(drvName);
                }

                engine.SampleRate = SampleRateComboBox.SelectedIndex switch { 1 => 48000, 2 => 96000, _ => 44100 };
                engine.BufferSize = BufferSizeComboBox.SelectedIndex switch { 0 => 64, 1 => 128, 3 => 512, _ => 256 };

                if (engine.Driver != AudioDriver.ASIO)
                {
                    var enumerator = new MMDeviceEnumerator();
                    engine.InputDevice = InputDeviceComboBox.SelectedIndex >= 0 && InputDeviceComboBox.SelectedIndex < inputDeviceIds.Length
                        ? enumerator.GetDevice(inputDeviceIds[InputDeviceComboBox.SelectedIndex]) : null;
                    engine.OutputDevice = OutputDeviceComboBox.SelectedIndex >= 0 && OutputDeviceComboBox.SelectedIndex < outputDeviceIds.Length
                        ? enumerator.GetDevice(outputDeviceIds[OutputDeviceComboBox.SelectedIndex]) : null;

                    if (engine.InputDevice == null || engine.OutputDevice == null)
                    {
                        StatusLabel.Text = "Select valid WASAPI input and output devices.";
                        return;
                    }
                }
                else
                {
                    int inOffset = AsioInLeftComboBox.SelectedIndex >= 0 ? AsioInLeftComboBox.SelectedIndex : 0;
                    int outOffset = AsioOutLeftComboBox.SelectedIndex >= 0 ? AsioOutLeftComboBox.SelectedIndex : 0;

                    engine.SetAsioRouting(inOffset, outOffset, 1, 2);
                    engine.InputDevice = null;
                    engine.OutputDevice = null;
                }

                engine.StartMonitoring();

                if (engine.Driver == AudioDriver.ASIO)
                {
                    int inIdx = AsioInLeftComboBox.SelectedIndex + 1;
                    int outIdx = AsioOutLeftComboBox.SelectedIndex + 1;
                    StatusLabel.Text = $"ASIO — In {inIdx} > Out {outIdx}/{outIdx + 1}";
                }
                else
                    StatusLabel.Text = "WASAPI monitoring active";

                SyncControlStates();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
        }

        private void TestRouting()
        {
            var drvName = GetSelectedAsioDriver();
            if (string.IsNullOrEmpty(drvName)) { StatusLabel.Text = "Select an ASIO driver first."; return; }
            if (engine.IsMonitoring) engine.StopMonitoring();

            engine.SetAsioDriver(drvName);
            int outOffset = AsioOutLeftComboBox.SelectedIndex >= 0 ? AsioOutLeftComboBox.SelectedIndex : 0;
            engine.SetAsioRouting(0, outOffset, 1, 2);
            engine.StartAsioTest(1000, 800f, 0.5f);
            StatusLabel.Text = $"Test tone > Out {outOffset + 1} / Out {outOffset + 2}";
        }

        private void OpenAsioControlPanel()
        {
            var driver = GetSelectedAsioDriver();
            if (string.IsNullOrEmpty(driver)) { StatusLabel.Text = "No ASIO driver selected."; return; }
            engine.SetAsioDriver(driver);
            if (!engine.ShowAsioControlPanel())
                StatusLabel.Text = "ASIO panel not available. Use MiniFuse Control Center.";
            ProbeAndFillAsioChannels(driver);
        }
    }
}