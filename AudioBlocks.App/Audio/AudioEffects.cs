using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioBlocks.App.Audio
{
    public class AudioEffects
    {
        private readonly List<IAudioEffect> effects = new();

        public event Action? OnEffectsChanged;

        public void AddEffect(IAudioEffect effect)
        {
            if (effect != null && !effects.Contains(effect))
            {
                effects.Add(effect);
                OnEffectsChanged?.Invoke();
            }
        }

        public void RemoveEffect(IAudioEffect effect)
        {
            if (effects.Remove(effect))
            {
                OnEffectsChanged?.Invoke();
            }
        }

        public List<IAudioEffect> GetAllEffects() => effects.ToList();

        public T GetEffect<T>() where T : class, IAudioEffect
        {
            foreach (var effect in effects)
            {
                if (effect is T typed)
                    return typed;
            }
            return null;
        }

        public void Process(float[] buffer)
        {
            foreach (var effect in effects)
            {
                if (effect.Enabled)
                    effect.Process(buffer);
            }
        }
    }
}
