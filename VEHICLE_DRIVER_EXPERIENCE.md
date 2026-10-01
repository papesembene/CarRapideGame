# Driver experience

Open `Assets/Scenes/SampleScene.unity` with Unity **6000.6.1f1** and enter Play Mode. The characters, textures, animations and recorded engine audio are bundled; no download or additional Unity package is required.

## Controls

- **E**: approach, open the driver door, climb into the cabin, sit and close the door.
- **R**: reach the ignition after seating and door closure, then play the starter recording. Driving stays locked until the sound finishes and the hand returns to the wheel.
- **W / S** or **Up / Down**: accelerate, brake and reverse.
- **A / D** or **Left / Right**: steer. **Space**: handbrake.
- **P**, at a standstill near the passenger: board through the rear door and stay seated during the journey.
- **O**, at a standstill: alight at the current stop. The engine can idle during either action; driving is locked until it finishes. Repeated/conflicting interaction requests are ignored.
- Gamepad: left stick drives, south button applies the handbrake after ignition.

## Geometry calibration

The source FBX is preserved; Read/Write is enabled for contact queries. The two front doors now use derived meshes with their glass triangles removed, leaving open side windows and the metal doors intact. Its scene instance is scaled uniformly by **2.2**, producing a roughly 5.4 m long vehicle. The old collider extended below the tyres and made the mesh float. Its bottom now coincides with the tyre contact plane; the vehicle starts settled on the ground.

The windscreen still uses transparent URP glass. The separate `Fenetre_arriere` frame follows the rear door. A frictionless collider material leaves rolling resistance and lateral grip to the existing arcade controller; acceleration is applied after its velocity constraints, and braking is limited to the current speed to prevent oscillation around zero.

The measurements use transformed mesh vertices, not padded skinned bounds or the roof rack:

| Reference | Measurement at authored vehicle scale |
|---|---|
| Driver hinge | Original `Porte_avant_gauche` pivot, around x=-1.058, z=2.112 |
| Door opening | 76 degrees about vehicle-up, accounting for the FBX local rotation |
| Left mirror | `Retroviseur gauche`, reparented with world transforms preserved |
| Misleading FBX name | `Porte_avant_gauche.001` is the opposite-side mirror; it stays untouched |
| Driver seat | Left section of `Chaises avant`, cushion near y=0.842 at x=-0.61, z=1.39; pelvis anchor (-0.61, 1.03, 1.43) |
| Wheel grips | Original `Volant` assembly offset 10 cm forward and 5 cm up to clear the knees; grips recalibrated on its ring |
| Driver step | Floor at x=-1.015, z=1.95, y=0.336; forward of the wheel housing |
| Rear platform | `Carosserie` vertices behind z=-2.45, top near y=0.32 |
| Receiver grip | Actual rear ladder rail (`Escalier`), near x=-0.63, z=-2.20 |

All interaction anchors are serialized under `CarRapidePlayer/InteractionPoints`. Select the `VehicleInteractionPoints` component to display its gizmos. Driver height is 1.72 m; receiver/passenger height is 1.70 m, measured from baked rest-pose mesh vertices rather than inflated animation bounds.

## Animation and ownership

- `VehicleDriverExperience` composes the experience and handles prompts/input.
- `DriverController` sequences individual support, swing, reach, duck, pivot, seating and ignition phases.
- `DriverAnimationController` blends Animator clips and contact tracks.
- `CharacterContactRig` applies a final two-bone solve for feet and hands with explicit elbow/knee poles. Planted feet keep fixed contact coordinates while the other foot follows a lifted swing arc. The pelvis trajectory shifts over supporting feet. The character root is not lerped to the seat.
- `VehicleDoorController` animates the real door and attached left mirror/handle. Closing sweeps the original door mesh in steps of at most one degree and stops on an obstructing body. Ignition remains locked until closure.
- `VehicleCharacterClearance` measures limb, head and torso envelopes against original bodywork, seats, dashboard, wheel and door triangles. Its queries follow moving mesh transforms without applying physics forces to the vehicle.
- `VehicleEngineController` gates ignition and `CanDrive`; `VehicleAudioController` blends recorded truck idle, load and mechanical rattle layers from the front engine position. Speed and propulsion demand control pitch/volume, with gentle irregularity for a worn motor. Braking does not count as throttle.
- `VehicleCameraController` chooses views; `VehicleCameraFollow` supplies continuous damping.
- `ReceiverController` filters vehicle acceleration into small body compensation movements while keeping feet and the hand attached to the vehicle.
- `PassengerController` owns separate boarding, seated and alighting states. Its waiting area remains in world space; the seated frame follows the car and detaches after alighting. The rear door remains open while the passenger is aboard.
- `PedestrianPhysics` supplies solid body colliders outdoors. A vehicle contact above 0.65 m/s releases a jointed ragdoll and disables animation so the person falls under physics. Slow contacts block the car without triggering a fall. Collision shapes are disabled during the calibrated entry/exit sequence and while seated. A fallen passenger stays on the ground until the Play session is reset.
- `WorkwearAppearance` applies independent reference-inspired outfits to the driver and receiver. Textures, provenance and prompts are in `WORKWEAR_REFERENCES.md`.

