# AudioBlocks 🎛️

AudioBlocks is a desktop application that turns a computer into a **virtual audio pedalboard**.

It receives audio from a microphone, guitar, instrument, or audio interface, processes the signal through a configurable chain of real-time effects, and sends the modified sound back to the speakers or headphones.

The project is designed for musicians, audio enthusiasts, and anyone interested in experimenting with real-time audio processing.

> **Project status: released functional application**
>
> AudioBlocks is more advanced than a simple prototype. It includes a real-time audio engine, several audio effects, presets, recording, WAV export, a metronome, custom audio controls, audio device configuration, and publishing support.
>
> The project is also available on **itch.io**, making it a publicly distributable application rather than only a local development project.

---

## What Is AudioBlocks?

AudioBlocks is a software alternative to a physical effects pedalboard.

A typical audio flow looks like this:

```text
Microphone / Guitar / Audio Interface
                ↓
          AudioBlocks
                ↓
        Chain of audio effects
                ↓
        Speakers / Headphones
```

The user can add and configure effects such as:

- gain;
- noise gate;
- compressor;
- distortion;
- fuzz;
- equalizer;
- graphic equalizer;
- delay;
- reverb;
- chorus.

The effects can be enabled, disabled, reordered, and configured in real time.

---

## Project Goal

The main goal of AudioBlocks is to create a usable and understandable real-time audio processing environment.

The application aims to provide:

- a virtual pedalboard for musicians;
- a way to experiment with audio effects;
- a simpler alternative to more complex digital audio workstations;
- a practical demonstration of digital signal processing;
- a customizable environment for real-time audio experimentation;
- recording and export capabilities;
- a platform that can be distributed and used outside the development environment.

AudioBlocks is also an important technical project because it combines:

- desktop application development;
- real-time audio processing;
- multithreading;
- audio device management;
- digital signal processing;
- custom graphical controls;
- file management;
- preset serialization;
- user-oriented product design.

---

## Public Release

AudioBlocks is not only stored as source code on GitHub.

The project has also been prepared for distribution and is available on **itch.io**.

This means the project includes several elements associated with a released application:

- application branding;
- production metadata;
- application icon;
- version information;
- release folders;
- publish folders;
- self-contained Windows publishing;
- single-file executable configuration;
- release versions such as `v1.0.0`, `v1.0.1`, and `v1.0.2`.

The itch.io release makes it possible to present AudioBlocks as a finished downloadable application rather than only as a programming exercise.

> The itch.io page link is intentionally not included here because it is not stored in this repository metadata.

---

## Main Features

### Real-Time Audio Processing

AudioBlocks processes incoming audio continuously.

The audio engine:

1. opens the selected input and output devices;
2. receives audio samples;
3. converts the samples into a processable format;
4. sends them through the effect chain;
5. optionally records the processed signal;
6. mixes the metronome if enabled;
7. sends the final signal to the output device.

This process must happen quickly and consistently to avoid:

- audible latency;
- audio dropouts;
- clicks;
- crackling;
- interruptions.

### Effect Chain

The application includes a configurable chain of audio effects.

Each effect implements a common interface:

```text
IAudioEffect
```

The interface allows the audio engine to process different effects in a generic way without needing to know the internal implementation of every effect.

The effect chain can be processed in order:

```text
Effect 1
    ↓
Effect 2
    ↓
Effect 3
    ↓
Effect 4
    ↓
Master volume
```

Effects can be enabled or disabled individually.

### Available Effects

#### Gain

Controls the volume of the signal.

It multiplies the audio samples by a gain factor.

#### Noise Gate

Reduces background noise when the input signal is below a defined threshold.

This is useful for reducing:

- microphone noise;
- electrical hum;
- guitar pickup noise;
- background room noise.

The noise gate includes gradual attack and release behavior to avoid abrupt cuts and clicks.

#### Compressor

Reduces the dynamic range of the audio signal.

It makes loud sounds less loud and helps produce a more controlled output level.

The compressor includes concepts such as:

