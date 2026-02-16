using AudioBlocks.App;
using AudioBlocks.App.Audio;
using Avalonia.Controls;
using Avalonia.Threading;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;

namespace AudioBlocks.App
{
    public partial class AudioSettingsWindow : Window
    {
        private AudioEngine engine;
        private DispatcherTimer vuTimer;

        // stocke seulement les IDs — on créera les MMDevice au besoin (évite de conserver des COM objects vivants)
        private string[] inputDeviceIds = Array.Empty<string>();
        private string[] outputDeviceIds = Array.Empty<string>();

        // ASIO drivers & channel choices
        private string[] asioDriverNames = Array.Empty<string>();
        private int asioInputCount = 0;
        private int asioOutputCount = 0;

        public AudioSettingsWindow(AudioEngine mainEngine)
        {
            InitializeComponent();

            engine = mainEngine;
            engine.OnCpuOverloadChanged += CpuOverloadChanged;
            engine.OnLog += (msg) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    const int MaxLen = 1000;
                    string current = StatusLabel.Text ?? "";
                    string next = current + (string.IsNullOrEmpty(current) ? "" : Environment.NewLine) + msg;
                    if (next.Length > MaxLen)
                        next = next.Substring(next.Length - MaxLen);
                    StatusLabel.Text = next;
                    // utile pour debug si tu lances depuis Visual Studio
                    Console.WriteLine(msg);
                });
            };

            // ===== Enumerate devices =====
            var inputIds = new List<string>();
            var outputIds = new List<string>();

            try
            {
                var inputs = engine.GetInputDevices();
                foreach (var dev in inputs)
                {
                    try
                    {
                        string name = dev.FriendlyName;
                        InputDeviceComboBox.Items.Add(name);
                    }
                    catch (COMException)
                    {
                        InputDeviceComboBox.Items.Add("(unavailable)");
                    }
                    finally
                    {
                        inputIds.Add(dev.ID);
                        dev.Dispose();
                    }
                }

                var outputs = engine.GetOutputDevices();
                foreach (var dev in outputs)
                {
                    try
                    {
                        string name = dev.FriendlyName;
                        OutputDeviceComboBox.Items.Add(name);
                    }
                    catch (COMException)
                    {
                        OutputDeviceComboBox.Items.Add("(unavailable)");
                    }
                    finally
                    {
                        outputIds.Add(dev.ID);
                        dev.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error enumerating devices: {ex.Message}";
            }

            inputDeviceIds = inputIds.ToArray();
            outputDeviceIds = outputIds.ToArray();

            // ===== Sélection initiale avec comparaison ID =====
            if (engine.InputDevice != null)
            {
                var idx = Array.IndexOf(inputDeviceIds, engine.InputDevice.ID);
                if (idx >= 0)
                    InputDeviceComboBox.SelectedIndex = idx;
            }
            else if (InputDeviceComboBox.Items.Count > 0)
                InputDeviceComboBox.SelectedIndex = 0;

            if (engine.OutputDevice != null)
            {
                var idx = Array.IndexOf(outputDeviceIds, engine.OutputDevice.ID);
                if (idx >= 0)
                    OutputDeviceComboBox.SelectedIndex = idx;
            }
            else if (OutputDeviceComboBox.Items.Count > 0)
                OutputDeviceComboBox.SelectedIndex = 0;

            // ===== Driver / SampleRate / BufferSize =====
            DriverComboBox.SelectedIndex = engine.Driver switch
            {
                AudioDriver.WASAPI_Exclusive => 1,
                AudioDriver.ASIO => 2,
                _ => 0
            };

            SampleRateComboBox.SelectedIndex = engine.SampleRate switch
            {
                48000 => 1,
                96000 => 2,
                _ => 0
            };

            BufferSizeComboBox.SelectedIndex = engine.BufferSize switch
            {
                64 => 0,
                128 => 1,
                256 => 2,
                512 => 3,
                _ => 2
            };

            // Populate ASIO drivers list
            PopulateAsioList();

            // Quand l'utilisateur change le driver ASIO, on détecte les canaux automatiquement
            AsioDriverComboBox.SelectionChanged += (_, __) =>
            {
                var name = GetCurrentAsioDriverName();
                if (!string.IsNullOrEmpty(name))
                    AutoPopulateAsioChannels(name);
            };

            // Répondre aux changements de driver pour activer/désactiver les contrôles pertinents
            DriverComboBox.SelectionChanged += (_, __) =>
            {
                UpdateDriverControls();
                // UI simplifiée: masquer devices WASAPI si ASIO choisi
                int idx = DriverComboBox.SelectedIndex;
                bool isAsio = idx == 2;
                InputDeviceComboBox.IsVisible = !isAsio;
                OutputDeviceComboBox.IsVisible = !isAsio;
            };

            // Appliquer l'état initial des contrôles
            UpdateDriverControls();

            // ===== VU-meter =====
            vuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            vuTimer.Tick += (_, __) => UpdateVUMeter();
            vuTimer.Start();

            // ===== Buttons =====
            StartSineButton.Click += (_, __) => StartSine();
            StopSineButton.Click += (_, __) => StopSine();
            ApplyButton.Click += (_, __) => ApplySettings();

            // Nouveau : ouvre le panneau de contrôle ASIO du driver sélectionné
            OpenAsioControlPanelButton.Click += (_, __) => OpenAsioControlPanel();

            // Test routing button (joue court tone sur la paire de sorties ASIO choisie)
            TestRoutingButton.Click += (_, __) => TestRouting();

            // ===== Stop Sine à la fermeture =====
            this.Closing += (_, __) =>
            {
                StopSine(); // monitoring principal reste actif
            };
        }

        private void TestRouting()
        {
            // récupère la paire de sorties sélectionnée et lance le test
            int outLeft = AsioOutLeftComboBox.SelectedIndex >= 0 ? AsioOutLeftComboBox.SelectedIndex : 0;
            int outRight = AsioOutRightComboBox.SelectedIndex >= 0 ? AsioOutRightComboBox.SelectedIndex : outLeft;

            // bouton test simple : 800 Hz pendant 800 ms
            engine.StartAsioTest(new int[] { outLeft, outRight }, 800, 800f, 0.5f);

            StatusLabel.Text = $"Test tone started on ASIO outputs Ch{outLeft + 1}/Ch{outRight + 1}";
        }

        private void PopulateAsioList()
        {
            try
            {
                var list = AudioEngine.GetAsioDrivers();
                asioDriverNames = list.ToArray();

                AsioDriverComboBox.Items.Clear();
                foreach (var name in asioDriverNames)
                    AsioDriverComboBox.Items.Add(name);

                if (AsioDriverComboBox.Items.Count > 0)
                {
                    AsioDriverComboBox.SelectedIndex = 0;
                    // auto probe for the first driver
                    AutoPopulateAsioChannels(asioDriverNames[0]);
                }
            }
            catch (Exception ex)
            {
                asioDriverNames = Array.Empty<string>();
                AsioDriverComboBox.Items.Clear();
                StatusLabel.Text = $"ASIO enumeration error: {ex.Message}";
            }
        }

        // Nouvelle méthode : détecte counts via AudioEngine.ProbeAsioChannels et remplit les combos selon counts
        private void AutoPopulateAsioChannels(string driverName)
        {
            asioInputCount = 0;
            asioOutputCount = 0;

            try
            {
                var (inCnt, outCnt) = engine.ProbeAsioChannels(driverName);
                asioInputCount = inCnt;
                asioOutputCount = outCnt;
            }
            catch
            {
                asioInputCount = 0;
                asioOutputCount = 0;
            }

            // fallback raisonnable si la detection a echoué
            if (asioInputCount <= 0) asioInputCount = 8;
            if (asioOutputCount <= 0) asioOutputCount = 8;

            // remplit dynamiquement combos pour les canaux detectés
            AsioInLeftComboBox.Items.Clear();
            AsioInRightComboBox.Items.Clear();
            AsioOutLeftComboBox.Items.Clear();
            AsioOutRightComboBox.Items.Clear();

            for (int i = 0; i < Math.Max(asioInputCount, asioOutputCount); i++)
            {
                string label = $"Ch {i + 1}";
                if (i < asioInputCount)
                {
                    AsioInLeftComboBox.Items.Add(label);
                    AsioInRightComboBox.Items.Add(label);
                }
                if (i < asioOutputCount)
                {
                    AsioOutLeftComboBox.Items.Add(label);
                    AsioOutRightComboBox.Items.Add(label);
                }
            }

            // default : Main 1/2 -> on sélectionne 1 et 2 si disponibles, sinon première paire
            if (AsioInLeftComboBox.Items.Count >= 2)
            {
                AsioInLeftComboBox.SelectedIndex = 0;
                AsioInRightComboBox.SelectedIndex = 1;
            }
            else if (AsioInLeftComboBox.Items.Count >= 1)
            {
                AsioInLeftComboBox.SelectedIndex = 0;
                AsioInRightComboBox.SelectedIndex = 0;
            }

            if (AsioOutLeftComboBox.Items.Count >= 2)
            {
                AsioOutLeftComboBox.SelectedIndex = 0;
                AsioOutRightComboBox.SelectedIndex = 1;
            }
            else if (AsioOutLeftComboBox.Items.Count >= 1)
            {
                AsioOutLeftComboBox.SelectedIndex = 0;
                AsioOutRightComboBox.SelectedIndex = 0;
            }

            DriverNoteText.Text = $"ASIO driver '{driverName}' detected: {asioInputCount} in / {asioOutputCount} out. Default mapping set to Ch1/Ch2.";
        }

        private void UpdateDriverControls()
        {
            int idx = DriverComboBox.SelectedIndex;

            bool isAsio = idx == 2;
            bool isExclusive = idx == 1;

            AsioDriverComboBox.IsEnabled = isAsio;
            AsioDriverComboBox.IsVisible = isAsio;

            // afficher/masquer mapping ASIO
            AsioInLeftComboBox.IsEnabled = isAsio;
            AsioInLeftComboBox.IsVisible = isAsio;
            AsioInRightComboBox.IsEnabled = isAsio;
            AsioInRightComboBox.IsVisible = isAsio;
            AsioOutLeftComboBox.IsEnabled = isAsio;
            AsioOutLeftComboBox.IsVisible = isAsio;
            AsioOutRightComboBox.IsEnabled = isAsio;
            AsioOutRightComboBox.IsVisible = isAsio;

            InputDeviceComboBox.IsEnabled = !isAsio;
            OutputDeviceComboBox.IsEnabled = !isAsio;

            BufferSizeComboBox.IsEnabled = !isAsio;

            if (isAsio)
                DriverNoteText.Text = "ASIO sélectionné : choisis le driver ASIO (détection automatique des canaux).";
            else if (isExclusive)
                DriverNoteText.Text = "WASAPI Exclusive: le device sera ouvert en mode exclusif; sample rate doit correspondre.";
            else
                DriverNoteText.Text = "WASAPI Shared: latence et mix gérés par le système.";
        }

        private void UpdateVUMeter()
        {
            try
            {
                VuMeter.Value = engine.Level;
                LatencyLabel.Text = $"{engine.CalculatedLatencyMs:F1} ms";
            }
            catch { }
        }

        private void CpuOverloadChanged(bool overload)
        {
            Dispatcher.UIThread.Post(() =>
            {
                CpuWarningLabel.Text = overload ? "⚠️ CPU too slow for current buffer!" : "";
            });
        }

        private void StartSine()
        {
            engine.StartAudio();
            StatusLabel.Text = "Sine started";
        }

        private void StopSine()
        {
            engine.StopAudio();
            StatusLabel.Text = "Sine stopped";
        }

        private void ApplySettings()
        {
            try
            {
                var wasMonitoring = engine.IsMonitoring;

                if (engine.IsMonitoring)
                    engine.StopMonitoring();

                // Driver
                engine.Driver = DriverComboBox.SelectedIndex switch
                {
                    1 => AudioDriver.WASAPI_Exclusive,
                    2 => AudioDriver.ASIO,
                    _ => AudioDriver.WASAPI_Shared
                };

                // ASIO driver selection
                if (engine.Driver == AudioDriver.ASIO)
                {
                    if (AsioDriverComboBox.SelectedIndex >= 0 && AsioDriverComboBox.SelectedIndex < asioDriverNames.Length)
                    {
                        engine.SetAsioDriver(asioDriverNames[AsioDriverComboBox.SelectedIndex]);
                    }
                    else
                    {
                        StatusLabel.Text = "No ASIO driver selected. Falling back to WASAPI Shared.";
                        engine.Driver = AudioDriver.WASAPI_Shared;
                    }
                }

                // SampleRate
                engine.SampleRate = SampleRateComboBox.SelectedIndex switch
                {
                    1 => 48000,
                    2 => 96000,
                    _ => 44100
                };

                // BufferSize
                engine.BufferSize = BufferSizeComboBox.SelectedIndex switch
                {
                    0 => 64,
                    1 => 128,
                    2 => 256,
                    3 => 512,
                    _ => 256
                };

                // Devices — pour WASAPI on recrée les MMDevice via l'ID
                if (engine.Driver != AudioDriver.ASIO)
                {
                    var enumerator = new MMDeviceEnumerator();

                    engine.InputDevice = InputDeviceComboBox.SelectedIndex >= 0 && InputDeviceComboBox.SelectedIndex < inputDeviceIds.Length
                        ? enumerator.GetDevice(inputDeviceIds[InputDeviceComboBox.SelectedIndex])
                        : null;

                    engine.OutputDevice = OutputDeviceComboBox.SelectedIndex >= 0 && OutputDeviceComboBox.SelectedIndex < outputDeviceIds.Length
                        ? enumerator.GetDevice(outputDeviceIds[OutputDeviceComboBox.SelectedIndex])
                        : null;

                    if (engine.InputDevice == null || engine.OutputDevice == null)
                    {
                        StatusLabel.Text = "Please select valid input and output devices for WASAPI.";
                        return;
                    }
                }
                else
                {
                    // ASIO: set channel mapping from UI selections, mais valide d'abord avec les counts détectés
                    int inLeft = AsioInLeftComboBox.SelectedIndex >= 0 ? AsioInLeftComboBox.SelectedIndex : 0;
                    int inRight = AsioInRightComboBox.SelectedIndex >= 0 ? AsioInRightComboBox.SelectedIndex : inLeft;
                    int outLeft = AsioOutLeftComboBox.SelectedIndex >= 0 ? AsioOutLeftComboBox.SelectedIndex : 0;
                    int outRight = AsioOutRightComboBox.SelectedIndex >= 0 ? AsioOutRightComboBox.SelectedIndex : outLeft;

                    if (inLeft >= asioInputCount || inRight >= asioInputCount)
                    {
                        StatusLabel.Text = $"Invalid ASIO input mapping (driver has {asioInputCount} inputs).";
                        return;
                    }
                    if (outLeft >= asioOutputCount || outRight >= asioOutputCount)
                    {
                        StatusLabel.Text = $"Invalid ASIO output mapping (driver has {asioOutputCount} outputs).";
                        return;
                    }

                    // transmet au moteur (indices ASIO zero-based)
                    engine.SetAsioChannels(new int[] { inLeft, inRight }, new int[] { outLeft, outRight });

                    // pas d'MMDevice pour ASIO
                    engine.InputDevice = null;
                    engine.OutputDevice = null;
                }

                // Redémarre monitoring si les paramètres valides
                if (engine.Driver == AudioDriver.ASIO)
                {
                    if (!string.IsNullOrEmpty(GetCurrentAsioDriverName()))
                    {
                        engine.StartMonitoring();
                        StatusLabel.Text = "ASIO driver selected and monitoring started.";
                    }
                    else
                    {
                        StatusLabel.Text = "ASIO driver not set; no monitoring started.";
                    }
                }
                else
                {
                    engine.StartMonitoring();
                    StatusLabel.Text = "WASAPI settings applied and monitoring started.";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error applying settings: {ex.Message}";
            }
        }

        private string? GetCurrentAsioDriverName()
        {
            if (AsioDriverComboBox.SelectedIndex >= 0 && AsioDriverComboBox.SelectedIndex < asioDriverNames.Length)
                return asioDriverNames[AsioDriverComboBox.SelectedIndex];
            return null;
        }

        private void OpenAsioControlPanel()
        {
            var driver = GetCurrentAsioDriverName();
            if (string.IsNullOrEmpty(driver))
            {
                StatusLabel.Text = "No ASIO driver selected.";
                return;
            }

            try
            {
                // crée temporairement AsioOut pour appeler le control panel (beaucoup de builds NAudio exposent ShowControlPanel)
                using var probe = new NAudio.Wave.AsioOut(driver);

                // Cherche via reflection une méthode d'ouverture du panneau de contrôle
                var show = probe.GetType().GetMethod("ShowControlPanel", BindingFlags.Instance | BindingFlags.Public);
                if (show != null)
                {
                    show.Invoke(probe, null);
                }
                else
                {
                    // Certains builds peuvent avoir un nom différent ; essayer une propriété ou méthode contenant "Control"
                    var alt = probe.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                .FirstOrDefault(m => m.Name.IndexOf("Control", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (alt != null)
                    {
                        // invocation si signature sans param
                        if (alt.GetParameters().Length == 0)
                            alt.Invoke(probe, null);
                        else
                            StatusLabel.Text = "ASIO control panel requires parameters not supported here.";
                    }
                    else
                    {
                        StatusLabel.Text = "ASIO control panel not available in this NAudio build.";
                    }
                }

                // Après ouverture/fermeture du panneau, reprobe les channels pour rafraîchir les combos
                AutoPopulateAsioChannels(driver);
            }
            catch (TargetInvocationException tie)
            {
                StatusLabel.Text = $"ASIO control panel error: {tie.InnerException?.Message ?? tie.Message}";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Cannot open ASIO control panel: {ex.Message}";
            }
        }
    }
}