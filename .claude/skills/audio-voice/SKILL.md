---
description: Inspect and extract Skyrim voice/audio files via mo2_audio_info (FUZ/XWM/WAV header metadata) and mo2_extract_fuz (split a FUZ into its XWM audio component and LIP sync component). Use when the user asks about audio file formats, voice file integrity, extracting lip-sync data from a voice file, accessing the raw audio inside a FUZ, or working with NPC voice assets.
---

# Audio / Voice

**`mo2_audio_info`** — Format metadata for `.fuz` / `.xwm` / `.wav`.
- Params: `path`
- FUZ parsing is handled by our local bridge parser. Spooky's upstream parser has a known bug rejecting valid FUZ files (magic bytes `FUZE\x01\x00\x00\x00`) — our bridge sidesteps it. XWM and WAV still route through Spooky's CLI.

**`mo2_extract_fuz`** — Split a `.fuz` file into its `.xwm` audio component and `.lip` sync component.
- Params: `path`
- Writes to `{output_mod}/FuzExtract/{basename}/` by default
- The resulting `.xwm` can be decoded separately (xWMAEncode or similar) if raw audio is needed

## Notes

- Audio paths are VFS-resolved — pass game-relative paths (e.g., `sound/voice/skyrim.esm/malenord/voicefile_00123.fuz`).
- If Spooky ever fixes their upstream FUZ parser, we'll drop the local implementation; for now our parser is authoritative for FUZ.