- threshold;
- ratio;
- attack;
- release;
- makeup gain;
- soft knee;
- gain reduction.

#### Distortion

Adds harmonic content by applying soft clipping to the signal.

It can be used to create an overdrive-style sound.

The implementation uses a smooth saturation curve instead of cutting the signal abruptly.

#### Fuzz

Creates a more aggressive and heavily distorted sound.

Compared with the distortion effect, fuzz uses stronger clipping and additional processing to create a harsher sound.

The effect includes features such as:

- asymmetric clipping;
- stronger gain;
- octave-like harmonic behavior;
- noise gating;
- DC blocking.

#### Three-Band EQ

Splits the audio signal into:

- low frequencies;
- mid frequencies;
- high frequencies.

The user can adjust these frequency ranges independently.

#### Graphic EQ

Provides multiple frequency bands for more precise tone shaping.

The graphic equalizer uses several filters covering frequencies from low bass to high treble.

#### Delay

Creates an echo by storing previous audio samples and playing them again after a configurable delay.

The delay includes:

- delay time;
- feedback;
- filtering;
- wet/dry mix.

#### Reverb

Simulates the sound of a room or acoustic space.

The implementation is based on a Freeverb-style structure using:

- comb filters;
- all-pass filters;
- damping;
- stereo processing;
- wet/dry mixing.

#### Chorus

Creates a thicker and wider sound by mixing delayed and modulated versions of the original signal.

The chorus uses an LFO to modulate the delay and create a subtle variation in pitch and timing.

---

## Preset System

AudioBlocks includes a preset system for saving and loading effect configurations.

A preset can contain:

- the list of effects;
- the effect order;
- enabled or disabled states;
- effect parameters;
- master volume;
- preset name.

Presets are stored using JSON.

Example workflow:

```text
Configure effects
    ↓
Save preset
    ↓
JSON file
    ↓
Load preset later
    ↓
Restore the complete audio setup
```

The preset manager uses reflection to automatically discover public effect parameters.

This means that adding a new effect can require little or no modification to the preset serialization system, as long as the effect follows the expected structure.

The application supports:

- quick saves in the application preset directory;
- exporting presets to a chosen location;
- importing presets from external files;
- sharing and backing up presets.

---

## Audio Recording

AudioBlocks includes an audio recorder.

The recorder captures the processed signal after the effects have been applied.

The recorded audio can be exported as a WAV file.

The recording workflow is:

```text
Input audio
    ↓
Effects chain
    ↓
Processed audio
    ↓
Recording buffer
    ↓
WAV export
```

The recorder is designed to be thread-safe because:

- audio samples are received on the audio thread;
- recording controls are operated from the user interface thread;
- export operations may occur after recording stops.

---

## Metronome

AudioBlocks includes a real-time metronome.

The metronome supports:

- tempo in BPM;
- synthesized click sounds;
- regular beat timing;
- downbeat emphasis;
- integration with the audio processing pipeline.

The click is generated mathematically rather than loaded from a file.

This provides:

- low loading overhead;
- sample-rate independence;
- real-time control;
- consistent timing.

The timing system uses a fractional sample accumulator to reduce drift over time.

---

## Audio Drivers and Devices

AudioBlocks supports different ways of communicating with audio hardware.

The project includes support for:

```text
WASAPI Shared
WASAPI Exclusive
ASIO
```

### WASAPI Shared

This is the standard Windows audio mode.

It is generally easier to use and compatible with most audio devices.

### WASAPI Exclusive

This mode gives the application more direct control over the audio device.

It can reduce latency but may prevent other applications from using the same device at the same time.

### ASIO

ASIO is designed for professional and low-latency audio work.

It is useful for:

- audio interfaces;
- music production;
- live performance;
- low-latency monitoring.

ASIO requires a compatible driver installed on the computer.

---

## Audio Settings

The audio settings window allows the user to configure:

- input device;
- output device;
- audio driver;
- sample rate;
- buffer size;
- ASIO driver;
- ASIO input channel;
- ASIO output channel.

The application can refresh the list of available devices and audio drivers.

