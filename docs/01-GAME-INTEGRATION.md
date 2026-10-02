# Game integration

## Lifecycle

Harmony postfixes on `YotogiStageSelectManager.OnCall`, `SelectStage`, and `OnFinish` attach, refresh, and dispose the panel. Data is read in Update after the native room selector initializes. The selected maid comes from `YotogiManager.maid`; the room comes from `SelectedStage`. Scripted new-style sequences skip this selector.

The plugin does not invoke the skill selector's OnCall method, which resets session state.

## Activity catalog

GameReader uses `YotogiSkillListManager.CreateDatas(status, false)`, then filters by:

- Modes serialized in the native selector's condition settings, plus Normal.
- `skill.IsExecStage(stage)`.
- `PersonalEventBlocker.IsEnabledYotodiSkill(personal, id)`.
- The player's NTR category restrictions.

CreateDatas already checks personality, relationship, contract, experience conditions, and future learning eligibility. Counts are not a complete catalog of everything a maid could eventually learn.

Mode and category catalogs are retained independently of matching rows so the UI can display disabled zero-count buttons.

## Progress and counts

An activity is unlocked when its maid-specific progress record exists. Mastered means its level reaches a positive maximum reported by `expSystem.GetMaxLevel()`. Stars display the level, capped at three.

The displayed Available numerator is the unlocked count; its denominator includes locked matching activities. Counts deduplicate skill IDs. Missing participants are noted in activity names. Internal selectable counts also check initial stamina and participant count, but do not simulate a queued sequence or its conflicts.

## UI and localization

The overlay uses the game's NGUI, atlas, font, and native pointer interaction. COM3D2.API provides the system-menu button. Localized skill and category names use I2 translation terms with source-text fallbacks. Normal and Blindfold are display aliases for native mode names.

Only window position, scale, and shortcut configuration are persisted. No game progress is written.

## Development

The build uses .NET 3.5-compatible references from the installed game. `build.ps1 -Test` runs domain tests for aggregation, levels, availability, grouping, and pagination; these do not emulate Unity or VR interaction.

Local inspection materials and downloaded tools belong in ignored `.inspection` and `.tools` directories. Do not commit game assemblies, assets, decompiled source, logs, or build outputs.

## Dialogue reward analysis

A postfix on `SelectButtonCtrl.CreateSelectButtons` captures the displayed answers' target labels and the active ADV script. There is no filename, personality, DLC, three-choice, reward-helper filename, or endpoint-label allowlist. An analysis instance and its caches belong to one choice creation, so changing saves or scripts cannot reuse an old outcome.

`AffectionHints` builds label and conditional-block indexes, then follows each answer through literal local/cross-file jumps, nested calls, fall-through, and returns. A new choice or stop ends the current outcome, including inside a called script. Multiple increments are summed. The recognized effects are literal affection changes for maid slot 0 and increments/decrements of the native reconciliation flag; their labels are derived from these operations, not filenames. Known presentation commands have no reward effect. Male activation is allowed; maid replacement remains unknown because it can redirect a later reward.

Conditionals are analyzed along both possible paths without executing expressions or querying mutable game state. If every path produces the same outcome, it is exact. Otherwise that answer is Unknown. This includes recollection guards that award points only outside replay mode. Unknown commands, dynamic targets, embedded script blocks, missing dependencies, ambiguous referenced labels, and nonliteral rewards do not become fabricated zeros. No change is reserved for an understood path with no relationship change. Unreferenced duplicate labels do not invalidate the script.

`DialogueReward` preserves detected reward kinds even when an answer becomes unknown. The UI appears if at least one answer has evidence of a relationship reward, and presents each answer independently. Reasons for unknown answers are logged with the filename and target label. Analysis failures are isolated to one choice and do not disable later dialogues.

Analysis is bounded by 20,000 visited nodes per answer, 64 levels of calls/branches, 64 scripts, and a 4 MB limit per script. Cycles are detected; completed exact call summaries are memoized. No game script or expression is executed, and no game state is written.

Scripts are read through `ScriptManager.file_system`, matching the active virtual filesystem. CP932 decoding uses Windows `MultiByteToWideChar`, avoiding Unity Mono's optional code-page assemblies. UTF-8 with a BOM is also accepted. Invalid byte sequences are rejected. Game scripts, assets, and dialogue text are not bundled with the plugin.

The reveal button is a separate NGUI overlay anchored at the bottom right, usable in desktop and VR. Revealed labels follow native options without replacing localized text, colliders, callbacks, or child hierarchy. Selecting an answer, clearing/replacing choices, disappearing controls, changing scripts, or destroying the plugin disposes the hints. Reveal state is not persisted. Room selection and dialogue hints have independent lifecycles.

## Validation

`build.ps1 -Test` covers reward composition, shuffled and variable-count answers, arbitrary names, nested calls, cross-file and conditional jumps, equal and unequal conditional outcomes, isolated unknown results, replay-like reward guards, termination, cycles, missing scripts, decoding, overflow, and the existing room-domain behavior.

The local offline corpus contains 153 conversations (459 answers). With the inspected helper scripts available, 150 conversations have complete exact outcomes and three have partial outcomes: 453 answers are exact and six are Unknown because their reward depends on a replay-mode condition. In the local run the slowest conversation took about 10 ms; Unity runtime performance and visual/pointer behavior still require in-game verification. Inspection files and the corpus runner remain ignored and are not distributed.
