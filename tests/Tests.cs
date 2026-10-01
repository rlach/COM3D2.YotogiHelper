using System;
using COM3D2.YotogiHelper;
internal static class Tests
{
    private static int count;
    private static void Check(bool value, string name) { count++; if (!value) throw new Exception(name); }
    private static void Main()
    {
        var locked = new SkillRow { Id = 1, Level = 3, MaxLevel = 3, EnoughMaids = true, HasStamina = true };
        var trained = new SkillRow { Id = 2, Unlocked = true, Level = 2, MaxLevel = 3, EnoughMaids = true, HasStamina = true };
        var mastered = new SkillRow { Id = 3, Unlocked = true, Level = 3, MaxLevel = 3, HasStamina = true, RequiredMaids = 2 };
        Check(!locked.Mastered && !locked.Selectable && locked.Stars == 0, "locked state");
        Check(!trained.Mastered && trained.Selectable && trained.Stars == 2, "trained state");
        Check(mastered.Mastered && !mastered.Selectable && mastered.Stars == 3, "maxed but insufficient participants");
        var counts = Counts.From(new[] { locked, trained, mastered, trained });
        Check(counts.Total == 3 && counts.Unlocked == 2 && counts.Mastered == 1 && counts.Selectable == 1, "unique counts");
        trained.HasStamina = false; Check(!trained.Selectable, "no stamina");
        trained.HasStamina = true; trained.MaxLevel = 0; Check(!trained.Mastered, "unknown cap");
        trained.MaxLevel = 2; Check(trained.Mastered, "native max level");
        trained.Level = 4; Check(trained.Stars == 3 && trained.Mastered, "level over cap");
        var room = new RoomSnapshot();
        trained.Mode = 1; trained.Category = 2; trained.Sort = 20;
        mastered.Mode = 1; mastered.Category = 2; mastered.Sort = 10;
        locked.Mode = 0; locked.Category = 2;
        room.Rows.Add(trained); room.Rows.Add(mastered); room.Rows.Add(locked);
        var filtered = room.Filter(1, 2);
        Check(filtered.Count == 2 && filtered[0] == mastered, "mode/category and native sort");
        Check(room.Filter(0, 2).Count == 1, "normal separate");
        Check(room.Filter(1, 3).Count == 0, "empty category");
        Check(RoomSnapshot.ClampPage(9, 0, 8) == 0, "empty page");
        Check(RoomSnapshot.ClampPage(9, 8, 8) == 0, "full first page");
        Check(RoomSnapshot.ClampPage(9, 9, 8) == 1, "partial last page");
        Check(RoomSnapshot.ClampPage(-1, 9, 8) == 0, "previous bounds");
        Check(RoomSnapshot.ClampPage(2, 16, 8) == 1, "removed last row");
        var empty = Counts.From(new SkillRow[0]);
        Check(empty.Total == 0 && empty.Mastered == 0 && empty.Selectable == 0, "empty totals");
        Console.WriteLine("PASS: " + count + " checks.");
    }
}
