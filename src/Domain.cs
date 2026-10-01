using System;
using System.Collections.Generic;

namespace COM3D2.YotogiHelper
{
    internal sealed class SkillRow
    {
        public int Id, Mode, Category, Sort, Level, MaxLevel, RequiredMaids;
        public string Name = "", ModeName = "", CategoryName = "";
        public bool Unlocked, EnoughMaids, HasStamina;
        public bool Mastered { get { return Unlocked && MaxLevel > 0 && Level >= MaxLevel; } }
        public bool Selectable { get { return Unlocked && EnoughMaids && HasStamina; } }
        public int Stars { get { return Unlocked ? Math.Max(0, Math.Min(3, Level)) : 0; } }
    }
    internal sealed class Counts
    {
        public int Total, Unlocked, Mastered, Selectable;
        public static Counts From(IEnumerable<SkillRow> rows)
        {
            var result = new Counts();
            var ids = new HashSet<int>();
            foreach (var row in rows)
            {
                if (!ids.Add(row.Id)) continue;
                result.Total++;
                if (row.Unlocked) result.Unlocked++;
                if (row.Mastered) result.Mastered++;
                if (row.Selectable) result.Selectable++;
            }
            return result;
        }
    }
    internal sealed class RoomSnapshot
    {
        public string MaidName = "", RoomName = "";
        public bool RoomPlayable = false;
        public readonly Dictionary<int, string> Modes = new Dictionary<int, string>(), Categories = new Dictionary<int, string>();
        public readonly List<SkillRow> Rows = new List<SkillRow>();
        public List<SkillRow> Filter(int mode, int category)
        {
            var result = Rows.FindAll(row => row.Mode == mode && row.Category == category);
            result.Sort(delegate(SkillRow a, SkillRow b) {
                int value = a.Sort.CompareTo(b.Sort);
                return value != 0 ? value : a.Id.CompareTo(b.Id);
            });
            return result;
        }
        public static int ClampPage(int page, int count, int size)
        { return Math.Max(0, Math.Min(page, Math.Max(0, (count - 1) / size))); }
    }
}
