# AUDIOBLOCKS

AUDIOBLOCKS is a C# desktop audio project built around a **live audio FX engine**.

## Overview

Based on the project structure and application metadata, AUDIOBLOCKS is a desktop application for real-time audio processing. The repository includes a dedicated audio engine, effects library, preset management, and audio settings workflows.

## Goal / Objective

The code suggests a project aimed at giving musicians or audio users a desktop environment for experimenting with live effects and signal routing.

> TODO: Add the original project context, target users, and whether the app was built for performance, experimentation, or production use.

## Visuals

- **Main interface screenshot:** TODO
- **Effects chain / preset workflow GIF:** TODO
- **Demo video:** TODO

## Stack

- C#
- .NET 8
- Avalonia UI
- NAudio

## Key Features

- Live audio processing engine
- Built-in effects library
- Preset management
- Audio settings window
- Device enumeration and routing controls
- Support for multiple driver modes visible in code, including WASAPI and ASIO

## Effects Visible in the Repository

- Chorus
- Compressor
- Delay
- Distortion
- EQ / Graphic EQ
- Fuzz
- Gain
- Noise Gate
- Reverb

## Architecture Summary

- `AudioBlocks.App/Audio/`: core audio engine logic
- `AudioBlocks.App/Effects/`: individual effect implementations
- `AudioBlocks.App/PresetManager.cs`: preset persistence/management
- `AudioBlocks.App/AudioSettingsWindow.axaml.cs`: device, routing, and driver configuration
- `AudioBlocks.App/EffectsLibraryWindow.axaml(.cs)`: effect browsing / management UI

## Run Locally

```bash
dotnet run --project AudioBlocks.App/AudioBlocks.App.csproj
```

> TODO: Add OS requirements, supported audio hardware details, and any ASIO-specific setup steps.

## Notes to Fill In Later

- Screenshots and demo footage
- Typical use case (guitar, vocals, live routing, experimentation, etc.)
- Latency/performance expectations
- Packaging / installer instructions

---