Available settings depend on the hardware and drivers installed on the computer.

---

## Custom User Interface

AudioBlocks includes custom controls designed specifically for audio applications.

### Knob Control

A rotary control inspired by physical guitar pedals, amplifiers, and synthesizers.

It can be used for parameters such as:

- gain;
- tone;
- drive;
- mix;
- feedback;
- room size.

### Fader Control

A linear control inspired by mixing consoles.

It is useful for:

- volume;
- effect levels;
- frequency bands;
- master output.

### Level Meter

A visual meter that shows the current audio level.

It can indicate:

- normal signal levels;
- high levels;
- potential clipping.

### Graphic EQ Control

A custom interface for controlling the graphic equalizer bands.

The custom controls are drawn using Avalonia rendering instead of relying only on standard controls.

---

## Technology Stack

### Language and Runtime

- **C#**
- **.NET 8**
- **Windows x64**

### User Interface

- **Avalonia UI 11**
- **AXAML**
- **Material Design icon support**

### Audio

- **NAudio 2.2.1**
- **WASAPI**
- **ASIO**

### Data and Configuration

- **JSON**
- reflection-based preset serialization

### Publishing

- self-contained Windows build;
- single-file executable;
- native library extraction;
- compressed single-file publishing;
- production metadata;
- application icon.

---

## Project Structure

```text
AUDIOBLOCKS/
├── AudioBlocks.App/
│   ├── Audio/
│   │   ├── AudioEngine.cs             Main audio processing engine
│   │   ├── AudioEffects.cs            Effect chain manager
│   │   ├── AudioRecorder.cs           Recording and WAV export
│   │   ├── IAudioEffect.cs            Common effect interface
│   │   ├── Metronome.cs               Synthesized metronome
│   │   └── SineWaveProvider.cs        Test signal generator
│   │
│   ├── Effects/
│   │   ├── ChorusEffect.cs            Chorus
│   │   ├── CompressorEffect.cs        Dynamic compression
│   │   ├── DelayEffect.cs             Echo and delay
│   │   ├── DistortionEffect.cs        Soft distortion
│   │   ├── EqEffect.cs                Three-band equalizer
│   │   ├── FuzzEffect.cs              Aggressive distortion
│   │   ├── GainEffect.cs              Gain control
│   │   ├── GraphicEqEffect.cs         Multi-band equalizer
│   │   ├── NoiseGateEffect.cs         Background noise reduction
│   │   └── ReverbEffect.cs             Room simulation
│   │
│   ├── UI/
│   │   ├── FaderControl.cs             Custom fader
│   │   ├── GraphicEqControl.cs         Graphic EQ interface
│   │   ├── KnobControl.cs              Rotary knob
│   │   └── LevelMeter.cs               Audio level meter
│   │
│   ├── Assets/
│   │   ├── logo.ico
│   │   └── logo.png
│   │
│   ├── App.axaml                       Application resources
│   ├── App.axaml.cs                    Application initialization
│   ├── Program.cs                      Desktop application entry point
│   ├── MainWindow.axaml                Main interface
│   ├── MainWindow.axaml.cs             Main interface logic
│   ├── AudioSettingsWindow.axaml       Audio settings interface
│   ├── AudioSettingsWindow.axaml.cs    Audio settings logic
│   ├── EffectsLibraryWindow.axaml      Effects library interface
│   ├── EffectsLibraryWindow.axaml.cs   Effects library logic
│   ├── SplashWindow.axaml              Startup screen
│   ├── SplashWindow.axaml.cs           Startup screen logic
│   ├── PresetManager.cs                Preset saving and loading
│   ├── AudioBlocks.App.csproj          Project configuration
│   └── app.manifest                    Windows application manifest
│
├── publish/                            Published application files
├── release/                            Release packages
├── Visual Studio 18/                   Development-related files
├── AudioBlocks.sln                     Visual Studio solution
├── DOCUMENTATION.md                    Complete technical documentation
├── DOCUMENTATION-SIMPLIFIEE.md         Simplified audio documentation
├── GUIDE-PRESENTATION.md               Presentation guide
└── README.md
```

