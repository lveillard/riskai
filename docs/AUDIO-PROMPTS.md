# Riesgus · audio clips and ElevenLabs prompts

`RiskAI/Assets/RiskAI/Resources/Audio/clips.json` is the single source for every clip: its files, prompts, length, space (2D/3D), volume, budget and priority. `Sfx.cs` reads it at runtime; this page only explains how to add or replace sounds.

- Each clip `id` is the snake_case name of an `SfxId` (`HitSword` → `hit_sword`). A clip without an `SfxId`, an `SfxId` without a clip, or a listed file missing from `Resources/Audio/` is an error (EditMode `FeedbackLogicTests` checks all three).
- A clip with several files picks one at random per play. Unit death sounds come from `presentation.deathSound` in `units.json` (`death` for land units, `sink` for ships).
- Generation (ElevenLabs Sound Effects): use the listed duration, prompt influence around 0.5, and the shared `style` line appended to every prompt.
- 3D clips are positioned in the world (panned and attenuated around the camera focus); 2D clips are interface feedback.
- Mix: normalise to about -14 LUFS short-term, trim leading silence (the game adds its own pitch/volume jitter). Import `.wav` files with `normalize: 0`.
- Review candidates side by side with `python scripts/audio_review_page.py`.
