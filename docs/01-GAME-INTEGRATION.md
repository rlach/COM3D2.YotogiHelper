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
