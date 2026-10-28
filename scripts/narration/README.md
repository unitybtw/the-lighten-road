# Offline archive narrator

Kokoro v1.0 FP16, British male `bm_george`, speed 0.96. Two CPU threads per worker. Title and story are read; UI labels and clues are not spoken. Synthesis runs in a separate process with a 90-second timeout. No network, Python installation, or API key is required on a player's machine once the native worker has been provisioned and shipped.

## Reproduce

Install uv, then run `python3 scripts/narration/setup.py` from the repository root. This downloads checksum-pinned model/voice files and builds a self-contained worker on the host platform. macOS ARM64 is verified; Windows x64 provisioning is supplied but unverified. Build and sign the appropriate native runtime per target platform before release. Linux, WebGL, mobile, and a build without the worker use the baked recordings.

To regenerate the four bundled WAVs, run `Game/Library/KokoroEnv/bin/python scripts/narration/bake_catalog.py` (Windows: `Scripts/python.exe`). This is a developer operation; the player never writes into StreamingAssets.

The model and native runtime are ignored by Git. Unity includes the locally provisioned StreamingAssets in a local build; CI and other developers must run setup for runtime synthesis. Content-addressed WAVs in `StreamingAssets/SpeechCache` are tracked so narration still works on an ordinary checkout without provisioning.

## Quality and cache

The profile is `kokoro-fp16-george-v2`. Long entries are split into complete sentence groups, not arbitrary character slices. Each group has a 4 ms boundary fade; 220 ms of silence separates groups. Peak limiting only reduces peaks exceeding 0.92. There is no pitch shifting, denoising, or lossy compression. These measures address clicks, clipped transitions, and rushed long utterances; a listening review remains necessary for pronunciation and voice preference.

The SHA-256 key includes the profile and whitespace-normalized title/story. Editing text or changing the profile creates a new cache entry. Generated WAVs are written atomically into `Application.persistentDataPath/Narration`. Missing or failed synthesis does not block reading. Closing the book or changing entries stops playback and prevents a late result from starting; generation may finish in the background for reuse. Within-entry page turns keep the narration continuous. Master/effects settings apply live.

## Provenance and redistribution

- Kokoro weights: https://huggingface.co/hexgrad/Kokoro-82M (Apache-2.0)
- ONNX export and runtime source: https://github.com/thewh1teagle/kokoro-onnx (MIT)
- eSpeak NG phonemizer: https://github.com/espeak-ng/espeak-ng (GPL-3.0); packaged via espeakng-loader.
- ONNX Runtime: https://github.com/microsoft/onnxruntime (MIT).

Preserve dependency license notices and satisfy applicable source-distribution obligations when shipping the native worker. Model checksums are recorded in `model_checksums.json`; exact package versions are pinned in `requirements.txt`. The selected voice is a catalog voice, not a custom clone.
