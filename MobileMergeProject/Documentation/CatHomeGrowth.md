# Cat Home Growth

## Rules

- GameScene and Stage1Scene through Stage10Scene initialize the same cat home controller.
- A complete row or column containing one cat type gives that cat one growth point.
- Mixed lines and lines containing stage cheese do not give growth points.
- An intersecting uniform row and column give two points. Placement and swipe clears both count.
- Each cat starts as a kitten, becomes a juvenile after 1 matching clear and an adult after 3 matching clears.
- Size increases between milestones and stops at adult size. Matching clears trigger a small hop; idle cats breathe and sway.
- Growth is stored locally in `score-data.json`, independently for each type, while the current game is active. It is not cloud-synced.
- Starting or restarting a gameplay scene resets every cat's growth to kitten. This does not reset the saved best score or gold. `ResetSaveData` still resets all saved data.
- The tutorial explains cat growth after its movement missions. It is a touch-through message and does not block the player with an extra mission.

## Assets And Tuning

`Assets/Resources/BlockBlast/CatHome/CatGrowthCatalog.asset` controls thresholds, matching block sprites, age sprites, home positions and maximum sizes.

`Assets/Resources/BlockBlast/CatHome/CatResidents.prefab` contains the four resident UI objects. It is instantiated under the active room background, below gameplay overlays. Its graphics do not intercept touches.

The screenshot order is mapped to the block library order:

1. Black: right tower cubby.
2. White: lower-right blue bed.
3. Sphynx: lower-left pink bed.
4. Orange: left cubby.

Home positions are normalized bottom-center coordinates in the existing background image. Adult sizes are maximum width/height fractions. The background artwork is unchanged.

## Verification

Unity menu: `Tools > Catch the Cats > Validate Cat Growth`.

The validator uses an additive temporary scene and a separate file under `Library/CatHomeValidation`. It does not call the score server or modify the player's save. It covers 100 seeded board arrangements, uniform intersections, cheese, save migration/reload, type isolation, event subscriptions, idle poses and all 11 gameplay scenes. Rendered previews and the report are written into the same ignored folder.

`Build Cat Home Assets` imports/slices the atlas and creates missing assets. Existing growth thresholds and positions are preserved.

## Art Provenance

Generated with the built-in image generation tool, using the four existing block sprites as style references. The PNG alpha channel is preserved; the Unity sprite importer slices the twelve illustrations without repainting them.

Generation prompt: Create a transparent 4-column by 3-row atlas of complete sitting cats matching the existing black, white, peach sphynx and orange tabby faces. Columns are the four types; rows are kitten, juvenile and adult. Use soft brown outlines, flat pastel fills, solid round eyes, a small w-shaped mouth, full paws and curled tails. Preserve consistent alignment and transparent spacing. No text, furniture, glossy eyes or grid lines.

Final edit prompt: Change only the backdrop. Remove the checkerboard pattern and make the space outside all twelve silhouettes truly transparent with an alpha channel. Preserve each cat, its color, shape, size and 4-by-3 arrangement. No added shadows, labels or framing.
