# Cat Defense (Unity, 3 levels)

Open `Assets/Game/Scenes/Main.unity` and press Play. Everything (screens, HUD, battle) is built from code by `Game`.

## Controls
- Aim: mouse / touch. Shoot: hold left click.
- Special: right click or Space (or the yellow button bottom right).
- Pause: Esc or the gear button.

## Tuning (no code changes needed)
- `Resources/Data/cats.json`: the 4 cats (damage, fire rate, special, muzzle offset).
- `Resources/Data/enemies.json`: walker / runner / brute / boss stats + boss phases.
- `Resources/Data/levels.json`: waves, spawn gap, mix, mini-boss, boss wave, boosters, barricade HP.

Distances in the JSON (splash radius, stop distance...) use the design doc's pixels on a 1280-wide screen.

## Art
`Resources/Game` holds only the kit art the game uses. Frame sequences are packed into sprite
sheets (`Game/Anim`, described by `Game/anims.json`); the raw kit stays untouched in `catdefense_assets`.

## Editor helpers
- Menu **Cat Defense > Create Main Scene** rebuilds the scene.
- `CatShooter.AutoFire = true` (editor only) makes the cat aim and shoot by itself for quick playtests.
