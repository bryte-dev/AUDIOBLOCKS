# AudioBlocks — Documentation Technique Complète

> Application de traitement audio en temps réel développée en C# / .NET 8 avec Avalonia UI et NAudio.
> Fonctionne comme un pedalboard virtuel : le son entre par le micro/interface, traverse une chaîne d'effets, et ressort dans les haut-parleurs.

---

## Table des matières

1. [Vue d'ensemble du projet](#1-vue-densemble-du-projet)
2. [Architecture générale](#2-architecture-générale)
3. [Technologies utilisées et pourquoi](#3-technologies-utilisées-et-pourquoi)
4. [Audio Engine — Le cœur du système](#4-audio-engine--le-cœur-du-système)
5. [Interface IAudioEffect — Le contrat des effets](#5-interface-iaudioeffect--le-contrat-des-effets)
6. [Les effets audio — Explication de chaque algorithme](#6-les-effets-audio--explication-de-chaque-algorithme)
7. [Système de presets](#7-système-de-presets)
8. [Interface utilisateur](#8-interface-utilisateur)
9. [Le métronome](#9-le-métronome)
10. [L'enregistreur audio](#10-lenregistreur-audio)
11. [Glossaire audio pour non-initiés](#11-glossaire-audio-pour-non-initiés)

---

## 1. Vue d'ensemble du projet

### Ce que fait l'application

AudioBlocks est un **processeur d'effets audio en temps réel**. Concrètement :

1. Le son entre depuis un **microphone** ou une **interface audio** (guitare, voix, etc.)
2. Le signal passe à travers une **chaîne d'effets** configurable (distorsion, reverb, delay, etc.)
3. Le son modifié sort dans les **haut-parleurs** ou un **casque**
4. L'utilisateur peut **enregistrer** le résultat et l'**exporter en WAV**

C'est l'équivalent logiciel d'un pedalboard de guitariste ou d'une chaîne d'effets dans un DAW (Digital Audio Workstation) comme FL Studio ou Ableton Live.

### Structure des fichiers

```
AudioBlocks.App/
├── Audio/                    # Couche audio (pas d'UI ici)
│   ├── AudioEngine.cs        # Moteur audio principal (capture → effets → sortie)
│   ├── AudioEffects.cs       # Gestionnaire de la chaîne d'effets
│   ├── AudioRecorder.cs      # Enregistrement et export WAV
│   ├── IAudioEffect.cs       # Interface commune à tous les effets
│   ├── Metronome.cs          # Métronome synthétisé
│   └── SineWaveProvider.cs   # Générateur de signal test (onde sinusoïdale)
│
├── Effects/                  # Implémentations des effets audio
│   ├── GainEffect.cs         # Boost de volume
│   ├── NoiseGateEffect.cs    # Suppression du bruit de fond
│   ├── CompressorEffect.cs   # Compression dynamique
│   ├── DistortionEffect.cs   # Distorsion (overdrive doux)
│   ├── FuzzEffect.cs         # Fuzz (distorsion agressive)
│   ├── EqEffect.cs           # Égaliseur 3 bandes
│   ├── GraphicEqEffect.cs    # Égaliseur graphique 10 bandes
│   ├── DelayEffect.cs        # Écho/delay
│   ├── ReverbEffect.cs       # Réverbération (simulation de salle)
│   └── ChorusEffect.cs       # Chorus (épaississement du son)
│
├── UI/                       # Contrôles UI personnalisés
│   ├── KnobControl.cs        # Potentiomètre rotatif (comme sur un ampli)
│   ├── FaderControl.cs       # Fader linéaire (comme sur une table de mixage)
│   ├── LevelMeter.cs         # VU-mètre à LED (vert/orange/rouge)
│   └── GraphicEqControl.cs   # Contrôle visuel de l'EQ graphique
│
├── PresetManager.cs          # Sauvegarde/chargement de presets (JSON)
├── MainWindow.axaml(.cs)     # Fenêtre principale
├── EffectsLibraryWindow.axaml(.cs)  # Bibliothèque d'effets (fenêtre flottante)
├── AudioSettingsWindow.axaml(.cs)   # Paramètres audio (choix carte son, driver)
├── SplashWindow.axaml(.cs)   # Écran de démarrage
└── Program.cs                # Point d'entrée de l'application
```

---

## 2. Architecture générale

### Le flux audio (comment le son circule)

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Carte son   │────▶│  AudioEngine │────▶│ AudioEffects │────▶│  Carte son   │
│  (entrée)    │     │  (capture)   │     │  (chaîne FX) │     │  (sortie)    │
└──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘
      Micro              Convertit             Chaque                Reconvertit
      ou                 les bytes             effet                 en bytes
      guitare            en floats             modifie               pour les
                         [-1, +1]              le buffer             haut-parleurs
```

### Pourquoi des `float` entre -1 et +1 ?

Le son numérique est une suite de nombres. Chaque nombre représente la position du haut-parleur à un instant donné :
- **+1.0** = haut-parleur poussé au maximum vers l'avant
- **0.0** = haut-parleur au repos (silence)
- **-1.0** = haut-parleur tiré au maximum vers l'arrière

On appelle chaque nombre un **sample** (échantillon). À 48000 Hz, il y a 48 000 samples par seconde.
Le format `float` (nombre à virgule flottante 32 bits) est le standard de l'industrie audio pour le traitement en temps réel car il offre une précision suffisante tout en étant rapide à calculer.

### Le pattern Observer (événements)

L'application utilise des **événements C#** pour communiquer entre les couches sans couplage fort :

```csharp
// AudioEffects signale quand la chaîne change
public event Action? OnEffectsChanged;

// L'UI s'abonne pour se redessiner automatiquement
engine.Effects.OnEffectsChanged += () => Dispatcher.UIThread.Post(UpdateEffectsPanel);
```

**Pourquoi ?** L'audio tourne sur un thread séparé à très haute priorité. L'UI tourne sur le thread principal. Ils ne peuvent pas s'appeler directement sans risquer des blocages (deadlocks). Les événements + `Dispatcher.UIThread.Post()` permettent une communication thread-safe.

---

## 3. Technologies utilisées et pourquoi

### .NET 8 (C#)

- **Pourquoi** : Langage typé, performant, avec un excellent garbage collector. Le C# moderne (pattern matching, records, default interface methods) rend le code concis.
- **Pourquoi pas C++** : Trop complexe pour un projet solo. Le C# avec `unsafe` et `Span<T>` offre des performances suffisantes pour l'audio en temps réel.

### Avalonia UI 11

- **Quoi** : Framework UI cross-platform (Windows, macOS, Linux), similaire à WPF.
- **Pourquoi Avalonia et pas WPF** : WPF ne fonctionne que sur Windows. Avalonia est cross-platform et utilise le même langage XAML.
- **Pourquoi pas WinForms** : WinForms n'a pas de rendu vectoriel. Les contrôles custom (knobs, meters) seraient beaucoup plus difficiles à dessiner.
- **AXAML** : C'est le format de description d'interface d'Avalonia (comme XAML pour WPF). Il décrit la disposition des contrôles de manière déclarative.

### NAudio 2.2.1

- **Quoi** : Bibliothèque audio .NET qui donne accès aux APIs audio Windows (WASAPI, ASIO).
- **Pourquoi** : C'est la référence en .NET pour l'audio bas niveau. Elle gère la communication avec les pilotes audio (WASAPI pour les cartes son standard, ASIO pour les interfaces audio professionnelles).
- **Ce qu'on utilise** : `WasapiCapture` (capture), `WasapiOut` (sortie), `AsioOut` (ASIO), `WaveFileWriter` (export WAV).

### WASAPI vs ASIO (les deux modes de communication avec la carte son)

| | WASAPI | ASIO |
|---|---|---|
| **Quoi** | API audio standard de Windows | Protocole professionnel de Steinberg |
| **Latence** | ~10-30ms (Shared), ~3-10ms (Exclusive) | ~1-5ms |
| **Compatibilité** | Toute carte son Windows | Nécessite un driver ASIO |
| **Usage** | Grand public | Studios, musiciens |

**Latence** = le délai entre le moment où vous jouez une note et le moment où vous l'entendez. En dessous de ~10ms, le cerveau perçoit le son comme instantané.

---

## 4. Audio Engine — Le cœur du système

### `AudioEngine.cs` — Ce qu'il fait

Le moteur audio est le chef d'orchestre. Il :
1. **Ouvre** la carte son (entrée + sortie)
2. **Reçoit** les samples bruts depuis le micro
3. **Convertit** les bytes en floats
4. **Appelle** la chaîne d'effets pour modifier le signal
5. **Reconvertit** les floats en bytes
6. **Envoie** le résultat aux haut-parleurs

### Le callback audio (le concept le plus important)

```csharp
private void OnWasapiData(object? sender, WaveInEventArgs e)
{
    // Cette méthode est appelée automatiquement par le driver audio
    // chaque fois qu'un bloc de samples est disponible (~5ms)
    
    // 1. Convertir bytes → floats
    // 2. Traiter les effets
    ProcessAudioPipeline(frames);
    // 3. Reconvertir floats → bytes et envoyer à la sortie
    WriteToBuffer(frames, wf);
}
```

**Concept clé** : On ne contrôle pas QUAND cette méthode est appelée. C'est le **driver audio** qui l'appelle. C'est un **callback** — le driver nous "rappelle" quand il a besoin de données. C'est pour ça que tout le code dans cette méthode doit être **extrêmement rapide** : si on prend trop de temps, le son craque (buffer underrun).

### Le pipeline de traitement

```csharp
private void ProcessAudioPipeline(int frames)
{
    bool playing = Recorder.IsPlaying;

    if (playing)
    {
        // Mode lecture : on lit l'enregistrement au lieu du micro
        int read = Recorder.ReadPlayback(floatBuffer, frames);
    }
    else
    {
        // Mode normal : on applique les effets au signal du micro
        Effects.Process(floatBuffer, frames);
        // Et on enregistre si demandé
        Recorder.WriteSamples(floatBuffer, frames);
    }

    // Le métronome est toujours mixé par-dessus
    Metronome.Process(floatBuffer, frames);
}
```

### `AudioEffects.cs` — La chaîne d'effets

```csharp
public void Process(float[] buffer, int count)
{
    // Chaque effet modifie le buffer à la suite
    foreach (var effect in effects)
        if (effect.Enabled) effect.Process(buffer, count);

    // Volume master appliqué après tous les effets
    float vol = MasterVolume;
    if (vol != 1.0f)
        for (int i = 0; i < count; i++)
            buffer[i] *= vol;
}
```

**Pourquoi un seul buffer modifié en place ?** Pour la performance. Créer un nouveau tableau à chaque bloc audio (toutes les ~5ms) causerait des allocations mémoire et des pauses du garbage collector, ce qui ferait craquer le son.

### Propagation du sample rate

```csharp
public int SampleRate
{
    set
    {
        sampleRate = value;
        foreach (var effect in effects)
            effect.SetSampleRate(value);  // Chaque effet recalcule ses coefficients
    }
}
```

**Pourquoi c'est nécessaire** : Les filtres audio (tone, damping, etc.) utilisent des coefficients mathématiques qui dépendent du sample rate. Un filtre passe-bas à 1000 Hz n'utilise pas les mêmes calculs à 44100 Hz qu'à 96000 Hz. Si on ne propage pas le sample rate, les filtres sonnent faux.

### `ArrayPool<float>` — Réutilisation mémoire

```csharp
private readonly ArrayPool<float> pool = ArrayPool<float>.Shared;
```

**Pourquoi** : Au lieu de créer un nouveau `float[]` à chaque callback audio, on emprunte un tableau au pool et on le rend après. Cela évite les allocations mémoire répétées qui déclencheraient le garbage collector et causeraient des craquements audio.

### `unsafe` et pointeurs (ASIO)

```csharp
private static unsafe void ReadAsioInput(IntPtr buf, float[] dest, int n, AsioSampleType t)
{
    switch (t)
    {
        case AsioSampleType.Int32LSB:
            int* s = (int*)buf;
            for (int i = 0; i < n; i++) dest[i] = s[i] / (float)int.MaxValue;
            break;
    }
}
```

**Pourquoi `unsafe`** : Le driver ASIO donne accès direct à la mémoire de la carte son via des pointeurs (`IntPtr`). Pour lire/écrire efficacement ces données, on utilise des pointeurs C-style. Le mot-clé `unsafe` est nécessaire car le compilateur C# ne peut pas vérifier la sécurité mémoire de ce code.

**Ce que fait le code** : Il convertit des entiers 32 bits (format de la carte son) en floats [-1, +1] (format de travail) en divisant par `int.MaxValue` (2 147 483 647).

---

## 5. Interface IAudioEffect — Le contrat des effets

```csharp
public interface IAudioEffect
{
    string Name { get; }                          // Nom affiché dans l'UI
    bool Enabled { get; set; }                    // Activer/désactiver
    void Process(float[] buffer, int count);      // Modifier le signal
    void SetSampleRate(int sampleRate) { }        // Adapter au sample rate
    float GainReductionDb => 0f;                  // Pour le metering (compresseur)
}
```

### Pourquoi une interface ?

Le **polymorphisme** : `AudioEffects.Process()` ne sait pas quel effet il appelle. Il appelle juste `effect.Process()` sur chaque effet de la liste. L'interface garantit que tous les effets ont la même signature.

```csharp
// Sans interface, il faudrait faire :
if (effect is DistortionEffect d) d.Process(buffer, count);
else if (effect is ReverbEffect r) r.Process(buffer, count);
// ... pour chaque effet → pas maintenable

// Avec interface :
effect.Process(buffer, count);  // Fonctionne pour TOUT effet
```

### La méthode `Process(float[] buffer, int count)`

C'est le cœur de chaque effet. Elle reçoit :
- `buffer` : le tableau de samples à modifier **en place** (pas de copie)
- `count` : le nombre de samples valides dans le tableau

Chaque effet lit les samples, les modifie selon son algorithme, et écrit le résultat dans le même tableau.

### Default Interface Methods (`SetSampleRate`, `GainReductionDb`)

```csharp
void SetSampleRate(int sampleRate) { }  // Implémentation par défaut = ne rien faire
float GainReductionDb => 0f;            // Par défaut = pas de réduction
```

**Pourquoi** : C# 8+ permet de donner une implémentation par défaut dans une interface. Ainsi, les effets simples (comme `GainEffect`) n'ont pas besoin d'implémenter ces méthodes — ils héritent du comportement par défaut. Seuls les effets qui en ont besoin (ceux avec des filtres, ou le compresseur) les redéfinissent.

---

## 6. Les effets audio — Explication de chaque algorithme

### 6.1 GainEffect — Le plus simple

```csharp
public void Process(float[] buffer, int count)
{
    float g = Gain;
    for (int i = 0; i < count; i++)
        buffer[i] *= g;
}
```

**Ce qu'il fait** : Multiplie chaque sample par un facteur. `Gain = 2.0` → le son est 2× plus fort (+6 dB).

**Analogie** : C'est comme tourner le bouton de volume d'un ampli. Simple multiplication.

---

### 6.2 NoiseGateEffect — Supprimer le bruit de fond

**Le problème** : Quand on ne joue pas de guitare, le micro capte du souffle, du buzz électrique, etc.

**La solution** : Couper le son quand il passe en dessous d'un certain volume (le seuil / threshold).

```
Signal:     ▓▓▓▓░░░░░░▓▓▓▓▓▓▓░░░░░
Threshold:  ────────────────────────  (ligne horizontale)
Sortie:     ▓▓▓▓              ▓▓▓▓▓▓▓
              ^                       ^
              Le son est trop         Le son repasse
              faible → coupé          au-dessus → ouvert
```

**Concepts clés dans le code** :

- **Envelope follower** : suit le volume du signal en temps réel
  ```csharp
  envelope += envCoeff * (abs - envelope);  // Lissage exponentiel
  ```
  On ne peut pas juste comparer chaque sample au seuil — le signal audio oscille trop vite. L'envelope follower "lisse" les variations pour obtenir le volume moyen.

- **Hystérésis** : le gate s'ouvre à un seuil et se ferme à un seuil plus bas
  ```csharp
  float openThresh = Threshold;
  float closeThresh = Threshold * (1f - Hysteresis);  // 40% plus bas
  ```
  **Pourquoi** : Sans hystérésis, le gate s'ouvrirait et se fermerait rapidement quand le signal est pile au seuil, créant un "tremblement" audible (chattering).

- **Attack/Release** : le gain monte/descend progressivement
  ```csharp
  if (target > gateGain)
      gateGain += attackCoeff * (target - gateGain);   // Ouvre progressivement
  else
      gateGain += releaseCoeff * (target - gateGain);  // Ferme progressivement
  ```
  **Pourquoi** : Couper le son instantanément créerait un "clic" audible. La transition douce élimine ces artefacts.

---

### 6.3 CompressorEffect — Réduire la dynamique

**Le problème** : La différence entre les passages doux et forts est trop grande.

**La solution** : Réduire automatiquement le volume quand le signal dépasse un seuil.

```
Entrée:    ░░▓▓▓▓▓▓▓████████░░░░
                      ^^^^^^^^ trop fort
Sortie:    ░░▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░
                      ^^^^^^^^ atténué
```

**Traitement en dB (décibels)** :
```csharp
float inputDb = abs < 1e-8f ? -160f : 20f * MathF.Log10(abs);
```

**Pourquoi travailler en dB** : Les décibels sont une échelle logarithmique. L'oreille humaine perçoit le volume de façon logarithmique : doubler le volume perçu nécessite 10× plus d'énergie. Les dB reflètent cette perception.

**Soft knee** (genou doux) :
```csharp
if (knee > 0f && MathF.Abs(overDb) < knee / 2f)
{
    float x = overDb + knee / 2f;
    gainDb = -(x * x) / (2f * knee) * (1f - 1f / ratio);
}
```

**Pourquoi** : Sans soft knee, la compression s'active brusquement au seuil exact. Le soft knee crée une transition progressive — la compression commence doucement quelques dB avant le seuil et atteint sa force maximale quelques dB après. C'est plus musical.

---

### 6.4 DistortionEffect — Overdrive doux

**Le concept** : Écrêter le signal de manière douce pour ajouter des harmoniques.

```csharp
float ws = MathF.Tanh(sample * driveGain) * compensation;
```

**`tanh()` (tangente hyperbolique)** : Cette fonction mathématique compresse doucement les valeurs vers ±1. C'est le cœur de la distorsion :
- Signal faible → passe quasi inchangé (zone linéaire)
- Signal fort → compressé vers ±1 (zone de saturation)

```
Entrée:     /\      /\
           /  \    /  \
Sortie:    /‾‾\   /‾‾\     ← Les pics sont "arrondis" (soft clipping)
```

**Pourquoi `tanh` et pas juste `Math.Clamp` ?** Le clamp coupe net (hard clipping), ce qui crée des harmoniques aiguës désagréables. Le `tanh` arrondit la transition, ce qui sonne plus "chaud" et musical — comme un ampli à lampes qui sature naturellement.

**Anti-aliasing (suréchantillonnage 2×)** :
```csharp
float up1 = (upFilterState + clipped) * 0.5f;  // Sample interpolé
float up2 = clipped;                             // Sample original
float ws1 = MathF.Tanh(up1 * driveGain);
float ws2 = MathF.Tanh(up2 * driveGain);
float wet = (ws1 + ws2) * 0.5f;                 // Moyenne
```

**Pourquoi** : La distorsion crée des harmoniques à haute fréquence. Si ces fréquences dépassent la moitié du sample rate (théorème de Nyquist), elles se "replient" en basses fréquences parasites (aliasing). Le suréchantillonnage 2× double temporairement la fréquence d'analyse pour éviter ce phénomène.

---

### 6.5 FuzzEffect — Distorsion agressive

**Différences fondamentales avec la Distortion** :

| | Distortion | Fuzz |
|---|---|---|
| Clipping | `tanh()` — doux, arrondi | **Hard clip** — coupe net, carré |
| Symétrie | Identique haut/bas | **Asymétrique** — le haut coupe plus tôt |
| Harmoniques | Paires + impaires | Ajout d'**harmoniques octave** (rectification) |
| Tone | 500 Hz – 15 kHz | **200 Hz – 3 kHz** (plus sombre) |

**Hard clipping asymétrique** :
```csharp
float posClip = 0.8f / (1f + asymmetry);  // Côté positif : clip bas (~0.4)
float negClip = 0.8f + asymmetry * 0.3f;  // Côté négatif : clip haut (~1.1)

if (x > posClip) wet = posClip;       // Coupe net en haut
else if (x < -negClip) wet = -negClip; // Coupe net en bas (plus tard)
else wet = x;                           // Passe tel quel
```

**Pourquoi asymétrique** : Dans un vrai circuit de fuzz (transistor au silicium), le transistor ne conduit pas de la même façon dans les deux sens. Ça crée des harmoniques **paires** (octave, etc.) qui donnent le son "nasal" caractéristique.

**Rectification partielle (effet octave)** :
```csharp
float rectMix = asymmetry * 0.3f;
x = x * (1f - rectMix) + MathF.Abs(x) * rectMix;
```

`MathF.Abs(x)` "replie" la partie négative vers le haut. Ça double la fréquence fondamentale → effet d'octave. Le `rectMix` contrôle l'intensité de cet effet.

**Noise gate intégré** :
```csharp
float gateMask = gateEnv > Gate ? 1f : gateEnv / (Gate + 1e-8f);
gateMask *= gateMask;
x *= gateMask;
```

**Pourquoi** : Le fuzz amplifie énormément le signal (jusqu'à 120×). Le moindre bruit de fond est aussi amplifié → il faut un gate pour couper le bruit quand on ne joue pas.

**DC blocking filter** :
```csharp
float dcOut = wet - dcPrev + 0.995f * dcBlock;
```

**Pourquoi** : Le clipping asymétrique décale le signal vers le haut ou le bas (composante continue / DC offset). Sans ce filtre, le haut-parleur serait poussé dans une direction en permanence, ce qui réduit sa marge de mouvement et peut l'endommager. Le filtre enlève cette composante continue sans affecter le son audible.

---

### 6.6 EqEffect — Égaliseur 3 bandes

**Le concept** : Séparer le son en 3 plages de fréquences (graves, médiums, aigus) et ajuster le volume de chacune indépendamment.

```csharp
// Filtre passe-bas 2 passes pour isoler les graves (<300 Hz)
lp1 += lpCoeff * (input - lp1);
lp2 += lpCoeff * (lp1 - lp2);
float low = lp2;

// Filtre passe-haut 2 passes pour isoler les aigus (>3000 Hz)
hp1 += hpCoeff * (input - hp1);
hp2 += hpCoeff * (hp1 - hp2);
float high = input - hp2;

// Les médiums = tout ce qui reste
float mid = input - low - high;

// Appliquer les gains séparément
buffer[i] = low * lowGain + mid * midGain + high * highGain;
```

**Filtre passe-bas (one-pole)** :
```csharp
lpState += coefficient * (input - lpState);
```

C'est un **filtre IIR du 1er ordre** (Infinite Impulse Response). Le principe : à chaque sample, on fait une moyenne pondérée entre la valeur actuelle et la valeur précédente du filtre. Le `coefficient` détermine la fréquence de coupure :
- Coefficient proche de 0 → seules les très basses fréquences passent
- Coefficient proche de 1 → tout passe (pas de filtrage)

La formule du coefficient : `c = 1 - exp(-2π × fréquence / sampleRate)`. C'est dérivé de la conversion d'un filtre analogique RC (résistance-capacité) en filtre numérique.

**Pourquoi 2 passes** : Un filtre 1 pôle a une pente de -6 dB/octave (faible). Deux filtres en cascade donnent -12 dB/octave → séparation plus nette entre les bandes.

---

### 6.7 GraphicEqEffect — Égaliseur 10 bandes (filtres biquad)

**Différence avec l'EQ 3 bandes** : Au lieu de 3 bandes fixes, 10 bandes à fréquences ISO standard (31 Hz à 16 kHz), chacune avec un filtre **biquad peaking**.

**Filtre biquad** :
```csharp
float output = b0 * sample + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
```

C'est un **filtre IIR du 2ème ordre**. Il utilise :
- Les 2 samples d'entrée précédents (`x1`, `x2`)
- Les 2 samples de sortie précédents (`y1`, `y2`)
- 5 coefficients (`b0`, `b1`, `b2`, `a1`, `a2`)

Les coefficients sont calculés à partir de la fréquence centrale, du gain en dB, et de la largeur de bande (Q) selon les **formules de Robert Bristow-Johnson** (standard de l'industrie pour les filtres audio numériques).

---

### 6.8 DelayEffect — Écho

**Le concept** : Stocker le signal dans un buffer circulaire et le relire avec un retard.

```csharp
private readonly float[] delayBuf = new float[192000];  // ~2 secondes à 96kHz
private int writePos;

// Lire le sample retardé
int readPos = writePos - delaySamples;
if (readPos < 0) readPos += delayBuf.Length;  // Buffer circulaire
float delayed = delayBuf[readPos];

// Écrire le nouveau sample + feedback (l'écho de l'écho)
delayBuf[writePos] = Math.Clamp(dry + filteredFb * fb, -1f, 1f);
```

**Buffer circulaire** : Au lieu de déplacer tous les samples à chaque itération (coûteux), on avance un index d'écriture. Quand il atteint la fin du tableau, il revient au début. C'est la structure de données idéale pour le delay car on a toujours besoin d'accéder à des données "anciennes".

**Feedback** : Le signal retardé est réinjecté à l'entrée → l'écho se répète. Un feedback de 0.5 = chaque répétition est 50% plus faible.

**Filtre HP dans le feedback** :
```csharp
fbHpState += hpCoeff * (delayed - fbHpState);
float filteredFb = delayed - fbHpState;
```

**Pourquoi** : Sans ce filtre, les basses fréquences s'accumulent dans la boucle de feedback et créent un grondement sourd. Le filtre passe-haut (~80 Hz) enlève les graves à chaque itération pour garder le delay propre.

---

### 6.9 ReverbEffect — Simulation de salle

**Le concept** : Dans une vraie pièce, le son rebondit sur les murs des centaines de fois. La reverb simule ces réflexions.

**Architecture Freeverb** (algorithme standard de l'industrie) :

```
Input ──┬── Comb 1 ──┐
        ├── Comb 2 ──┤       ┌── Allpass L1→L2→L3→L4 ── Output L
        ├── Comb 3 ──┤       │
        ├── Comb 4 ──┼── HP ─┤
        ├── Comb 5 ──┤       │
        ├── Comb 6 ──┤       └── Allpass R1→R2→R3→R4 ── Output R
        ├── Comb 7 ──┤
        └── Comb 8 ──┘
```

**Comb filter (filtre en peigne)** :
```csharp
float fbSig = input + readVal * fb;                    // Input + écho atténué
dampState = dampState * damp + fbSig * (1f - damp);   // Filtre passe-bas (damping)
buf[pos] = Math.Clamp(dampState, -1f, 1f);            // Écrire dans le delay
```

Chaque comb filter est un delay avec feedback. Les 8 combs ont des tailles différentes (nombres premiers) pour éviter que les échos se renforcent à certaines fréquences (ce qui sonnerait "métallique").

Le **damping** (amortissement) simule l'absorption du son par les murs : les aigus sont absorbés plus vite que les graves, comme dans une vraie pièce.

**Allpass filter (filtre passe-tout)** :
```csharp
float delayed = buf[pos];
float output = -input + delayed;
buf[pos] = input + delayed * coeff;
```

**Pourquoi** : Les allpass ajoutent de la **diffusion** — ils mélangent les réflexions pour créer une "queue" de reverb dense et lisse au lieu d'entendre des échos distincts. 4 allpass en cascade (au lieu de 2 dans la version initiale) donnent une diffusion beaucoup plus naturelle.

**Traitement stéréo** :
```csharp
// Combs impairs → canal gauche, combs pairs → canal droit
float combL = (r1 + r3 + r5 + r7) * 0.25f;
float combR = (r2 + r4 + r6 + r8) * 0.25f;
```

Les allpass L et R ont des tailles légèrement différentes (+23 samples pour R). Cette **décorrélation** fait que les canaux gauche et droit ne sont pas identiques → le son paraît large et "3D".

**Soft limiting** :
```csharp
diffL = MathF.Tanh(diffL);
diffR = MathF.Tanh(diffR);
```

**Pourquoi** : Avec un decay élevé, l'énergie s'accumule dans les comb filters et peut dépasser ±1 → clipping numérique (craquements). Le `tanh` compresse doucement le signal pour rester sous ±1 sans artefacts.

---

### 6.10 ChorusEffect — Épaississement du son

**Le concept** : Dupliquer le signal avec un micro-delay modulé par un LFO (oscillateur basse fréquence). La variation constante du delay crée un léger désaccordage qui "épaissit" le son.

```csharp
float lfo = (float)(0.5 + 0.5 * Math.Sin(lfoPhase));  // Oscille entre 0 et 1
float delaySamples = baseDelay + lfo * maxDelay;        // Delay varie dans le temps
```

**Interpolation de Hermite** :
```csharp
float c0 = s1;
float c1 = 0.5f * (s2 - s0);
float c2 = s0 - 2.5f * s1 + 2f * s2 - 0.5f * s3;
float c3 = 0.5f * (s3 - s0) + 1.5f * (s1 - s2);
float delayed = ((c3 * frac + c2) * frac + c1) * frac + c0;
```

**Pourquoi** : Le delay modulé tombe souvent "entre" deux samples (ex: 45.7 samples de retard). L'interpolation de Hermite utilise 4 samples voisins pour estimer la valeur entre eux avec une courbe lisse. C'est mieux que l'interpolation linéaire (2 points) car elle ne crée pas de bruit haute fréquence.

---

## 7. Système de presets

### `PresetManager.cs` — Sérialisation par réflection

**Le problème** : On veut sauvegarder l'état complet de la chaîne d'effets (quels effets, quels paramètres) sans écrire du code spécifique pour chaque effet.

**La solution** : La **réflection** C# — inspecter les propriétés d'un objet au runtime.

```csharp
foreach (var prop in effect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    if (prop.PropertyType == typeof(float) && prop.CanRead && prop.CanWrite)
        dict[prop.Name] = (float)prop.GetValue(effect)!;
}
```

**Ce que fait ce code** : Pour chaque effet, il trouve automatiquement toutes les propriétés publiques de type `float` (Drive, Tone, Mix, Level, etc.) et sauvegarde leur nom + valeur dans un dictionnaire.

**Pourquoi la réflection** : Si on ajoute un nouvel effet avec de nouveaux paramètres, la sérialisation fonctionne automatiquement sans modifier le `PresetManager`. C'est le principe **Open/Closed** (ouvert à l'extension, fermé à la modification).

**Format de sauvegarde** (JSON) :
```json
{
  "Name": "My Lead Tone",
  "MasterVolume": 0.85,
  "Effects": [
    {
      "Type": "NoiseGate",
      "Enabled": true,
      "Parameters": { "Threshold": 0.03, "Attack": 0.1, "Release": 0.4 }
    },
    {
      "Type": "Distortion",
      "Enabled": true,
      "Parameters": { "Drive": 0.6, "Tone": 0.65, "Mix": 1.0, "Level": 0.5 }
    }
  ]
}
```

**Deux modes de sauvegarde** :
1. **Quick save** : `%AppData%/AudioBlocks/Presets/` — pour les presets internes à l'app
2. **Export/Import** : File picker système — l'utilisateur choisit l'emplacement (partage, backup, etc.)

---

## 8. Interface utilisateur

### Contrôles custom (dessin vectoriel)

Tous les contrôles UI (knobs, faders, meters) sont dessinés **from scratch** en surchargeant la méthode `Render()` d'Avalonia :

```csharp
public override void Render(DrawingContext ctx)
{
    // Dessiner un arc pour le knob
    ctx.DrawEllipse(bodyBrush, bodyPen, center, radius, radius);
    ctx.DrawLine(pointerPen, from, to);  // Ligne indicatrice
}
```

**Pourquoi des contrôles custom plutôt que des sliders standard** : Les sliders rectangulaires d'Avalonia ne correspondent pas à l'UX d'une application audio. Les knobs rotatifs et les faders sont les contrôles standards des logiciels audio (FL Studio, Ableton, etc.).

### KnobControl — Double-clic pour saisie clavier

```csharp
DoubleTappedEvent.AddClassHandler<KnobControl>((x, e) =>
{
    x.isEditing = true;
    x.editText = "";
    x.Focusable = true;
    x.Focus();
});
```

**Pourquoi `AddClassHandler`** : En Avalonia, certains événements (comme `DoubleTapped`) ne sont pas disponibles comme méthodes virtuelles (`OnDoubleTapped` n'existe pas sur `Control`). Le class handler est le mécanisme officiel pour intercepter ces événements routés sur un type de contrôle custom.

### Pattern MVVM simplifié

L'application utilise un **code-behind** direct (pas de ViewModels séparés) pour des raisons de simplicité. Les contrôles AXAML déclarent la structure :

```xml
<Button x:Name="AddDistortionBtn" Classes="lib-item" Content="Distortion"/>
```

Et le code-behind wire les événements :
```csharp
AddDistortionBtn.Click += (_, _) => AddEffect(new DistortionEffect());
```

**Pourquoi pas MVVM complet** : Pour un projet de cette taille, MVVM ajouterait de la complexité (ViewModels, Commands, bindings) sans bénéfice majeur. Le code-behind est plus direct et plus lisible pour un seul développeur.

---

## 9. Le métronome

### Précision sub-sample

```csharp
double samplesPerBeat = (60.0 / bpm) * sampleRate;

if (sampleAccumulator >= samplesPerBeat)
{
    sampleAccumulator -= samplesPerBeat;  // Garde le résidu fractionnaire
    // → Déclencher le click
}
sampleAccumulator += 1.0;
```

**Pourquoi `double` et pas `int`** : À 128.5 BPM et 48000 Hz, un beat = 22 412.451... samples. Si on arrondit à 22 412, on perd 0.451 sample par beat. Après 1000 beats (~8 minutes), le décalage est de 451 samples = ~9.4 ms. Avec l'accumulateur `double`, l'erreur ne s'accumule **jamais** car le résidu fractionnaire est conservé.

### Synthèse du click

```csharp
// Downbeat : fondamentale + octave pour plus de punch
click = (float)(Math.Sin(clickPhase) * 0.7 + Math.Sin(clickPhase * 2.0) * 0.3);
```

Le click est **synthétisé** (pas un fichier audio lu depuis le disque). Avantage : zéro latence de chargement, adapté automatiquement au sample rate, et modifiable en temps réel (fréquence, durée, enveloppe).

---

## 10. L'enregistreur audio

### `AudioRecorder.cs`

**Thread-safety** :
```csharp
private readonly object lockObj = new();

public void WriteSamples(float[] buffer, int count)
{
    if (!isRecording) return;
    lock (lockObj)  // Un seul thread à la fois
    {
        for (int i = 0; i < count; i++)
            recordBuffer.Add(buffer[i]);
    }
}
```

**Pourquoi le `lock`** : `WriteSamples` est appelé depuis le thread audio. `StartPlayback`, `ExportWav`, etc. sont appelés depuis le thread UI. Sans le `lock`, les deux threads pourraient modifier `recordBuffer` simultanément → corruption de données, crash.

**Pré-allocation mémoire** :
```csharp
if (recordBuffer.Capacity < recordBuffer.Count + count)
    recordBuffer.Capacity = Math.Max(recordBuffer.Capacity * 2, recordBuffer.Count + count + 48000);
```

**Pourquoi** : `List<float>.Add()` réalloue le tableau interne quand il est plein (copie de tout le contenu). En pré-allouant par gros blocs (×2 ou +48000 = 1 seconde), on réduit le nombre de réallocations et donc les pauses dans le thread audio.

---

## 11. Glossaire audio pour non-initiés

| Terme | Explication simple |
|---|---|
| **Sample** | Un nombre représentant le son à un instant précis |
| **Sample rate** | Combien de samples par seconde (48000 Hz = 48000 mesures/sec) |
| **Buffer** | Un bloc de samples traités ensemble (~256 à la fois) |
| **Latence** | Délai entre jouer une note et l'entendre |
| **dB (décibel)** | Unité de volume. +6 dB = 2× plus fort. -6 dB = 2× moins fort |
| **Fréquence (Hz)** | Vitesse de vibration. 440 Hz = note La. Graves = bas Hz, aigus = haut Hz |
| **Clipping** | Le signal dépasse ±1 → distorsion non voulue, craquements |
| **Dry/Wet** | Dry = signal original, Wet = signal modifié par l'effet |
| **Feedback** | Réinjecter la sortie d'un effet dans son entrée |
| **LFO** | Oscillateur lent qui module un paramètre (vibrato, chorus) |
| **Callback** | Le driver audio appelle notre code quand il a besoin de données |
| **Thread audio** | Thread dédié au calcul du son, à très haute priorité |
| **IIR filter** | Filtre numérique récursif : la sortie dépend des entrées ET sorties précédentes |
| **Nyquist** | La fréquence max représentable = sample rate ÷ 2 |
| **DC offset** | Composante continue dans le signal (décalage vers le haut ou le bas) |
| **Aliasing** | Fréquences parasites créées quand on dépasse la limite de Nyquist |
| **WASAPI** | API audio standard de Windows |
| **ASIO** | Protocole audio professionnel basse latence |

---

*Documentation générée pour le projet AudioBlocks — CFC Informatique*
