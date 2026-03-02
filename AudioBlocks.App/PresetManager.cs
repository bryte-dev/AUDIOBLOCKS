using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AudioBlocks.App.Audio;
using AudioBlocks.App.Effects;

namespace AudioBlocks.App
{
    public class EffectConfig
    {
        public string Type { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public Dictionary<string, float> Parameters { get; set; } = new();
    }

    public class PresetData
    {
        public string Name { get; set; } = "";
        public float MasterVolume { get; set; } = 1.0f;
        public List<EffectConfig> Effects { get; set; } = new();
    }

    public static class PresetManager
    {
        private static readonly string PresetsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AudioBlocks", "Presets");

        private static readonly HashSet<string> ExcludedProps =
        [
            "Name", "Enabled", "GainReductionDb", "CurrentGateGain",
            "GainDb", "LowLevel", "MidLevel", "HighLevel", "Levels"
        ];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static void Save(PresetData preset)
        {
            Directory.CreateDirectory(PresetsDir);
            var path = Path.Combine(PresetsDir, SanitizeName(preset.Name) + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
        }

        public static void SaveToPath(PresetData preset, string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
        }

        public static PresetData? Load(string name)
        {
            var path = Path.Combine(PresetsDir, SanitizeName(name) + ".json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<PresetData>(File.ReadAllText(path));
        }

        public static PresetData? LoadFromPath(string path)
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<PresetData>(File.ReadAllText(path));
        }

        public static List<string> GetAll()
        {
            if (!Directory.Exists(PresetsDir)) return [];
            return Directory.GetFiles(PresetsDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n)
                .ToList();
        }

        public static void Delete(string name)
        {
            var path = Path.Combine(PresetsDir, SanitizeName(name) + ".json");
            if (File.Exists(path)) File.Delete(path);
        }

        public static PresetData CapturePreset(string name, AudioEffects effects)
        {
            var preset = new PresetData
            {
                Name = name,
                MasterVolume = effects.MasterVolume
            };

            foreach (var effect in effects.GetAllEffects())
            {
                preset.Effects.Add(new EffectConfig
                {
                    Type = GetEffectType(effect),
                    Enabled = effect.Enabled,
                    Parameters = ExtractParameters(effect)
                });
            }

            return preset;
        }

        public static void ApplyPreset(PresetData preset, AudioEffects effects)
        {
            foreach (var fx in effects.GetAllEffects())
                effects.RemoveEffect(fx);

            effects.MasterVolume = preset.MasterVolume;

            foreach (var config in preset.Effects)
            {
                var effect = EffectsLibraryWindow.CreateEffect(config.Type);
                if (effect == null) continue;

                effect.Enabled = config.Enabled;
                ApplyParameters(effect, config.Parameters);
                effects.AddEffect(effect);
            }
        }

        private static string GetEffectType(IAudioEffect effect) => effect switch
        {
            GainEffect => "Gain",
            CompressorEffect => "Compressor",
            NoiseGateEffect => "NoiseGate",
            EqEffect => "EQ",
            DistortionEffect => "Distortion",
            DelayEffect => "Delay",
            ReverbEffect => "Reverb",
            ChorusEffect => "Chorus",
            FuzzEffect => "Fuzz",
            GraphicEqEffect => "GraphicEQ",
            _ => "Unknown"
        };

        private static Dictionary<string, float> ExtractParameters(IAudioEffect effect)
        {
            var dict = new Dictionary<string, float>();

            foreach (var prop in effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (ExcludedProps.Contains(prop.Name)) continue;

                if (prop.PropertyType == typeof(float) && prop.CanRead && prop.CanWrite)
                    dict[prop.Name] = (float)prop.GetValue(effect)!;

                if (prop.PropertyType == typeof(float[]) && prop.CanRead)
                {
                    var arr = (float[])prop.GetValue(effect)!;
                    for (int i = 0; i < arr.Length; i++)
                        dict[$"{prop.Name}[{i}]"] = arr[i];
                }
            }

            return dict;
        }

        private static void ApplyParameters(IAudioEffect effect, Dictionary<string, float> parameters)
        {
            var type = effect.GetType();

            foreach (var (key, value) in parameters)
            {
                if (key.Contains('['))
                {
                    var bracketIdx = key.IndexOf('[');
                    var propName = key[..bracketIdx];
                    var index = int.Parse(key[(bracketIdx + 1)..^1]);
                    var prop = type.GetProperty(propName);
                    if (prop?.GetValue(effect) is float[] arr && index < arr.Length)
                        arr[index] = value;
                }
                else
                {
                    var prop = type.GetProperty(key);
                    if (prop != null && prop.PropertyType == typeof(float) && prop.CanWrite)
                        prop.SetValue(effect, value);
                }
            }
        }

        private static string SanitizeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
