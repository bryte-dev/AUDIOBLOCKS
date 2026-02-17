using AudioBlocks.App.Audio;
using AudioBlocks.App.Effects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace AudioBlocks.App
{
    public partial class EffectsLibraryWindow : Window
    {
        private readonly AudioEngine engine;

        internal static string? PendingEffectType;

        private readonly (string tag, Func<IAudioEffect> factory)[] effectItems =
        [
            ("Gain",       () => new GainEffect()),
            ("Compressor", () => new CompressorEffect()),
            ("NoiseGate",  () => new NoiseGateEffect()),
            ("EQ",         () => new EqEffect()),
            ("Distortion", () => new DistortionEffect()),
            ("Delay",      () => new DelayEffect()),
            ("Reverb",     () => new ReverbEffect()),
            ("Chorus",     () => new ChorusEffect()),
            ("Fuzz",       () => new FuzzEffect()),
        ];

        public EffectsLibraryWindow(AudioEngine engine)
        {
            InitializeComponent();
            this.engine = engine;

            SetupDragItem(DragGain);
            SetupDragItem(DragCompressor);
            SetupDragItem(DragGate);
            SetupDragItem(DragEq);
            SetupDragItem(DragDistortion);
            SetupDragItem(DragDelay);
            SetupDragItem(DragReverb);
            SetupDragItem(DragChorus);
            SetupDragItem(DragFuzz);

            PresetClean.PointerPressed += (_, _) => ApplyPreset("Clean");
            PresetCrunch.PointerPressed += (_, _) => ApplyPreset("Crunch");
            PresetLead.PointerPressed += (_, _) => ApplyPreset("Lead");
            PresetAmbient.PointerPressed += (_, _) => ApplyPreset("Ambient");
        }

        private void SetupDragItem(Border item)
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

                    PendingEffectType = effectType;

                    // Use new API: DataTransfer + DataFormat.Text + DoDragDropAsync
                    var data = new DataTransfer();
                    data.Set(DataFormat.Text, $"AudioBlocks:Effect:{effectType}");

                    var result = await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);

                    PendingEffectType = null;
                }
            };

            item.PointerReleased += (_, _) =>
            {
                if (pressPoint.HasValue && !dragStarted)
                {
                    pressPoint = null;
                    var fx = CreateEffect(effectType);
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

        public static IAudioEffect? CreateEffect(string effectType)
        {
            return effectType switch
            {
                "Gain" => new GainEffect(),
                "Compressor" => new CompressorEffect(),
                "NoiseGate" => new NoiseGateEffect(),
                "EQ" => new EqEffect(),
                "Distortion" => new DistortionEffect(),
                "Delay" => new DelayEffect(),
                "Reverb" => new ReverbEffect(),
                "Chorus" => new ChorusEffect(),
                "Fuzz" => new FuzzEffect(),
                _ => null
            };
        }

        private void ApplyPreset(string preset)
        {
            foreach (var fx in engine.Effects.GetAllEffects())
                engine.Effects.RemoveEffect(fx);

            switch (preset)
            {
                case "Clean":
                    engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.01f });
                    engine.Effects.AddEffect(new EqEffect { Low = -0.2f, Mid = 0.1f, High = 0.2f });
                    engine.Effects.AddEffect(new CompressorEffect { Threshold = 0.4f, Ratio = 3f, Makeup = 1.3f });
                    break;
                case "Crunch":
                    engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.02f });
                    engine.Effects.AddEffect(new GainEffect { Gain = 1.5f });
                    engine.Effects.AddEffect(new DistortionEffect { Drive = 0.35f, Tone = 0.5f, Mix = 1f, Level = 0.65f });
                    engine.Effects.AddEffect(new EqEffect { Low = 0.1f, Mid = 0.2f, High = -0.1f });
                    break;
                case "Lead":
                    engine.Effects.AddEffect(new NoiseGateEffect { Threshold = 0.03f });
                    engine.Effects.AddEffect(new GainEffect { Gain = 2.0f });
                    engine.Effects.AddEffect(new DistortionEffect { Drive = 0.6f, Tone = 0.65f, Mix = 1f, Level = 0.5f });
                    engine.Effects.AddEffect(new EqEffect { Low = -0.1f, Mid = 0.3f, High = 0.1f });
                    engine.Effects.AddEffect(new DelayEffect { Time = 0.35f, Feedback = 0.3f, Mix = 0.2f });
                    engine.Effects.AddEffect(new ReverbEffect { Mix = 0.15f, Decay = 0.4f, Damping = 0.5f });
                    break;
                case "Ambient":
                    engine.Effects.AddEffect(new EqEffect { Low = 0.2f, Mid = -0.2f, High = 0.3f });
                    engine.Effects.AddEffect(new ChorusEffect { Rate = 0.3f, Depth = 0.6f, Mix = 0.4f });
                    engine.Effects.AddEffect(new DelayEffect { Time = 0.6f, Feedback = 0.5f, Mix = 0.4f });
                    engine.Effects.AddEffect(new ReverbEffect { Mix = 0.5f, Decay = 0.8f, Damping = 0.6f });
                    break;
            }
            StatusLabel.Text = $"Preset: {preset}";
        }
    }
}