The motion is authored procedurally, not motion capture. Free wrists follow their forearms, elbow poles are relative to the shoulders, and the driver's supporting hand stays in front during entry. The passenger reuses one of the licensed generic avatars. The source avatars are not specifically Senegalese; attribution and exact asset provenance are in `CREDITS.md`.

## Authoring and verification tools

`Car Rapide > Vehicle > Install calibrated driver experience` reinstalls the serialized layout and materials in Edit Mode. Existing baked Humanoid performances are preserved. This operation saves the open scene and supports Undo.

`Car Rapide > Vehicle > Record 30 second Play Mode review` runs a repeatable smoke test from a fresh Play Mode session: hold controls before seating, board, start, accelerate/turn, brake and reverse. Screenshots and check results are written to `Library/VehicleReview/Run` (ignored by Git). This is a development test, not a production autoplay mode.

The review also captures the complete solved Humanoid muscle performance. After stopping Play Mode, `Bake captured Humanoid performance` writes editable `.anim` assets. Contact tracks remain authoritative for placement in this particular cabin. The final contact pass restores body bone rotations before solving the authored pose, avoiding accumulation of older baked chest/shoulder rotations; fingers retain clip animation. Clips remain inspectable in the Animator/Animation windows. The revised seat pivot uses one continuous trajectory with knee poles around the steering column, rather than stopping between intermediate poses. Walking shares pelvis velocity between steps, while feet lift before crossing a sill and land vertically.

`Record passenger Play Mode review` records the rear entry, seating and exit separately in `Library/VehicleReview/Passenger`. Both reviews report contact reach errors against a 5 cm maximum threshold and body-envelope overlaps against a 1.5 cm tolerance. These are conservative envelope checks, not exact skinned-mesh collision detection. The driving review first tests door obstruction and recovery, then checks steering, handbrake stopping, reverse motion, held controls throughout ignition, and actual audio samples at the listener. Run them individually; use a fresh Play Mode session for the driving review.

`Record driver review with audio MP4` additionally exports `Library/VehicleReview/Run/driver-review.mp4` at 1280×720, H.264 with a 12 Mbps target bitrate. It captures Unity's actual listener mix into the MP4 and a companion `driver-review-audio.wav`. The simulation uses a fixed 30 fps during capture and restores its previous rate afterwards, including on interruption. Unity routes sound to the recording while this export runs; normal playback resumes afterwards. Encoding can take longer than the 30 seconds shown in the video.

`VehicleReviewBridge` is an Editor-only local file command harness for repeatable visual inspection. It only accepts a fixed set of commands and writes to the ignored `Library/VehicleReview` directory.

## Initial clearance validation — 2026-09-28 (before reference outfit update)

Verified in the actual Unity 6000.6.1f1 editor on Windows. The revised driver sequence was recorded at 30 fps, and the passenger sequence was run twice consecutively in the same Play Mode session to check restart behaviour.

- Driver review: all 16 checks passed, including door obstruction/recovery, held controls before seating and during ignition, running audio, acceleration above 30 km/h, steering, handbrake, reverse and no digital audio clipping.
- Driver contacts: maximum reach error 0.047 m; body-envelope overlap against the original meshes peaked at 0.0098 m, below the 0.015 m tolerance. Seated hands and feet report 0.000 m reach error at the logged precision.
- Passenger review: all 4 checks passed on both runs. Maximum reach error was 0.032 m; maximum body-envelope overlap was 0.0109 m on the first run and 0.0110 m on the repeated run. Seating was reached before alighting, and the ignition interlock was released afterwards.
- The final MP4 contains 900 frames at 30 fps (30 seconds), 1280×720, with video and audio tracks. Its companion WAV contains 29.995 seconds of stereo 48 kHz audio, a peak of 0.5739 including the starter, and no clipped samples. Driving audio peaked at 0.2838.
- The local video, audio, passenger images and three check logs are in `Recordings/VehicleInteractions-2026-09-28/`, ignored by Git. The dedicated interaction task and manual acceptance steps are in `VEHICLE_INTERACTIONS_TASK.md`.