---

## Application Architecture

The main audio processing flow is:

```text
Audio input
    ↓
AudioEngine
    ↓
Float sample buffer
    ↓
AudioEffects
    ↓
Effect 1 → Effect 2 → Effect 3 → ...
    ↓
Recording and metronome mix
    ↓
Audio output
```

### Audio Engine

The `AudioEngine` class is responsible for:

- opening the audio input;
- opening the audio output;
- receiving samples;
- converting audio formats;
- processing buffers;
- calling the effects chain;
- sending samples to the output;
- managing recording;
- mixing the metronome;
- supporting WASAPI and ASIO.

The engine processes audio in small buffers to minimize latency.

### Effect Interface

All effects follow the common interface:

```csharp
public interface IAudioEffect
{
    string Name { get; }
    bool Enabled { get; set; }
    void Process(float[] buffer, int count);
    void SetSampleRate(int sampleRate);
}
```

This architecture makes it possible to:

- add new effects;
- reorder effects;
- enable or disable effects;
- process all effects consistently;
- keep the audio engine independent from individual algorithms.

### Real-Time Processing

The audio thread must process each buffer before the hardware requests the next one.

The project uses techniques such as:

- in-place buffer processing;
- `ArrayPool<float>`;
- unsafe blocks for ASIO data;
- locks around shared recording data;
- event-based communication between audio and UI layers;
- minimal allocation inside the audio callback.

---

## What Currently Works

Based on the current source code, documentation, release configuration, and project structure, the following features are implemented.

### Core Application

- Avalonia desktop application;
- Windows desktop startup;
- splash screen;
- main window;
- application branding;
- custom icon;
- production version metadata;
- release configuration;
- self-contained publishing configuration.

### Audio Engine

- live audio capture;
- audio output;
- WASAPI Shared support;
- WASAPI Exclusive support;
- ASIO support;
- input and output device management;
- audio buffer processing;
- sample format conversion;
- real-time effect processing;
- master volume handling;
- audio level monitoring.

### Effects

The following effects are present:

- Gain;
- Noise Gate;
- Compressor;
- Distortion;
- Fuzz;
- Three-band EQ;
- Graphic EQ;
- Delay;
- Reverb;
- Chorus.

Effects can be added to the chain and processed in order.

### User Interface

- main audio interface;
- effect chain display;
- effect library window;
- custom knobs;
- custom faders;
- level meters;
- graphic equalizer controls;
- audio settings window;
- device selection;
- driver selection;
- status information;
- splash screen;
- Material Design icons.

### Presets

- save effect configurations;
- load effect configurations;
- quick save;
- JSON serialization;
- preset export;
- preset import;
- effect parameter persistence;
- enabled state persistence;
- effect order persistence.

### Recording

- record processed audio;
- store samples safely between threads;
- stop recording;
- export audio as WAV;
- play back recordings;
- manage recording state.

### Metronome

- BPM configuration;
- generated click sound;
- beat timing;
- downbeat handling;
- audio-thread integration;
- fractional timing accumulator.

### Distribution

- Windows x64 runtime target;
- self-contained publish configuration;
- single-file executable configuration;
- native library inclusion;
- release folders;
- versioned releases;
- itch.io distribution.

---

## What May Not Work Reliably or Requires Attention

AudioBlocks is a released and usable application, but real-time audio software depends heavily on the computer, drivers, and hardware being used.

### 1. Audio Performance Depends on the Computer

Real-time audio processing is sensitive to:

- CPU load;
- buffer size;
- sample rate;
- driver quality;
- other running applications;
- audio interface performance;
- operating system scheduling.

On some systems, users may experience:

- latency;
- clicks;
- dropouts;
- crackling;
- buffer underruns;
- unstable monitoring.

### 2. ASIO Requires a Compatible Driver

ASIO mode does not work automatically on every computer.

The user needs:

- a compatible audio interface;
- a valid ASIO driver;
- correct channel configuration;
- supported input and output routing.

If the ASIO driver is missing or incorrectly configured, WASAPI is usually the easier alternative.

