using System;
using System.Collections.Generic;
using System.Linq;

namespace AudioBlocks.App.Audio
{
    public class AudioEffects
    {
        private readonly List<IAudioEffect> effects = new();

        /// <summary>
        /// Master volume applied AFTER all effects. Range: 0..2 (1 = unity).
        /// </summary>
        public float MasterVolume { get; set; } = 1.0f;

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
                OnEffectsChanged?.Invoke();
        }

        public void MoveEffect(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= effects.Count) return;
            if (toIndex < 0 || toIndex >= effects.Count) return;
            if (fromIndex == toIndex) return;

            var item = effects[fromIndex];
            effects.RemoveAt(fromIndex);
            effects.Insert(toIndex, item);
            OnEffectsChanged?.Invoke();
        }

        public void InsertEffect(int index, IAudioEffect effect)
        {
            if (effect == null || effects.Contains(effect)) return;
            index = Math.Clamp(index, 0, effects.Count);
            effects.Insert(index, effect);
            OnEffectsChanged?.Invoke();
        }

        public int Count => effects.Count;

        public List<IAudioEffect> GetAllEffects() => effects.ToList();

        public T? GetEffect<T>() where T : class, IAudioEffect
        {
            foreach (var effect in effects)
                if (effect is T typed) return typed;
            return null;
        }

        public void Process(float[] buffer, int count)
        {
            foreach (var effect in effects)
                if (effect.Enabled) effect.Process(buffer, count);

            float vol = MasterVolume;
            if (vol != 1.0f)
                for (int i = 0; i < count; i++)
                    buffer[i] *= vol;
        }
    }
}
