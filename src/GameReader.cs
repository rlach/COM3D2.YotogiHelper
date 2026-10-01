using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Yotogis;
using MaidStatus;

namespace COM3D2.YotogiHelper
{
    internal static class GameReader
    {
        internal static string Text(string term, string fallback)
        {
            string value = string.IsNullOrEmpty(term) ? null : I2.Loc.LocalizationManager.GetTranslation(term);
            return string.IsNullOrEmpty(value) || value == term ? fallback : value;
        }
        // Read the selector's serialized modes, even before its Awake has initialized enum values.
        private static HashSet<Skill.Data.SpecialConditionType> Modes(YotogiManager manager)
        {
            var result = new HashSet<Skill.Data.SpecialConditionType>();
            result.Add(Skill.Data.SpecialConditionType.Null);
            foreach (var selector in manager.GetComponentsInChildren<YotogiSkillSelectManager>(true))
            {
                var settings = selector.conditionSetting;
                if (!settings) continue;
                var field = AccessTools.Field(settings.GetType(), "checkBoxButtonDatas");
                var buttons = field == null ? null : field.GetValue(settings) as IEnumerable;
                if (buttons == null) continue;
                foreach (object button in buttons)
                {
                    var name = AccessTools.Field(button.GetType(), "specialConditionName");
                    if (name == null) continue;
                    string value = name.GetValue(button) as string;
                    if (!string.IsNullOrEmpty(value) && Enum.IsDefined(typeof(Skill.Data.SpecialConditionType), value))
                        result.Add((Skill.Data.SpecialConditionType)Enum.Parse(typeof(Skill.Data.SpecialConditionType), value));
                }
            }
            if (result.Count == 1)
                throw new InvalidOperationException("Cannot read the native skill selector modes; counts would be incomplete.");
            return result;
        }
        internal static RoomSnapshot Read(YotogiManager manager, YotogiStageSelectManager.StageExpansionPack pack)
        {
            if (!manager || !manager.maid || pack == null || pack.stageData == null)
                throw new InvalidOperationException("No selected maid or room.");
            Maid maid = manager.maid;
            var stage = pack.stageData;
            var result = new RoomSnapshot {
                MaidName = maid.status.firstName + " " + maid.status.lastName,
                RoomName = pack.yotogiAdaptMyRoomStageData == null ? Text(stage.termName, stage.drawName) : pack.yotogiAdaptMyRoomStageData.myRoomName,
                RoomPlayable = stage.isYotogiPlayable(maid, GameMain.Instance.CharacterMgr.status.clubGrade, true)
            };
            var modes = Modes(manager);
            foreach (var type in modes)
                result.Modes[(int)type] = type == Skill.Data.SpecialConditionType.Null ? "Normal" :
                    type == Skill.Data.SpecialConditionType.Mask ? "Blindfold" : type.ToString();
            foreach (Yotogi.Category type in Enum.GetValues(typeof(Yotogi.Category)))
                if (type != Yotogi.Category.MAX) result.Categories[(int)type] = Text("SceneYotogi/スキルカテゴリー/" + type, type.ToString());
            int participants = manager.GetPlayPossibleMaidCount();
            bool ntrBlocked = GameMain.Instance.CharacterMgr.status.lockNTRPlay;
            foreach (var item in YotogiSkillListManager.CreateDatas(maid.status, false).Values)
            {
                var skill = item.skillData;
                if (skill == null || !modes.Contains(skill.specialConditionType) || !skill.IsExecStage(stage) ||
                    !PersonalEventBlocker.IsEnabledYotodiSkill(maid.status.personal, skill.id)) continue;
                if (ntrBlocked && (skill.category == Yotogi.Category.交換 || skill.category == Yotogi.Category.乱交)) continue;
                var progress = item.maidStatusSkillData;
                result.Rows.Add(new SkillRow {
                    Id = skill.id, Sort = skill.sortId, Mode = (int)skill.specialConditionType, Category = (int)skill.category,
                    Name = Text(skill.termName, skill.name),
                    ModeName = skill.specialConditionType == Skill.Data.SpecialConditionType.Null ? "Normal" : skill.specialConditionType.ToString(),
                    CategoryName = Text("SceneYotogi/スキルカテゴリー/" + skill.category, skill.category.ToString()),
                    Unlocked = progress != null, Level = progress == null ? 0 : progress.level,
                    MaxLevel = progress == null ? 0 : progress.expSystem.GetMaxLevel(),
                    RequiredMaids = skill.player_num, EnoughMaids = participants >= skill.player_num,
                    HasStamina = manager.skill_select_max_hp > 0
                });
            }
            return result;
        }
    }
}
