---
license: apache-2.0
library_name: unity-sentis
pipeline_tag: automatic-speech-recognition
tags:
  - unity-inference-engine
---

# Whisper-Tiny model in Unity 6 with Inference Engine

This is the [Whisper Tiny](https://huggingface.co/openai/whisper-tiny) model running in Unity 6 with Inference Engine. It is a speech-to-text model that transcribes 16kHz wav audio to text.

## How to Use

* Create a new scene in Unity 6;
* Install `com.unity.ai.inference` from the package manager;
* Add the `RunWhisper.cs` script to the Main Camera;
* Drag the `decoder_model.onnx` asset from the `models` folder into the `Audio Decoder 1` field;
* Drag the `decoder_with_past_model.onnx` asset from the `models` folder into the `Audio Decoder 2` field;
* Drag the `encoder_model.onnx` asset from the `models` folder into the `Audio Encoder` field;
* Drag the `logmel_spectrogram.onnx` asset from the `models` folder into the `Log Mel Spectro` field;
* Drag the `vocab.json` asset from the `data` folder into the `Vocab Asset` field;
* Drag an audio asset, e.g. `data/answering-machine16kHz.wav` to the `Audio Clip` field. Ensure the `Normalize` flag is set on asset import for best results.

## Preview
Enter play mode. If working correctly the transcribed audio will be logged to the console.

## Inference Engine
Inference Engine is a neural network inference library for Unity. Find out more [here](https://docs.unity3d.com/Packages/com.unity.ai.inference@latest).