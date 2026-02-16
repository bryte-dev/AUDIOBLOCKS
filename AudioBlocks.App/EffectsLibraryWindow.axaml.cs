using AudioBlocks.App.Audio;
using AudioBlocks.App.Effects;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;

namespace AudioBlocks.App
{
    public partial class EffectsLibraryWindow : Window
    {
        private readonly AudioEngine engine;

        public EffectsLibraryWindow(AudioEngine engine)
        {
            InitializeComponent();
            this.engine = engine;

            // Liste statique des effets disponibles
            AvailableEffectsList.ItemsSource = new string[] { "Gain", "Distortion", "Reverb" };

            // Mettre à jour la liste actuelle
            UpdateEffectList();

            // Écoute des changements
            engine.Effects.OnEffectsChanged += UpdateEffectList;

            AddEffectButton.Click += AddEffectButton_Click;
            RemoveEffectButton.Click += RemoveEffectButton_Click;
        }

        private void UpdateEffectList()
        {
            CurrentEffectsList.ItemsSource = engine.Effects.GetAllEffects()
                                                     .Select(e => e.Name)
                                                     .ToList();
        }

        private void AddEffectButton_Click(object? sender, RoutedEventArgs e)
        {
            if (AvailableEffectsList.SelectedItem == null) return;

            string name = AvailableEffectsList.SelectedItem.ToString()!;

            switch (name)
            {
                case "Gain": engine.Effects.AddEffect(new GainEffect()); break;
                case "Distortion": engine.Effects.AddEffect(new DistortionEffect()); break;
                case "Reverb": engine.Effects.AddEffect(new ReverbEffect()); break;
                default: return;
            }

            StatusLabel.Text = $"{name} added!";
        }

        private void RemoveEffectButton_Click(object? sender, RoutedEventArgs e)
        {
            if (CurrentEffectsList.SelectedItem == null) return;

            string name = CurrentEffectsList.SelectedItem.ToString()!;
            var effect = engine.Effects.GetAllEffects().FirstOrDefault(x => x.Name == name);

            if (effect != null)
            {
                engine.Effects.RemoveEffect(effect);
                StatusLabel.Text = $"{name} removed!";
            }
        }
    }
}
