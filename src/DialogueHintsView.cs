using System;
using System.Collections.Generic;

using UnityEngine;

namespace COM3D2.YotogiHelper
{
    internal sealed class DialogueHintsView : IDisposable
    {
        internal readonly SelectButtonCtrl Owner;
        private readonly GameObject root, reveal;
        private readonly Transform parent;
        private readonly Transform[] options;
        private readonly UILabel[] hints;
        private readonly UIPanel sourcePanel;
        private bool revealed;
        private readonly string script;

        internal static DialogueHintsView Create(SelectButtonCtrl owner, List<KeyValuePair<string, KeyValuePair<string, bool>>> choices, Action<string> report)
        {
            string file = GameMain.Instance.ScriptMgr.adv_kag.kag.GetCurrentFileName();
            var labels = new List<string>();
            foreach (var choice in choices) labels.Add(choice.Value.Key);
            var rewards = AffectionHints.Analyze(file, labels, Read);
            bool relevant = false;
            foreach (var reward in rewards) if (reward.HasReward) relevant = true;
            if (!relevant)
            {
                string reason = "no recognized relationship reward";
                foreach (var reward in rewards) if (!reward.Known) { reason += "; " + reward.Reason; break; }
                report("Dialogue hints skipped: script=" + file + "; " + reason); return null;
            }
            var values = new string[rewards.Length];
            for (int i = 0; i < rewards.Length; i++)
            {
                values[i] = rewards[i].Text;
                if (!rewards[i].Known) report("Dialogue hint unknown: script=" + file + "; label=" + labels[i] + "; " + rewards[i].Reason);
            }
            report("Dialogue hints ready: script=" + file + "; choices=" + choices.Count + "; VR=" + GameMain.Instance.VRMode);
            return new DialogueHintsView(owner, file, values);
        }
        private static string Read(string name)
        {
            if (!name.EndsWith(".ks", StringComparison.OrdinalIgnoreCase)) name += ".ks";
            using (var file = GameMain.Instance.ScriptMgr.file_system.FileOpen(name))
            {
                if (!file.IsValid()) throw new InvalidOperationException("Cannot read dialogue script: " + name);
                return ScriptText.Decode(file.ReadAll());
            }
        }
        private DialogueHintsView(SelectButtonCtrl owner, string file, string[] values)
        {
            Owner = owner; script = file;
            sourcePanel = owner.GetComponentInParent<UIPanel>();
            var system = GameObject.Find("SystemUI Root");
            if (!system) throw new InvalidOperationException("System UI unavailable for dialogue hints.");
            parent = system.transform;
            root = Child(parent, "YotogiAffectionHints");
            try
            {
                var sample = system.GetComponentInChildren<UIWidget>();
                root.layer = sample ? sample.gameObject.layer : system.layer;
                root.AddComponent<UIPanel>().depth = NGUITools.CalculateNextDepth(system) + 10;
                var font = Resources.Load<Font>("font/notosanscjkjp-hinted/notosanscjkjp-demilight");
                var atlas = Resources.Load<UIAtlas>("CommonUI/Atlas/AtlasCommon");
                if (!font || !atlas) throw new InvalidOperationException("Native font/atlas unavailable.");
                options = new Transform[values.Length]; hints = new UILabel[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    options[i] = owner.transform.Find("selectButton_" + i);
                    if (!options[i]) throw new InvalidOperationException("Dialogue choice is missing.");
                    hints[i] = Label(root.transform, font, values[i]);
                    hints[i].gameObject.SetActive(false);
                }
                reveal = Child(root.transform, "Show hints");
                var sprite = reveal.AddComponent<UISprite>(); sprite.atlas = atlas;
                sprite.spriteName = "cm3d2_common_plate_white"; sprite.type = UIBasicSprite.Type.Sliced;
                sprite.width = 180; sprite.height = 46; sprite.depth = 1; sprite.color = new Color(.16f, .22f, .29f);
                NGUITools.AddWidgetCollider(reveal);
                var button = reveal.AddComponent<UIButton>(); button.tweenTarget = reveal;
                button.defaultColor = sprite.color; button.hover = new Color(.3f, .42f, .52f);
                button.pressed = new Color(.1f, .16f, .2f);
                Label(reveal.transform, font, "Show hints");
                EventDelegate.Add(button.onClick, delegate { revealed = true; reveal.SetActive(false); });
                root.SetActive(false);
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }
        private static GameObject Child(Transform parent, string name)
        {
            var obj = new GameObject(name); obj.layer = parent.gameObject.layer;
            obj.transform.SetParent(parent, false); return obj;
        }
        private static UILabel Label(Transform parent, Font font, string text)
        {
            var label = Child(parent, "Label").AddComponent<UILabel>();
            label.trueTypeFont = font; label.fontSize = 22; label.text = text;
            label.width = 180; label.height = 44; label.depth = 2;
            label.supportEncoding = false; label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.effectStyle = UILabel.Effect.Shadow; label.effectColor = Color.black;
            return label;
        }
        internal bool Tick()
        {
            if (!root || !Owner || !Owner.gameObject.activeInHierarchy || !parent) return false;
            if (GameMain.Instance.ScriptMgr.adv_kag.kag.GetCurrentFileName() != script) return false;
            foreach (var option in options) if (!option || !option.gameObject.activeInHierarchy) return false;
            bool visible = !sourcePanel || sourcePanel.alpha > .01f;
            root.SetActive(visible);
            if (!visible) return true;
            var uiRoot = parent.GetComponent<UIRoot>();
            var camera = UICamera.FindCameraForLayer(root.layer);
            float height = uiRoot ? uiRoot.activeHeight : 720;
            float aspect = camera ? camera.GetComponent<Camera>().aspect : (float)Screen.width / Math.Max(1, Screen.height);
            float right = height * aspect * .5f;
            reveal.transform.localPosition = new Vector3(right - 110, -height * .5f + 48, 0);
            for (int i = 0; i < options.Length; i++)
            {
                hints[i].gameObject.SetActive(revealed);
                if (!revealed) continue;
                float x = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
                foreach (var widget in options[i].GetComponentsInChildren<UIWidget>())
                {
                    if (!widget.enabled || !widget.gameObject.activeInHierarchy) continue;
                    foreach (var corner in widget.worldCorners)
                    {
                        var p = parent.InverseTransformPoint(corner);
                        x = Mathf.Max(x, p.x); bottom = Mathf.Min(bottom, p.y); top = Mathf.Max(top, p.y);
                    }
                }
                if (x == float.MinValue) { hints[i].gameObject.SetActive(false); continue; }
                hints[i].transform.localPosition = new Vector3(Mathf.Min(x + 100, right - 100), (bottom + top) * .5f, 0);
                hints[i].alpha = sourcePanel ? sourcePanel.alpha : 1;
            }
            return true;
        }
        public void Dispose()
        {
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        }
    }
}