### 3. Windows Is the Main Supported Platform

The project uses:

```text
RuntimeIdentifier: win-x64
```

and relies on:

- Windows audio APIs;
- WASAPI;
- ASIO;
- Windows-specific audio drivers.

Although Avalonia itself supports multiple operating systems, the current application should be considered primarily Windows-oriented.

### 4. No Complete Automated Test Suite Is Visible

The repository contains extensive technical documentation and manual testing possibilities, but no large automated test suite is clearly visible.

This means that testing is primarily based on:

- manually running the application;
- checking audio input and output;
- enabling effects;
- testing presets;
- recording audio;
- trying different devices;
- using different drivers and buffer sizes.

### 5. Audio Quality Depends on Effect Parameters

Some effect combinations can produce:

- clipping;
- excessive volume;
- feedback;
- very loud output;
- unwanted noise;
- unstable levels.

Users should begin with moderate levels and monitor the output carefully.

### 6. Latency Cannot Be Identical on Every System

The application supports low-latency configurations, but the actual latency depends on:

- the selected driver;
- the audio interface;
- the sample rate;
- the buffer size;
- the computer;
- the operating system;
- the effect chain complexity.

ASIO usually provides lower latency than standard WASAPI, but it requires proper hardware support.

### 7. Preset Compatibility May Depend on Effect Names

Presets store effect types and parameter names.

If an effect is renamed or its public properties change, older preset files may require compatibility handling.

### 8. Some Features Require Real Hardware

The test signal can be used for development and testing, but the full product experience requires:

- a microphone;
- an instrument;
- an audio interface;
- headphones or speakers.

The quality of the experience depends on the connected equipment.

### 9. The Project Is Not a Full DAW

AudioBlocks is an effects processor and virtual pedalboard.

It is not intended to replace a complete digital audio workstation with features such as:

- multitrack editing;
- timeline editing;
- MIDI sequencing;
- full song arrangement;
- mixing automation;
- plugin hosting;
- advanced mastering;
- project-based session management.

---

## Requirements

The recommended environment is:

- Windows 10 or Windows 11;
- .NET 8 runtime or SDK for development;
- compatible audio input and output devices;
- ASIO drivers if ASIO mode is required;
- sufficient CPU performance for real-time effects.

---

## Running from Source

Clone the repository:

```bash
git clone https://github.com/bryte-dev/AUDIOBLOCKS.git
cd AUDIOBLOCKS
```

Run the application:

```bash
dotnet run --project AudioBlocks.App/AudioBlocks.App.csproj
```

Alternatively:

```bash
cd AudioBlocks.App
dotnet run
```

---

## Building the Project

Restore dependencies:

```bash
dotnet restore
```

Build the application:

```bash
dotnet build
```

Run the application:

```bash
dotnet run --project AudioBlocks.App/AudioBlocks.App.csproj
```

---

## Publishing a Windows Build

The project is configured for a Windows x64 self-contained release.

A publish command can be run with:

```bash
dotnet publish AudioBlocks.App/AudioBlocks.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true
```

The project is configured to support:

- a single-file executable;
- self-contained deployment;
- native library extraction;
- compressed single-file output;
- release metadata;
- application branding.

The exact output location may depend on the .NET SDK and publish configuration.

---

## First Use

A basic first-use workflow is:

1. Launch AudioBlocks.
2. Open the audio settings.
3. Select the input device.
4. Select the output device.
5. Choose WASAPI Shared or another supported driver.
6. Apply the audio settings.
7. Add an effect from the effects library.
8. Enable the effect.
9. Play or speak into the input device.
10. Adjust the effect parameters.
11. Monitor the output level.
12. Save the configuration as a preset if desired.

A safe starting effect chain could be:

```text
Noise Gate
    ↓
Gain
    ↓
Distortion
    ↓
EQ
    ↓
Reverb
```

---

## Example Preset Workflow

```text
Add effects
    ↓
Adjust knobs and faders
    ↓
Set the master volume
    ↓
Save the preset
    ↓
Export the preset as JSON
    ↓
Share or back it up
    ↓
Load it again later
```

