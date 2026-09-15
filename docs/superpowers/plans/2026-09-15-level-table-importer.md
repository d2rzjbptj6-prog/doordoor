# Level Table Importer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Import the separate level source workbook into `level_config.json` and make the Unity demo load those level events at runtime.

**Architecture:** Extend the existing XML-based `ModelTableImporter` with a separate `source`-sheet parser and `Tools/Demo/Import Level Table` menu. Serialize stage IDs and normalized runtime events to `Assets/Resources/Config/level_config.json`; the bootstrap loads this file after its fallback `BuildLevels()` data and replaces matching levels.

**Tech Stack:** Unity Editor C#, `System.IO.Compression`, `System.Xml`, `JsonUtility`, existing `LevelPlan/LevelEvent` runtime model.

**Spec:** User-approved two-file workflow: `model.xlsx` remains unit/buff/monster data; the separate level source workbook remains level placement data.

## Global Constraints

- Do not merge the level workbook into `model.xlsx`.
- Preserve hardcoded levels as a fallback when `level_config.json` is missing or invalid.
- Support the existing two-lane source tokens: `鸡N`, `肥鸡`, `快鸡`, `鸡boss`, `门+N`, `门-N`, `火锁`, `雷锁`, `冰锁`, `弓箱`, `杖箱`.
- Convert a time range such as `3-5` to its first numeric second (`3f`) because the current runtime schedules events by `LevelEvent.Time`.
- Keep unknown/non-event cells as empty lanes and report malformed tokens through Unity warnings.

---

### Task 1: Add source-sheet parsing and level JSON export

**Files:**
- Modify: `GooseMergeDemoProject/Assets/Editor/ModelTableImporter.cs`

**Interfaces:**
- Produces `ImportDefaultLevelTable(bool showDialog)` and the menu command `Tools/Demo/Import Level Table`.
- Writes `Assets/Resources/Config/level_config.json` with `levels[].stageId`, `levels[].name`, `levels[].tip`, and `levels[].events[]`.

- [x] Add serializable level DTOs and event-kind constants matching `Gate`, `ElementGate`, `WeaponRack`, and `ChickenGroup`.
- [x] Find the source header row by locating columns named `关卡`, `左`, `右`, and `秒`; do not assume the source workbook uses the model workbook's four metadata rows.
- [x] Carry the last non-empty stage ID down through blank stage cells.
- [x] Parse both lanes per row. Use the first number in the `秒` cell as event time. Emit lane `-1` for left and `1` for right.
- [x] Parse tokens into normalized events and warn on unsupported tokens.
- [x] Add a file-picker menu and batch-compatible public method; preserve the existing model import menu unchanged.
- [x] Validate duplicate/empty stage IDs and serialize the generated JSON with `JsonUtility.ToJson(config, true)`.

### Task 2: Load imported levels at runtime

**Files:**
- Modify: `GooseMergeDemoProject/Assets/Scripts/GameLogic/Runtime/Game/Demo/GooseMergeDemoBootstrap.cs`

**Interfaces:**
- Consumes `Resources/Config/level_config.json`.
- Produces `LevelPlan` entries with the existing `LevelEvent` event constructors and event loop.

- [x] Add JSON DTOs for levels/events near existing model config DTOs.
- [x] Call `LoadLevelsFromJson()` after `BuildLevels()` in `Boot()` so fallback levels exist before replacement.
- [x] Match imported levels by `stageId` encoded in the level name or explicit DTO field; replace the fallback list by imported order when valid data exists.
- [x] Convert imported DTO event values into the existing private enums and `LevelEvent` fields.
- [x] Sort imported events by time and ignore malformed events without crashing the game.
- [x] Keep `RestartRun()` and next-level behavior unchanged.

### Task 3: Verify the importer and fallback behavior

**Files:**
- Inspect: `Assets/Resources/Config/level_config.json`
- Inspect: `GooseMergeDemoProject/Assets/Scripts/GameLogic/Runtime/Game/Demo/GooseMergeDemoBootstrap.cs`

- [x] Run a standalone token smoke check against `C:/Users/TU/AIOA/doordoor亿扇/关卡source_两路_教学12实战3.xlsx` and confirm all 15 stages contain only supported tokens.
- [x] Run `git diff --check` and inspect the generated code paths for expected event kinds and no empty stage IDs.
- [x] Confirm by code inspection that missing/empty `level_config.json` leaves the hardcoded `BuildLevels()` path available.
- [x] Report that Unity Editor play-mode verification is required if no Unity executable is available in the environment.