Validation covers the supplied avatars and this scene in Play Mode; no standalone player build was produced. Contact envelopes are approximate, and different avatars or cabin geometry require recalibration. The motion remains procedural, with generic avatar styling and engine recordings as detailed in `CREDITS.md`.

## Passenger journey and impact review

`Car Rapide > Vehicle > Review passenger journey and physical impact` runs from a fresh Play Mode session. It checks both open front windows against the source metal triangle counts, passenger boarding with conflicting inputs, persistent seating, travel and turning, refused alighting while moving, two exit/entry cycles at the new stop, slow solid contact and a real vehicle collision that releases the ragdoll. Screenshots and assertions are saved under `Library/VehicleReview/Realism`.

This review positions the car for an isolated impact at the end and freezes it afterwards so the fall can be inspected. Exit Play Mode to reset that test state. Tests use the same public gameplay commands and PhysX collisions; they do not trigger the fall method directly. The steering check records the maximum heading change during travel so a completed full turn does not falsely report that steering never worked.

To regenerate the reference outfits and door mesh derivatives in Edit Mode, use `Apply reference workwear and open cab windows`. It is also called by the calibrated experience installer. The project includes the generated materials, textures, meshes and scene overrides, so normal Play Mode does not require the installer.

## Reference outfit and interaction validation — 2026-09-28

- Final driver video review: **16/16 checks passed**. Maximum contact reach error **0.047 m**, maximum body-envelope overlap **0.0138 m** against the 0.015 m threshold. This run includes the restored metal driver door with only its glass removed, the relaxed left arm on the step and the beige/denim outfit.
- The final driver MP4 has **901 frames at 30 fps**, 1280×720, with the actual Unity audio mix. Companion WAV: **30.016 seconds**, stereo 48 kHz, peak **0.5739** and no digitally clipped samples.
- Local recording and check log: `Recordings/VehicleRealism-2026-09-28/driver-review.mp4` and `driver-checks.txt` (ignored by Git). These supersede the initial recording for the updated driver/outfit sequence.
- Initial journey/physics integration on 28 September: **17/17 checks passed**. The final rerun with the updated passenger limb placement is recorded below.
- The passenger clearance run on 28 September found a **0.0243 m right-toe overlap** with the rear platform during descent. Moving the intermediate foot target **8 cm farther behind the edge** resolved this failure in the final review below.

## Final passenger and integration validation — 2026-09-30

- Unity 6000.6.1f1 reopened successfully and compiled the current runtime and Editor scripts. The licence interruption from 29 September no longer blocked Play Mode; no activation or account settings were changed by the agent.
- Passenger clearance: **4/4 checks passed**, including seating, completion of alighting and release of the driving interlock. Maximum reach error **0.029 m**; maximum body-envelope overlap **0.0080 m**, below the **0.015 m** threshold. The corrected right-foot descent was included in this run.
- Journey and physical impact: **21/21 checks passed**. Both front side glass surfaces are empty while metal triangle counts match the source model. The passenger stays seated until requested, follows the moving and turning car (**0.000 m** seat error at logged precision), refuses exit in motion, and completes two alighting cycles with another boarding after the car changes heading.
- The actual contact test confirms solid low-speed contact, a physical fall after a **14.4 km/h** impact, ground collision, suspension of animation during the fall and refusal to board while fallen. The final screenshot was visually inspected and shows the passenger on the ground behind the vehicle.
- Local evidence: `Recordings/VehicleRealism-2026-09-30/passenger-checks.txt`, `journey-checks.txt`, `passenger-alighting.png` and `passenger-impact.png` (ignored by Git). These complete the previously pending passenger and integration checks.

To repeat the checks, run **Record passenger Play Mode review**, then **Review passenger journey and physical impact** in a fresh Play Mode session. The glass comparison reads the original FBX directly because the animated door pivots reparent the scene meshes at runtime.

Inspection is in Unity Play Mode, with a procedural motion system and the supplied avatars. This does not certify every terrain slope, arbitrary obstacle or replacement avatar. The workwear is reference-inspired rather than an exact photo reproduction; the generic source body meshes remain in use.