---

## Documentation

The repository contains several documentation files.

### Technical Documentation

```text
DOCUMENTATION.md
```

This document explains:

- the audio engine;
- the effect interface;
- digital signal processing concepts;
- each effect algorithm;
- the preset system;
- the user interface;
- the metronome;
- the audio recorder;
- audio terminology.

### Simplified Documentation

```text
DOCUMENTATION-SIMPLIFIEE.md
```

This version explains the project with simpler language and everyday analogies.

It is intended for developers or users who are not familiar with audio programming.

### Presentation Guide

```text
GUIDE-PRESENTATION.md
```

This guide provides:

- a presentation plan;
- slide ideas;
- demonstration instructions;
- technical explanations;
- diagrams to recreate;
- questions that may be asked;
- suggestions for presenting the application.

---

## Why the Project Is Interesting

AudioBlocks is interesting because it is not only a graphical desktop application.

It also involves real-time constraints.

The software must process audio continuously and return the result quickly enough for the user to hear the output naturally.

This creates several technical challenges:

- audio callbacks;
- multithreading;
- synchronization;
- buffer management;
- memory allocation;
- sample-rate handling;
- driver compatibility;
- low-latency processing;
- effect ordering;
- signal clipping;
- thread-safe recording.

The project also demonstrates how complex audio tools can be built from smaller independent components.

---

## Learning Objectives

AudioBlocks helped explore and demonstrate:

### C# and .NET

- desktop application development;
- project configuration;
- unsafe code;
- records and interfaces;
- reflection;
- JSON serialization;
- file operations.

### Audio Programming

- audio capture;
- audio playback;
- samples and buffers;
- sample rates;
- latency;
- WASAPI;
- ASIO;
- WAV files.

### Digital Signal Processing

- gain;
- filtering;
- compression;
- soft clipping;
- hard clipping;
- delay lines;
- feedback;
- reverb;
- chorus modulation;
- LFOs;
- interpolation;
- anti-aliasing;
- DC blocking.

### User Interface Development

- Avalonia;
- AXAML;
- custom controls;
- rendering;
- knobs;
- faders;
- VU meters;
- effect panels;
- settings windows.

### Software Architecture

- interfaces;
- modular effects;
- audio pipeline separation;
- event-based communication;
- thread safety;
- preset management;
- reusable components.

---

## Release History

The repository contains evidence of multiple application releases:

```text
v1.0.0
v1.0.1
v1.0.2
```

The release work included fixes related to:

- application naming;
- displayed version information;
- execution and packaging behavior.

The latest documented release-related fix addressed execution problems caused by the application name.

---

## Project Assessment

AudioBlocks is one of the more complete projects in this collection.

It includes:

- a real application concept;
- a coherent architecture;
- substantial audio processing code;
- multiple effects;
- a functional user interface;
- presets;
- recording;
- a metronome;
- audio settings;
- production metadata;
- release folders;
- an itch.io distribution channel.

It is not simply a proof of concept. It is a usable audio application with a public distribution workflow.

However, it should still be considered a focused audio effects application rather than a full professional DAW.

---

## Summary

AudioBlocks is a C# and .NET desktop application that works as a virtual audio pedalboard.

Its main workflow is:

```text
Receive audio
    ↓
Process the signal
    ↓
Apply a configurable effects chain
    ↓
Monitor the output
    ↓
Record or export the result
```

The project includes:

```text
Real-time audio processing
    +
Multiple audio effects
    +
Presets
    +
Recording
    +
WAV export
    +
Metronome
    +
Custom audio controls
    +
WASAPI and ASIO support
    +
Windows publishing
    +
itch.io distribution
```

The core features are implemented and the project has been packaged for public release. The main limitations are related to hardware and driver compatibility, Windows-oriented audio support, real-time performance, and the absence of a full automated test suite.

AudioBlocks can therefore be described as a **released virtual pedalboard and real-time audio effects application**, built as a substantial C# desktop project and distributed through GitHub and itch.io.
