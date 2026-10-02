using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace COM3D2.YotogiHelper
{
    // Bounded, read-only control-flow analysis. Never executes KAG/TJS or mutates game state.
    internal sealed class AffectionHints
    {
        private const string ReconciliationFlag = "ヤキモチモード＿仲直りポイント";
        private static readonly Regex Attribute = new Regex(@"(?<key>\w+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s]+))");
        private static readonly Regex Award = new Regex(@"^AddMaidStatus\(\s*0\s*,\s*['""]好感度['""]\s*,\s*(?<amount>[+-]?\d+)\s*\)\s*;?$");
        private static readonly Regex Reconcile = new Regex(@"^SetMaidFlag\(\s*0\s*,\s*['""]" + ReconciliationFlag + @"['""]\s*,\s*\(\s*GetMaidFlag\(\s*0\s*,\s*['""]" + ReconciliationFlag + @"['""]\s*\)\s*(?<sign>[+-])\s*(?<amount>\d+)\s*\)\s*\)\s*;?$");
        private static readonly HashSet<string> Presentation = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "talk", "hitret", "motionscript", "motion", "face", "faceblend2", "camera", "cameraanimationstart",
            "camerachase", "cameracontrol", "charavisible", "charavisiblealloff", "eyetocamera", "fade", "fade3d",
            "wait", "playse", "playbgm", "stopbgm", "stopse", "setbg", "lightmain", "allpos", "allresetpos",
            "addprefabbg", "delprefabbg", "delprefabbgall", "itemset", "itemreset", "allprocpropseqstart", "messagewindow", "eyetotarget", "stopvoice", "chinko", "offset", "alloffset", "addalloffset"
        };
        private sealed class Node { internal string Tag = "", Source = ""; internal int FalseTarget = -1, EndTarget = -1; }
        private sealed class Script
        {
            internal readonly List<Node> Nodes = new List<Node>();
            internal readonly Dictionary<string, int> Labels = new Dictionary<string, int>(StringComparer.Ordinal);
            internal string Error;
        }
        private readonly Func<string, string> read;
        private readonly Dictionary<string, Script> scripts = new Dictionary<string, Script>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DialogueReward> memo = new Dictionary<string, DialogueReward>(StringComparer.OrdinalIgnoreCase);
        private int remaining;
        private AffectionHints(Func<string, string> read) { this.read = read; }
        internal static string Attr(string source, string key)
        {
            foreach (Match m in Attribute.Matches(source))
                if (string.Equals(m.Groups["key"].Value, key, StringComparison.OrdinalIgnoreCase)) return m.Groups["value"].Value;
            return "";
        }
        private static string Label(string value) { return value.Trim().TrimStart('*').Split('|')[0].Trim(); }
        private static string FileName(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(new[] { '&', '?', '%', '[', ']', '\'', '"' }) >= 0) return null;
            return value.EndsWith(".ks", StringComparison.OrdinalIgnoreCase) ? value : value + ".ks";
        }
        internal static DialogueReward[] Analyze(string file, IList<string> labels, Func<string, string> read)
        {
            var analyzer = new AffectionHints(read);
            var results = new DialogueReward[labels.Count];
            for (int i = 0; i < labels.Count; i++)
            {
                analyzer.remaining = 20000;
                string name = FileName(file);
                results[i] = name == null ? DialogueReward.Unknown("Dynamic script name") : analyzer.Entry(name, labels[i], new HashSet<string>(), 0);
            }
            return results;
        }
        private Script Load(string file)
        {
            Script script;
            if (scripts.TryGetValue(file, out script)) return script;
            script = new Script(); scripts.Add(file, script);
            try
            {
                if (scripts.Count > 64) throw new InvalidOperationException("Script count limit");
                string text = read(file);
                if (text == null || text.Length > 4000000) throw new InvalidOperationException("Missing or oversized script");
                var conditions = new Stack<List<int>>();
                bool scriptBlock = false;
                foreach (string raw in text.Replace("\r", "").Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue;
                    var n = new Node { Source = line };
                    if (scriptBlock)
                    {
                        if (line.Equals("@endscript", StringComparison.OrdinalIgnoreCase) || line.Equals("[endscript]", StringComparison.OrdinalIgnoreCase)) { scriptBlock = false; continue; }
                        n.Tag = "scriptbody"; script.Nodes.Add(n); continue;
                    }
                    if (line.StartsWith("*"))
                    {
                        string label = Label(line);
                        if (script.Labels.ContainsKey(label)) script.Labels[label] = -1;
                        else script.Labels.Add(label, script.Nodes.Count);
                    }
                    else
                    {
                        if (line.StartsWith("[") && line.EndsWith("]") && line.Length > 2) line = "@" + line.Substring(1, line.Length - 2);
                        if (!line.StartsWith("@")) continue;
                        n.Source = line;
                        int space = line.IndexOfAny(new[] { ' ', '\t' });
                        n.Tag = (space < 0 ? line.Substring(1) : line.Substring(1, space - 1)).ToLowerInvariant();
                        if (n.Tag == "iscript") scriptBlock = true;
                    }
                    int index = script.Nodes.Count;
                    script.Nodes.Add(n);
                    if (n.Tag == "if") conditions.Push(new List<int> { index });
                    else if (n.Tag == "else" || n.Tag == "elsif" || n.Tag == "elseif")
                    {
                        if (conditions.Count == 0) { script.Error = "Unbalanced conditional"; break; }
                        var group = conditions.Peek();
                        if (script.Nodes[group[group.Count - 1]].Tag == "else") { script.Error = "Branch after else"; break; }
                        script.Nodes[group[group.Count - 1]].FalseTarget = index;
                        group.Add(index);
                    }
                    else if (n.Tag == "endif")
                    {
                        if (conditions.Count == 0) { script.Error = "Unbalanced conditional"; break; }
                        var group = conditions.Pop();
                        script.Nodes[group[group.Count - 1]].FalseTarget = index;
                        foreach (int branch in group) script.Nodes[branch].EndTarget = index;
                    }
                }
                if (conditions.Count != 0 || scriptBlock) script.Error = "Unterminated block";
            }
            catch (Exception ex) { script.Error = "Cannot read " + file + ": " + ex.Message; }
            return script;
        }
        private DialogueReward Entry(string file, string label, HashSet<string> active, int depth)
        {
            var script = Load(file);
            if (script.Error != null) return DialogueReward.Unknown(script.Error);
            int index = 0;
            if (label.Length != 0 && (!script.Labels.TryGetValue(Label(label), out index) || index < 0 || label.StartsWith("&")))
                return DialogueReward.Unknown("Unresolved label: " + label);
            string key = file + ":" + index;
            DialogueReward cached;
            if (memo.TryGetValue(key, out cached)) return cached;
            var result = Walk(file, index, active, depth);
            if (result.Known) memo[key] = result;
            return result;
        }
        private DialogueReward Condition(string file, int index, HashSet<string> active, int depth)
        {
            if (depth > 64 || --remaining < 0) return DialogueReward.Unknown("Conditional analysis limit");
            var s = Load(file); var n = s.Nodes[index];
            if (n.Tag == "endif" || n.Tag == "else") return Walk(file, index + 1, active, depth + 1);
            if (n.FalseTarget < 0) return DialogueReward.Unknown("Missing conditional endpoint");
            var yes = Walk(file, index + 1, active, depth + 1);
            var no = Condition(file, n.FalseTarget, active, depth + 1);
            return DialogueReward.Add(ConditionEffects(Attr(n.Source, "exp")), DialogueReward.Merge(yes, no));
        }
        private DialogueReward Walk(string file, int index, HashSet<string> ancestors, int depth)
        {
            if (depth > 64) return DialogueReward.Unknown("Call/branch depth limit");
            var s = Load(file);
            if (s.Error != null) return DialogueReward.Unknown(s.Error);
            var active = new HashSet<string>(ancestors);
            var total = new DialogueReward();
            for (int i = index; i < s.Nodes.Count; i++)
            {
                if (--remaining < 0) return DialogueReward.Add(total, DialogueReward.Unknown("Analysis budget exceeded"));
                if (!active.Add(file.ToLowerInvariant() + ":" + i)) return DialogueReward.Add(total, DialogueReward.Unknown("Control-flow cycle"));
                var n = s.Nodes[i];
                if (n.Tag == "") continue;
                if (n.Tag == "if") return DialogueReward.Add(total, Condition(file, i, active, depth + 1));
                if (n.Tag == "else" || n.Tag == "elsif" || n.Tag == "elseif") { i = n.EndTarget; continue; }
                if (n.Tag == "endif") continue;
                bool conditional = Attr(n.Source, "cond").Length != 0;
                if (conditional) total = DialogueReward.Add(total, ConditionEffects(Attr(n.Source, "cond")));
                if (n.Tag == "return" || n.Tag == "s" || n.Tag.StartsWith("choices"))
                {
                    var finish = new DialogueReward { Stops = n.Tag != "return" };
                    if (conditional) finish = DialogueReward.Merge(finish, Walk(file, i + 1, active, depth + 1));
                    return DialogueReward.Add(total, finish);
                }
                DialogueReward effect;
                if (n.Tag == "jump" || n.Tag == "call")
                {
                    string target = Attr(n.Source, "file");
                    target = target.Length == 0 ? file : FileName(target);
                    effect = target == null ? DialogueReward.Unknown("Dynamic script target") : Entry(target, Attr(n.Source, "label"), active, depth + 1);
                    if (n.Tag == "jump")
                    {
                        if (conditional) effect = DialogueReward.Merge(effect, Walk(file, i + 1, active, depth + 1));
                        return DialogueReward.Add(total, effect);
                    }
                }
                else if (n.Tag == "eval") effect = Expression(Attr(n.Source, "exp"));
                else if (n.Tag == "scriptbody")
                {
                    effect = Expression(n.Source); effect.Known = false; effect.Reason = "Embedded script block";
                }
                else if (n.Tag == "charaactivate" && Regex.IsMatch(Attr(n.Source, "man"), @"^\d+$")) continue;
                else if (Presentation.Contains(n.Tag)) continue;
                else effect = DialogueReward.Unknown("Unsupported tag: " + n.Tag);
                if (conditional) effect = DialogueReward.Merge(effect, new DialogueReward());
                total = DialogueReward.Add(total, effect);
                if (effect.Stops) return total;
            }
            return DialogueReward.Add(total, DialogueReward.Unknown("Script ended without return/stop"));
        }
        private static DialogueReward ConditionEffects(string expression)
        {
            // Conditions are not executed; only known read-only queries are effect-free.
            bool safe = !Regex.IsMatch(expression, @"(?<![=!<>])=(?!=)|\+\+|--|;");
            foreach (Match call in Regex.Matches(expression, @"([a-zA-Z_]\w*)\s*\("))
                if (!Regex.IsMatch(call.Groups[1].Value, @"^(?:GetTmpFlag|GetMaidFlag|GetManFlag|GetMaidStatus|GetSystemFlag|IsVRMode)$")) safe = false;
            if (safe) return new DialogueReward();
            return new DialogueReward { Known = false, HasAffection = expression.Contains("好感度"),
                HasReconciliation = expression.Contains(ReconciliationFlag), Reason = "Condition may have side effects" };
        }
        private static DialogueReward Expression(string expression)
        {
            expression = expression.Trim();
            var a = Award.Match(expression); var r = Reconcile.Match(expression);
            int value;
            if (a.Success && int.TryParse(a.Groups["amount"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return new DialogueReward { HasAffection = true, Affection = value };
            if (r.Success && int.TryParse(r.Groups["amount"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return new DialogueReward { HasReconciliation = true, Reconciliation = r.Groups["sign"].Value == "-" ? -value : value };
            if (expression.Contains("好感度") || expression.Contains(ReconciliationFlag))
                return new DialogueReward { Known = false, HasAffection = expression.Contains("好感度"), HasReconciliation = expression.Contains(ReconciliationFlag), Reason = "Nonliteral or unsupported reward expression" };
            // Bookkeeping may influence branches, but every branch is analyzed, never executed.
            if (Regex.IsMatch(expression, @"^\s*(?:SetTmpFlag\(|SetMaidFlag\(|tf\[|f\[|global\.)"))
            {
                bool safe = !expression.StartsWith("SetMaidFlag", StringComparison.Ordinal) ||
                    Regex.IsMatch(expression, @"^SetMaidFlag\(\s*\d+\s*,\s*['""][^'""]+['""]\s*,");
                foreach (Match call in Regex.Matches(expression, @"([a-zA-Z_]\w*)\s*\("))
                    if (!Regex.IsMatch(call.Groups[1].Value, @"^(?:SetTmpFlag|SetMaidFlag|GetTmpFlag|GetMaidFlag|GetMaidStatus|GetFlag)$")) safe = false;
                if (safe && expression.IndexOf(';') < 0) return new DialogueReward();
            }
            return DialogueReward.Unknown("Unsupported expression");
        }
    }
}
