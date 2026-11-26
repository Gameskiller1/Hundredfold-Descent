# Enemies & Combat - quick guide

## How it fits together
- **Health** (Assets/Combat) - hit points for anything: player, enemies, bosses, breakables. Fires events (Damaged, Died).
- **Enemy** - the core on every enemy root. Facing, target (player), stun/knockback, death, dormant state.
- **Brain** (what to do) - `PatrolChaseBrain`: patrol -> notice -> chase -> attack -> give up. Inherit `EnemyBrain` for custom AI.
- **Motor** (how to move) - `WalkMotor` (ground), `FlyMotor` (air). Inherit `EnemyMotor` for new movement.
- **Attacks** (the moves) - `MeleeEnemyAttack`, `ProjectileEnemyAttack`, `LungeEnemyAttack`, `SummonEnemyAttack`. Inherit `EnemyAttack` for new ones.
- **EnemyAnimator** - same style as PlayerAnimator: no transitions, state names = clip names.
- **Boss** - an Enemy plus `BossPhases` (HP-based phases) and `BossEncounter` (arena trigger, doors, boss bar, reset).

## Layers
1. Add layer **Enemies**.
2. Player attack / spells: `Hit Layers` = Enemies.
3. Enemy attacks / ContactDamage / Projectile: `Hit Layers` / `Target Layers` = the Player's layer (it's on Default now - make a **Player** layer, it saves headaches).
4. Motors & brain `Ground Layer` / `Sight Blockers` = Ground.

## Regular enemy prefab
Root (layer Enemies): `Enemy`, `Health`, `Rigidbody2D` (Dynamic, Gravity ~3, Freeze Rotation), `Collider2D`, `ContactDamage` (optional), `HitFlash` (optional)
- child `Visual`: SpriteRenderer + Animator + `EnemyAnimator`
- child `Brain`: `PatrolChaseBrain`
- child `Motor`: `WalkMotor` (or `FlyMotor`)
- child `Attacks/Bite`: `MeleeEnemyAttack` (add as many attack children as you like)

Animator states (all optional, missing ones are skipped): `Idle`, `Walk`, `Hurt`, `Death` + one per attack (set `Animation State` on each attack).

## Boss
Same as a regular enemy, plus on the root: `BossPhases` (Can Be Stunned OFF, Knockback Resistance 1 on `Enemy`).
Arena: empty object with a trigger Collider2D + `BossEncounter` (assign boss, Boss Id, doors via the UnityEvents, `HealthBarUI`).
Weak points: child collider + `Hurtbox` (damage multiplier 2 = head, 0.25 = armor).

## Player
- `Health` + `PlayerHealth` on the Player (Invulnerable After Hit ~0.8; add `HitFlash` with flicker).
- `MeleeAttackAbility` on AbilitiesChild, assign the `Attack` input action and Hit Layers.
- `HealthBarUI` on a HUD Image with "Find Player" ticked.
