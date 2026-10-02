using System;
using System.Collections.Generic;
using System.Globalization;

namespace COM3D2.YotogiHelper
{
    internal sealed class DialogueReward
    {
        internal bool Known = true, HasAffection, HasReconciliation, Stops;
        internal int Affection, Reconciliation;
        internal string Reason = "";
        internal bool HasReward { get { return HasAffection || HasReconciliation; } }
        internal string Text
        {
            get
            {
                if (!Known) return "Unknown";
                var parts = new List<string>();
                if (HasAffection) parts.Add("Affection " + Affection.ToString("+0;-0;+0", CultureInfo.InvariantCulture));
                if (HasReconciliation) parts.Add("Reconciliation " + Reconciliation.ToString("+0;-0;+0", CultureInfo.InvariantCulture));
                return parts.Count == 0 ? "No change" : string.Join("\n", parts.ToArray());
            }
        }
        internal static DialogueReward Unknown(string reason) { return new DialogueReward { Known = false, Reason = reason }; }
        internal static DialogueReward Add(DialogueReward a, DialogueReward b)
        {
            var r = new DialogueReward { Known = a.Known && b.Known, HasAffection = a.HasAffection || b.HasAffection,
                HasReconciliation = a.HasReconciliation || b.HasReconciliation, Stops = a.Stops || b.Stops,
                Reason = a.Known ? b.Reason : a.Reason };
            try { r.Affection = checked(a.Affection + b.Affection); r.Reconciliation = checked(a.Reconciliation + b.Reconciliation); }
            catch (OverflowException) { r.Known = false; r.Reason = "Reward overflow"; }
            return r;
        }
        internal static DialogueReward Merge(DialogueReward a, DialogueReward b)
        {
            bool same = a.Known && b.Known && a.Affection == b.Affection && a.Reconciliation == b.Reconciliation && a.Stops == b.Stops;
            return new DialogueReward { Known = same, Affection = a.Affection, Reconciliation = a.Reconciliation,
                HasAffection = a.HasAffection || b.HasAffection, HasReconciliation = a.HasReconciliation || b.HasReconciliation,
                Stops = a.Stops || b.Stops, Reason = same ? "" : (!a.Known ? a.Reason : !b.Known ? b.Reason : "Different conditional outcomes") };
        }
    }
}
