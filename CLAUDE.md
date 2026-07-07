# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository purpose

This is a personal Unity sandbox for practicing specific game-programming concepts. There is no single overarching game — instead, each git branch is an isolated experiment in one concept (architecture patterns, dependency injection, localization, multiplayer, procedural generation). Branch names are prefixed by category:

- `code/*` — architecture/coding-pattern practice (e.g. `code/architecture-practice`, `code/dependency-injection`)
- `feature/*` — feature-system practice (e.g. `feature/localize-system`, `feature/multiplayer-local-host`, `feature/procedural-generation`)

When working on a branch, assume the concept named in the branch is the entire scope of the task — don't pull in patterns from other branches/experiments unless asked.

## Project layout

The actual Unity project lives in `Personal Projects/` (note the space in the folder name — always quote it in shell commands). Everything under `Personal Projects/Library/`, `Temp/`, `Logs/`, `obj/`, and `UserSettings/` is Unity/IDE-generated and gitignored; ignore it when exploring the codebase.

- `Personal Projects/Assets/Scripts/` — gameplay C# scripts (only present on branches that have added code; `main` starts from the bare Unity URP 3D template with no game code)
- `Personal Projects/Assets/Tests/` — NUnit edit-mode tests, isolated into their own assembly definition
- `Personal Projects/Assets/Document/Readme.md` — when present on a branch, holds the write-up of the design principles that branch is practicing
- `Personal Projects/Assets/Settings/` — URP render pipeline/quality assets
- `Personal Projects/Packages/manifest.json` — package dependencies (Input System, URP, Timeline, Visual Scripting, AI Navigation, Multiplayer Center, etc.)
- `Personal Projects/ProjectSettings/ProjectVersion.txt` — pins the Unity Editor version (Unity 6000.2.8f1 / Unity 6)

Scripts use assembly definitions (`.asmdef`) to separate runtime code (`scripts.asmdef`) from edit-mode tests (`Tests.asmdef`, constrained to `UNITY_INCLUDE_TESTS`), so tests reference the runtime assembly rather than living in the same compilation unit.

## Architecture patterns (as seen on `code/architecture-practice`)

This branch demonstrates a MonoBehaviour-light architecture, documented in `Personal Projects/Assets/Document/Readme.md`:

1. **Program to interfaces, not concrete MonoBehaviours** (`IWeapon`, `IDamageable` in `Interfaces.cs`) — components depend on behavior contracts so implementations can be swapped and tested without a scene.
2. **Separate logic from MonoBehaviour** — e.g. `Weapon` (MonoBehaviour) delegates to a plain C# `WeaponLogic` class that holds no Unity dependencies, so the logic is unit-testable outside play mode (see `WeaponTests.cs`).
3. **Separate data via ScriptableObjects** — `WeaponConfig` is a `ScriptableObject` so designers can tweak balance values without touching code.
4. **Event-driven flow** — e.g. `PlayerInput` exposes an `AttackPressed` event; `Player` subscribes rather than polling, keeping input, decision-making, and action loosely coupled.
5. **Static generic registry instead of direct references** — `Registry<T>` (in `Registry.cs`) is a static, type-keyed set that lets systems register/query "what exists now" (e.g. `Registry<IDamageable>`) instead of using `FindObjectOfType` or wiring explicit references.

When extending this branch, follow the same shape: define the contract as an interface first, keep gameplay decision logic in a plain C# class separate from the MonoBehaviour wrapper, put tunable values in a ScriptableObject, and use events/the registry instead of hard references or `Find*` calls.

## Working with the Unity project

There is no CLI build/test/lint pipeline in this repo — Unity projects are built and tested through the Unity Editor:

- **Open the project**: open `Personal Projects/` as the project root in Unity Hub/Editor (version 6000.2.8f1).
- **Run tests**: use the Unity Test Runner window (`Window > General > Test Runner`) inside the Editor; NUnit tests live under `Personal Projects/Assets/Tests/`.
- **Compilation errors**: check the Editor Console, or open `Personal Projects/Personal Projects.sln` in an IDE (Rider/VS) for C# diagnostics — the `.sln`/`.csproj` files are Unity-generated, don't hand-edit them.

Do not edit files under `Library/`, `Temp/`, `Logs/`, `obj/`, or `UserSettings/` — Unity regenerates these and hand edits will be lost or ignored. `.meta` files must be kept alongside their corresponding asset (renaming/moving an asset should carry its `.meta` file with it) since Unity uses them to track stable GUIDs.
