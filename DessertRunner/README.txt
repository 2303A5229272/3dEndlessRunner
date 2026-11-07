DESSERT RUNNER - ENDLESS RUNNER GAME  (Unity project files)
==========================================================
Whole game is generated from code at runtime (primitives, procedural textures and sounds),
so there are no external assets to import. Uses only built-in Unity features.

HOW TO USE
1. Unity Hub -> New project -> "3D" template (Built-in or URP, any recent Unity version).
2. Close Unity, copy the folder  Assets/DessertRunner  from this zip into your project's Assets folder.
   (Or copy the whole Assets folder contents.)
3. Open the project. After scripts compile, the editor opens/creates Assets/Scenes/DessertRunner.unity
   and enters Play mode automatically. The game also starts when you press Play in ANY scene.
   Menu: Dessert Runner > Play Game  (to start it manually).

CONTROLS
 A / Left Arrow      Move left          D / Right Arrow   Move right
 W / Up / Space      Jump               S / Down Arrow    Slide
 Escape              Pause / resume     Space / Enter     Start / retry

FEATURES
 3 lanes, auto-run, jump + slide, recycled track tiles and pooled obstacles/pickups, random obstacle rows,
 speed that increases over time, 3 difficulty levels, 4 desert stages (Golden Dunes, Oasis Palms,
 Rocky Canyon, Sunset Trail), coins, gems, power-ups (Shield, Magnet, Speed Boost, Score x2),
 HUD, pause, game over + retry, save/load with PlayerPrefs (COINSAVE, GEMSAVE, DISTANCESAVE).

FILES (Assets/DessertRunner)
 Scripts/GameManager.cs  main controller (track, player, obstacles, pickups, score, UI)
 Scripts/Builders.cs     materials + 3D models built from primitives
 Scripts/Inp.cs          input (old Input Manager and new Input System both supported)
 Scripts/Bootstrap.cs    auto-creates the game in any scene
 Editor/AutoRun.cs       auto-opens scene and enters Play mode when the project opens

NOTE: written without access to the Unity Editor, so it has not been compiled/play-tested here.
If the Console shows a compile error, send it back and it can be fixed quickly.
