# COM3D2.YotogiHelper

A room-selection overlay and optional dialogue hints for **Custom Order Maid 3D 2**, usable in desktop and VR mode. Compare the Yotogi activities available to your selected maid before choosing a room.

- Room and category summaries: **Available unlocked/total. Maxed: mastered**.
- Mode and category buttons with counts; empty groups are disabled.
- Paginated activity lists with localized names and level stars. Unlocked activities are white; locked activities are gray.
- Automatic updates when selecting a room.
- On-demand affection and reconciliation hints, detected from dialogue scripts rather than DLC or personality names.
- Movable, resizable panel with separate saved layouts for desktop and VR.

Press **F9** or use the door icon in the system menu to show or hide the panel. Reopening centers it. The plugin only displays information; it does not unlock activities or modify progress.

**Dialogue hints:** when the current answers contain recognizable relationship rewards, click **Show hints** at the bottom right. Answers show **Affection**, **Reconciliation**, or **No change**. Unresolved answers show **Unknown** independently of the other answers. Hints reset after every choice. Detection reads your installed scripts and does not require a list of supported personalities, DLC names, filenames, or a fixed number of choices. Values are scripted increments before game status caps; conditional or dynamically computed outcomes may remain unknown.

The room overlay supports the regular COM3D2 Yotogi room selector. The legacy CM3D2 selector, recollection selectors, and scripted sequences that skip room selection are not supported. Counts reflect the maid's current conditions; unlocked activities may still require additional participants.

## Installation

Requires a working **BepInEx 5** installation and **COM3D2.API**. The build references used for this version are BepInEx 5.4.23.2 and COM3D2.API 1.1.

1. Close the game.
2. Download `COM3D2.YotogiHelper.dll` from [Releases](https://github.com/rlach/COM3D2.YotogiHelper/releases/latest).
3. Copy it into `BepInEx/plugins`, replacing any previous version.
4. Open the regular Yotogi room selection screen.

Settings: `BepInEx/config/COM3D2.YotogiHelper.cfg`. Logs: `BepInEx/LogOutput.log`.

## Build

On Windows, install the **.NET SDK** (tested with 8.0.401). A local copy of the game with BepInEx and COM3D2.API is required; game libraries are not included in this repository.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -GamePath "C:\Games\COM3D2\com3d2inm" -Test
```

Output: `artifacts/COM3D2.YotogiHelper.dll`. The optional `-Test` switch runs the domain and dialogue-parser tests; it does not launch the game.
