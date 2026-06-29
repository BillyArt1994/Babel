# AGENTS.md

## Project Background

Babel is a 2D roguelite inspired by Vampire Survivors, with tower-defense
pressure layered into the run. The player is an angry god trying to stop
overreaching humans from completing the Tower of Babel before the timer expires.

Core gameplay loop:

- Enemies: human units continuously enter from both sides of the screen. Unit
  types have different jobs and abilities, such as carrying materials, healing,
  speeding allies up, occupying build points, or contesting access to upper
  layers. Their shared goal is to build the tower layer by layer.
- Player: the primary attack is click-based damage, supported by passive divine
  powers such as automatic lightning or fire and active ultimate abilities such
  as a flood that clears the screen. Kills grant faith, faith grants level-ups,
  and each level-up offers one of three random skill choices to shape the run's
  build.
- Win and loss: the tower is an inverted pyramid, so higher layers are narrower
  and harder to defend. The player wins if time expires before the tower is
  completed. The player loses if humans finish the tower within the time limit.

## Project Basics

- Engine: Unity 2022.3.62f3.
- Language: C#.
- Target platform: PC first, with possible mobile ports later.

## Key Paths

- `Babel_Client/`: Unity project root.
- `docs/gdd/`: game design documentation.
