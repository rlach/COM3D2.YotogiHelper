using System;
using System.Collections.Generic;
using COM3D2.YotogiHelper;

internal static class DialogueTests
{
    private static string Gain(string amount) { return "@eval exp=\"AddMaidStatus(0,'好感度'," + amount + ")\"\n"; }
    private static string Reconcile(string amount) { return "@eval exp=\"SetMaidFlag(0,'ヤキモチモード＿仲直りポイント',(GetMaidFlag(0,'ヤキモチモード＿仲直りポイント')" + amount + "))\"\n"; }
    private static DialogueReward[] Analyze(string text, params string[] labels)
    { return AffectionHints.Analyze("Future DLC 2035.ks", labels, name => text); }
    internal static void Run(Action<bool, string> check)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        files["unseen personality.ks"] = "*option_a\n@call file=arbitrary_rewards label=*nested\n@jump label=*arbitrary_end\n*option_b\n" + Gain("0") + "*arbitrary_end\n@fade out\n@return\n";
        files["arbitrary_rewards.ks"] = "*nested\n@call file=more_rewards label=*value\n@return\n";
        files["more_rewards.ks"] = "*value\n" + Gain("5") + "@return\n";
        var result = AffectionHints.Analyze("unseen personality.ks", new[] { "*option_b", "*option_a" }, name => files[name]);
        check(result[0].Known && result[0].Affection == 0 && result[0].HasReward, "explicit zero, arbitrary file/label and two choices");
        check(result[1].Known && result[1].Affection == 5, "nested calls, local jump, shuffled choices");
        result = Analyze("*first\n" + Gain("2") + Gain("3") + Reconcile("+1") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 5 && result[0].Reconciliation == 1 && result[0].Text.Contains("\n"), "sum multiple and mixed rewards");
        result = Analyze("*first\n" + Reconcile("-2") + "@return", "*first");
        check(result[0].Known && result[0].Reconciliation == -2 && !result[0].HasAffection, "reconciliation identified from content");
        result = Analyze("*first\n" + Gain("-5") + "@return", "*first");
        check(result[0].Known && result[0].Affection == -5, "negative affection");
        result = Analyze("*first\n@if exp=flag\n" + Gain("5") + "@else\n" + Gain("5") + "@endif\n" + Gain("2") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 7, "equal conditional outcomes and shared continuation");
        result = Analyze("*first\n@if exp=flag\n" + Gain("5") + "@else\n" + Gain("3") + "@endif\n@return\n*second\n" + Gain("4") + "@return", "*first", "*second");
        check(!result[0].Known && result[0].HasReward && result[0].Text == "Unknown" && result[1].Known && result[1].Affection == 4, "unequal branches do not suppress another option");
        result = Analyze("*first\n@if exp=a\n@if exp=b\n" + Gain("3") + "@else\n" + Gain("3") + "@endif\n@elsif exp=c\n" + Gain("3") + "@else\n" + Gain("3") + "@endif\n@return", "*first");
        check(result[0].Known && result[0].Affection == 3, "nested if/elsif/else");
        result = Analyze("*first\n@if exp=flag\n" + Gain("5") + "@endif\n@return", "*first");
        check(!result[0].Known && result[0].HasReward, "implicit no-reward conditional branch");
        result = Analyze("*first\n" + Gain("5").TrimEnd('\n') + " cond=flag\n@return", "*first");
        check(!result[0].Known && result[0].HasReward, "conditional reward attribute");
        result = Analyze("*first\n" + Gain("GetBonus()") + "@return", "*first");
        check(!result[0].Known && result[0].HasAffection, "computed reward is unknown, never executed");
        result = Analyze("*first\n@new_unknown_macro\n" + Gain("5") + "@return", "*first");
        check(!result[0].Known && result[0].HasReward, "unknown macro cannot silently change outcome");
        result = Analyze("*first\n@return\n*second\n" + Gain("3") + "@return", "*first", "*second", "*missing");
        check(result[0].Known && result[0].Text == "No change" && !result[2].Known && result[1].Known, "no reward and missing labels are distinct");
        result = Analyze("*first\n" + Gain("1") + "@jump label=*first", "*first");
        check(!result[0].Known, "jump cycle terminates");
        result = Analyze("*first\n@call label=*first\n@return", "*first");
        check(!result[0].Known, "recursive call terminates");
        result = Analyze("*first\n@jump label=&dynamic", "*first");
        check(!result[0].Known, "dynamic jump is unknown");
        result = Analyze("*first\n@call file=&dynamic label=*reward\n" + Gain("5") + "@return", "*first");
        check(!result[0].Known && result[0].HasReward, "dynamic call does not invalidate other options");
        result = AffectionHints.Analyze("initial.ks", new[] { "*first" }, name => { if(name != "initial.ks") throw new Exception("Missing"); return "*first\n@call file=missing\n" + Gain("5") + "@return"; });
        check(!result[0].Known && result[0].HasReward, "missing dependency is isolated");
        result = Analyze("*first\n" + Gain("2") + "@ChoicesSet label=*later text=Next\n@ChoicesShow\n*later\n" + Gain("99") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 2, "stop before next decision");
        files["initial.ks"] = "*first\n@call file=child label=*start\n" + Gain("99") + "@return";
        files["child.ks"] = "*start\n" + Gain("2") + "@s";
        result = AffectionHints.Analyze("initial.ks", new[] { "*first" }, name => files[name]);
        check(result[0].Known && result[0].Affection == 2, "callee stop does not return to caller");
        files["child.ks"] = "*start\n" + Gain("2") + "@return";
        files["initial.ks"] = "*first\n@jump file=child label=*start\n" + Gain("99") + "@return";
        result = AffectionHints.Analyze("initial.ks", new[] { "*first" }, name => files[name]);
        check(result[0].Known && result[0].Affection == 2, "cross-file jump");
        result = Analyze("*first\n@jump label=*other cond=flag\n" + Gain("5") + "@return\n*other\n" + Gain("5") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 5, "conditional jump equal outcomes");
        result = Analyze("*first\n@iscript\nAddMaidStatus(0,'好感度',5);\n@endscript\n@return", "*first");
        check(!result[0].Known && result[0].HasReward, "embedded script is not mistaken for dialogue text");
        result = Analyze("*first\n@if exp=flag\n" + Gain("5") + "@return", "*first");
        check(!result[0].Known, "malformed condition rejected");
        result = Analyze("*first\n" + Gain("2147483647") + Gain("1") + "@return", "*first");
        check(!result[0].Known, "reward overflow rejected");
        result = Analyze("*first\n@eval exp=\"SetTmpFlag('choice',1)\"\n@eval exp=\"tf['index'] = 0\"\n" + Gain("3") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 3, "bookkeeping does not prevent constant reward analysis");
        result = Analyze("*first\n" + Gain("5").Replace("(0,", "(1,") + "@return", "*first");
        check(!result[0].Known && result[0].HasAffection, "do not attribute another maid's reward to primary maid");
        result = Analyze("*first\n[if exp=flag]\n" + Gain("3") + "[else]\n" + Gain("3") + "[endif]\n@return", "*first");
        check(result[0].Known && result[0].Affection == 3, "standalone inline KAG commands");
        result = Analyze("*first\n" + Gain("5"), "*first");
        check(!result[0].Known, "unexpected EOF is not a proven outcome");
        var options = new[] { "*a", "*b", "*c", "*d" };
        result = Analyze("*a\n"+Gain("1")+"@return\n*b\n"+Gain("2")+"@return\n*c\n"+Gain("3")+"@return\n*d\n"+Gain("4")+"@return", options);
        check(result.Length == 4 && result[3].Known && result[3].Affection == 4, "no three-choice restriction");
        result = Analyze("*unused\n*unused\n*first\n" + Gain("5") + "@return", "*first", "*unused");
        check(result[0].Known && !result[1].Known, "duplicate unrelated text labels do not reject whole script");
        result = Analyze("*first\n@CharaActivate man=1 npc=extra\n@EyeToTarget maid=0 target=head\n@StopVoice\n@Chinko off\n" + Gain("5") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 5, "native presentation tags and male activation");
        result = Analyze("*first\n@CharaActivate maid=0 npc=extra\n" + Gain("5") + "@return", "*first");
        check(!result[0].Known, "maid replacement cannot silently redirect reward");
        result = Analyze("*first\n@eval exp=\"SetMaidFlag(0,tf['rewardKey'],3)\"\n" + Gain("5") + "@return", "*first");
        check(!result[0].Known, "dynamic flag key cannot be classified as harmless bookkeeping");
        result = Analyze("*first\n@call label=*award\n@call label=*award\n@return\n*award\n" + Gain("2") + "@return", "*first");
        check(result[0].Known && result[0].Affection == 4, "cached call summaries still count repeated calls");
        result = Analyze("*first\n@if exp=\"AddMaidStatus(0,'好感度',5)\"\n@face name=happy\n@endif\n@return", "*first");
        check(!result[0].Known && result[0].HasAffection, "condition side effects are not ignored");
        result = Analyze("*first\n" + Gain("5").TrimEnd('\n') + " cond=GiveBonus()\n@return", "*first");
        check(!result[0].Known, "conditional tag with unknown side effects");
    }
}
