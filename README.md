# Hanging Woman

A first person horror walk: it's raining, it's late, and you're walking down a city street past
laundromats, noodle bars and a dying pawn-shop sign. About halfway down, across the street on
your left, a street lamp lights up two parallel trails of blood running from the road into an alley.
At the end of the alley a woman hangs from a rope that rises into the sky, her back to you.

## Opening the project

1. In Unity Hub choose **Add → Add project from disk** and pick this folder.
   Unity 2022.3 LTS or Unity 6 both work. If Hub asks about the editor version, pick any
   2022.3+ install you have.
2. When the project first opens, the scene builds itself and is saved to
   `Assets/Scenes/NightStreet.unity`.
   You can rebuild it at any time with **Hanging Woman → Build Night Street Scene**.
   If you pull an update while the project already has a scene, the Console and Scene view
   warn you that it's out of date. Rebuild it from that menu, because an old scene won't
   include the new changes. **Hanging Woman → Find Hanging Woman**
   selects her in the Scene view, and offers a rebuild if she's missing.
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
  hydrants, and a row of dead street lamps. The only one still working stands at the alley
  mouth.
- **The businesses:** a laundromat, a noodle bar, a pawn shop, a 24HR pharmacy, a liquor
  store, a hotel and more. Each has neon signs that flicker or buzz, lit or shuttered windows,
  awnings, and light spilling onto the pavement. The upper floors have scattered lit
  apartment windows and the odd flickering TV.
- **The rain:** world-space streaks that follow the player and splash wherever they land,
  including on awnings, cars and the dumpster. You also get a looping rain sound, occasional
  lightning and rolling thunder, and dense fog.
- **The blood trails:** two parallel drag marks, about heel-width apart, start at a pool in
  the west lane. They cross the curb and sidewalk under the one working street lamp,
  then run past the dumpster to a second pool by the back door, with a smear down the wall.
- **The alley:** on the left (west) side, about halfway down the street. It has a dumpster, trash bags, boxes, pipes and a buzzing caged wall lamp.
  `Alley Trigger` slows the player while they're inside and has `onPlayerEnter` /
  `onPlayerExit` UnityEvents to hook into.
- **The hanging woman:** she's a physics ragdoll in a dirty white dress, with long wet black
  hair. She stands limp in the pool where the two trails end, held up by the neck on a rope that
  rises until the fog swallows it, with her back to you. Get within 3 m and the camera takes over:
  1. It tilts up the rope until she's out of frame.
  2. The rope creaks and twists her around.
  3. The camera comes back down: she's facing you, and you back away a few steps.
  4. The top of the rope swings toward you, then drags her limp body after you, her feet
     scraping fresh blood trails as she goes. She knocks over the garbage can and the
     stacked boxes in her path, and stops just in front of you.

  Timings, distances, drag speed and the `onTriggered` / `onRevealed` / `onArrived` events
  are on `HangingWomanEvent`.

## Scripts

| Script | Purpose |
| --- | --- |
| `FirstPersonController` | CharacterController movement, mouse look, crouch, head bob, wet footsteps, flashlight. Supports both the legacy Input Manager and the Input System. |
| `RainSystem` | Rain and splash particles that follow the player, plus the rain loop. |
| `LightningStorm` | Random lightning flashes and delayed thunder. |
| `LightFlicker` | Gentle, neon-buzz or broken flicker for lights, emissive surfaces and sign text. |
| `AlleyTrigger` | Trigger volume that slows the player and fires events. |
| `HangingWomanEvent` | The scare sequence, driving the player's camera. |
| `HangingRagdoll` | Moves the rope's anchor to turn and drag the ragdoll, and draws the rope. |
| `FootBloodTrail` | Leaves blood smears wherever a foot drags along the ground. |
| `Billboard` | Turns lamp glows toward the camera. |
| `ProceduralAudio` | Generates the rain, thunder, footstep and rope-creak sounds, so no audio files are needed. Assign real clips in the inspector to replace them. |
| `QualityBootstrap` | Raises the per-pixel light count, since the built-in pipeline only allows a few by default. |
| `Editor/NightStreetSceneBuilder` | Builds and saves the whole scene. |

## Tips

- **Bloom makes the neon glow.** In URP, add a Global Volume with Bloom, Vignette and some
  Film Grain. In the built-in pipeline, use the Post Processing package.
- Layout numbers (alley position, shop list, trail path) are constants and data at the top of
  `NightStreetSceneBuilder` sections. Edit them and rebuild.
- Rebuilding replaces the scene. Put hand-made additions in a separate scene and load it
  additively, or stop using the builder once you start hand-editing.
