# Hanging Woman

A first person horror walk: it's raining, it's late, and you're walking down a city street past
laundromats, noodle bars and a dying pawn-shop sign. Somewhere outside the 24-hour pharmacy a trail
of blood starts on the sidewalk and leads you into an alley.

## Opening the project

1. In Unity Hub choose **Add → Add project from disk** and pick this folder.
   Unity 2022.3 LTS or Unity 6 both work. If Hub asks about the editor version, pick any
   2022.3+ install you have.
2. When the project first opens, the scene builds itself and is saved to
   `Assets/HangingWoman/Scenes/NightStreet.unity`.
   You can rebuild it at any time with **Hanging Woman → Build Night Street Scene**.
3. Press **Play**.

Everything is made from primitives, generated textures and generated audio, so there are no
assets to import. It works with the **Built-in** render pipeline and **URP**: the builder
detects which one is active and creates the matching materials. If you switch pipelines later,
just run the build menu again.

## Controls

| Input | Action |
| --- | --- |
| WASD / arrows | Walk |
| Mouse | Look |
| Left Shift | Hurry |
| Left Ctrl / C | Crouch |
| Space | Jump |
| F | Flashlight |
| Esc / click | Release / recapture the mouse |

Gamepad works too if the project uses the new Input System.

## What's in the scene

- **The street:** a wet asphalt road with sidewalks, curbs, lane paint, puddles, parked cars,
  hydrants, and sodium street lamps. The lamp just before the alley is dying.
- **The businesses:** a laundromat, a noodle bar, a pawn shop, a 24HR pharmacy, a liquor
  store, a hotel and more. Each has neon signs that flicker or buzz, lit or shuttered windows,
  awnings, and light spilling onto the pavement. The upper floors have scattered lit
  apartment windows and the odd flickering TV.
- **The rain:** world-space streaks that follow the player and splash wherever they land,
  including on awnings, cars and the dumpster. You also get a looping rain sound, occasional
  lightning and rolling thunder, and dense fog.
- **The blood trail:** sparse drips start on the sidewalk by the pharmacy and get heavier as
  they reach the alley. Inside, they turn into drag smears past the dumpster, ending in a
  pool by the back door, with a smear down the wall.
- **The alley:** a dumpster, trash bags, boxes, pipes and a buzzing caged wall lamp.
  `Alley Trigger` slows the player while they're inside and has `onPlayerEnter` /
  `onPlayerExit` UnityEvents to hook into. `Trail End (Event Anchor)` marks where the trail
  ends, for whatever happens next.

## Scripts

| Script | Purpose |
| --- | --- |
| `FirstPersonController` | CharacterController movement, mouse look, crouch, head bob, wet footsteps, flashlight. Supports both the legacy Input Manager and the Input System. |
| `RainSystem` | Rain and splash particles that follow the player, plus the rain loop. |
| `LightningStorm` | Random lightning flashes and delayed thunder. |
| `LightFlicker` | Gentle, neon-buzz or broken flicker for lights, emissive surfaces and sign text. |
| `AlleyTrigger` | Trigger volume that slows the player and fires events. |
| `ProceduralAudio` | Generates the rain, thunder and footstep sounds, so no audio files are needed. Assign real clips in the inspector to replace them. |
| `QualityBootstrap` | Raises the per-pixel light count, since the built-in pipeline only allows a few by default. |
| `Editor/NightStreetSceneBuilder` | Builds and saves the whole scene. |

## Tips

- **Bloom makes the neon glow.** In URP, add a Global Volume with Bloom, Vignette and some
  Film Grain. In the built-in pipeline, use the Post Processing package.
- Layout numbers (alley position, shop list, trail path) are constants and data at the top of
  `NightStreetSceneBuilder` sections. Edit them and rebuild.
- Rebuilding replaces the scene. Put hand-made additions in a separate scene and load it
  additively, or stop using the builder once you start hand-editing.
