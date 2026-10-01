using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.YotogiHelper
{
    public sealed class WindowDrag : MonoBehaviour
    {
        public Transform Target;
        public Action Changed;
        private void OnDrag(Vector2 delta)
        {
            if (!Target || !UICamera.currentCamera) return;
            var camera = UICamera.currentCamera;
            var point = camera.WorldToScreenPoint(Target.position);
            Target.position += camera.ScreenToWorldPoint(point + new Vector3(delta.x, delta.y, 0)) - camera.ScreenToWorldPoint(point);
        }
        private void OnPress(bool pressed) { if (!pressed && Changed != null) Changed(); }
        private void OnDragEnd() { if (Changed != null) Changed(); }
    }
    public sealed class PageScroll : MonoBehaviour
    {
        public Action<int> Change;
        private void OnScroll(float delta) { if (Change != null && delta != 0) Change(delta > 0 ? -1 : 1); }
    }
    internal sealed class NativeView : IDisposable
    {
        public Action<Vector2, float> LayoutChanged;

        public readonly GameObject Root;
        private readonly UIAtlas atlas;
        private readonly Font font;
        private readonly Action<string> report;
        private UILabel summary, pageLabel, groupCounts;
        private readonly List<GameObject> choices = new List<GameObject>();
        private readonly List<UILabel> names = new List<UILabel>(), stars = new List<UILabel>();
        private readonly List<GameObject> rowRoots = new List<GameObject>();
        private readonly List<int> modes = new List<int>(), categories = new List<int>();
        private RoomSnapshot snapshot;
        private int mode, category, page;
        private float scale = .85f;
        public bool Visible { get { return Root && Root.activeSelf; } }
        public NativeView(GameObject parent, Action<string> report)
        {
            this.report = report;
            atlas = Resources.Load<UIAtlas>("CommonUI/Atlas/AtlasCommon");
            font = Resources.Load<Font>("font/notosanscjkjp-hinted/notosanscjkjp-demilight");
            if (!atlas || !font) throw new InvalidOperationException("Native UI atlas/font unavailable.");
            Root = Child(parent, "YotogiHelper");
            try
            {
                var sample = parent.GetComponentInChildren<UIWidget>();
                Root.layer = sample ? sample.gameObject.layer : parent.layer;
                if (!UICamera.FindCameraForLayer(Root.layer)) throw new InvalidOperationException("No native UI camera.");
                Root.AddComponent<UIPanel>().depth = NGUITools.CalculateNextDepth(parent) + 10;
                Build();
            }
            catch { UnityEngine.Object.Destroy(Root); throw; }
        }
        private GameObject Child(GameObject parent, string name)
        {
            var obj = new GameObject(name); obj.layer = parent.layer;
            obj.transform.SetParent(parent.transform, false); return obj;
        }
        private UISprite Box(GameObject parent, string name, float x, float y, int w, int h, int depth, Color color, bool collider)
        {
            var obj = Child(parent, name);
            obj.transform.localPosition = new Vector3(x, y, 0);
            var sprite = obj.AddComponent<UISprite>(); sprite.atlas = atlas;
            sprite.spriteName = "cm3d2_common_plate_white"; sprite.type = UIBasicSprite.Type.Sliced;
            sprite.width = w; sprite.height = h; sprite.depth = depth; sprite.color = color;
            if (collider) NGUITools.AddWidgetCollider(obj);
            return sprite;
        }
        private UILabel Label(GameObject parent, string text, float x, float y, int width, int size = 18, int height = 36)
        {
            var label = Child(parent, "Label").AddComponent<UILabel>();
            label.trueTypeFont = font; label.fontSize = size; label.text = text;
            label.width = width; label.height = height; label.depth = 4;
            label.supportEncoding = false; label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.transform.localPosition = new Vector3(x, y, 0); return label;
        }
        private void Safe(Action action)
        {
            try { action(); } catch (Exception ex) { report(ex.ToString()); }
        }
        private UILabel Button(string text, float x, float y, int width, Action action)
        {
            var sprite = Box(Root, text, x, y, width, 34, 2, new Color(.16f, .22f, .29f), true);
            var button = sprite.gameObject.AddComponent<UIButton>();
            button.tweenTarget = sprite.gameObject; button.defaultColor = sprite.color;
            button.hover = new Color(.3f, .42f, .52f); button.pressed = new Color(.1f, .16f, .2f);
            EventDelegate.Add(button.onClick, delegate { Safe(action); });
            return Label(sprite.gameObject, text, 0, 0, width - 10);
        }
        private void Build()
        {
            Box(Root, "Background", 0, 0, 1040, 644, 0, new Color(.045f, .06f, .08f), true);
            var title = Box(Root, "Drag", 0, 299, 1030, 40, 1, new Color(.12f, .2f, .27f), true);
            Label(title.gameObject, "Yotogi Helper", -335, 0, 280, 23);
            var drag = title.gameObject.AddComponent<WindowDrag>(); drag.Target = Root.transform; drag.Changed = Remember;
            Button("Center", 344, 299, 70, Center);
            Button("-", 401, 299, 34, () => Zoom(-.1f));
            Button("+", 441, 299, 34, () => Zoom(.1f));
            Button("X", 486, 299, 40, () => Show(false));
            summary = Label(Root, "", 0, 246, 1000, 27, 42);
            groupCounts = Label(Root, "", 0, 108, 1000, 22, 34);
            for (int i = 0; i < 8; i++)
            {
                var row = Child(Root, "Skill row"); row.transform.localPosition = new Vector3(0, 60 - i * 38, 0);
                stars.Add(Label(row, "", -460, 0, 84, 20, 30));
                var name = Label(row, "", 50, 0, 900, 20, 34);
                name.alignment = NGUIText.Alignment.Left; name.maxLineCount = 1;
                names.Add(name); rowRoots.Add(row);
            }
            Button("< Previous", -207, -263, 144, () => ChangePage(-1));
            pageLabel = Label(Root, "", 0, -263, 160, 16);
            Button("Next >", 207, -263, 144, () => ChangePage(1));

            Root.AddComponent<PageScroll>().Change = ChangePage;
        }
        public void Display(RoomSnapshot data)
        {
            snapshot = data;
            modes.Clear();
            foreach (int key in data.Modes.Keys) modes.Add(key);
            modes.Sort();
            if (!modes.Contains(mode) || !data.Rows.Exists(row => row.Mode == mode)) mode = data.Rows.Count == 0 ? 0 : data.Rows[0].Mode;
            RebuildCategories(); page = 0;
            var counts = Counts.From(data.Rows);
            summary.text = Summary(counts);
            summary.color = data.RoomPlayable && counts.Selectable > 0 ? Color.white : new Color(1, .7f, .4f);
            Render();
        }
        private void RebuildCategories()
        {
            categories.Clear();
            if (snapshot != null) foreach (int key in snapshot.Categories.Keys) categories.Add(key);
            categories.Sort();
            if (snapshot != null && (!categories.Contains(category) || !snapshot.Rows.Exists(row => row.Mode == mode && row.Category == category)))
            { var first = snapshot.Rows.Find(row => row.Mode == mode); category = first == null ? 0 : first.Category; }
        }
        private int Next(List<int> options, int current, int delta)
        {
            if (options.Count == 0) return 0;
            return options[(Math.Max(0, options.IndexOf(current)) + delta + options.Count) % options.Count];
        }
        private void ChangeMode(int delta) { mode = Next(modes, mode, delta); RebuildCategories(); page = 0; Render(); }
        private void ChangeCategory(int delta) { category = Next(categories, category, delta); page = 0; Render(); }
        private void ChangePage(int delta) { page += delta; Render(); }
        private void Render()
        {
            if (snapshot == null) return;
            var rows = snapshot.Filter(mode, category);
            page = RoomSnapshot.ClampPage(page, rows.Count, 8);
            RenderChoices();
            var counts = Counts.From(rows);
            groupCounts.text = Summary(counts);
            pageLabel.text = rows.Count == 0 ? "0 / 0" : (page + 1) + " / " + ((rows.Count + 7) / 8);
            for (int i = 0; i < names.Count; i++)
            {
                int index = page * 8 + i;
                rowRoots[i].SetActive(index < rows.Count);
                if (index >= rows.Count) continue;
                var row = rows[index];
                stars[i].text = new string('★', row.Stars) + new string('☆', 3 - row.Stars);
                stars[i].color = row.Mastered ? new Color(1, .82f, .3f) : Color.white;
                names[i].text = row.Name + (row.Unlocked && !row.EnoughMaids ? " (needs " + row.RequiredMaids + " maids)" : "");
                names[i].color = row.Unlocked ? Color.white : Color.gray;
            }
        }
        private static string Summary(Counts count) { return "Available " + count.Unlocked + "/" + count.Total + ". Maxed: " + count.Mastered; }
        private void ClearChoices() { foreach (var obj in choices) { obj.SetActive(false); UnityEngine.Object.Destroy(obj); } choices.Clear(); }
        private void RenderChoices()
        {
            ClearChoices();
            DrawChoices(modes, true, 196);
            DrawChoices(categories, false, 151);
        }
        private void DrawChoices(List<int> options, bool isMode, float y)
        {
            if (options.Count == 0) return;
            int width = 1012 / options.Count;
            for (int i=0; i<options.Count; i++)
            {
                int key=options[i];
                var rows=snapshot.Rows.FindAll(row => isMode ? row.Mode == key : row.Mode == mode && row.Category == key);
                int count=Counts.From(rows).Total;
                string name=(isMode ? snapshot.Modes : snapshot.Categories)[key];
                bool selected=(isMode ? mode : category)==key;
                var label=Button((selected ? "• " : "") + name + "(" + count + ")", -506+width*(i+.5f), y, width-5, delegate {
                    if (isMode) { mode=key; RebuildCategories(); } else category=key;
                    page=0; Render();
                });
                label.fontSize=isMode ? 20 : 17;
                label.color=count==0 ? Color.gray : Color.white;
                var obj=label.transform.parent.gameObject;
                obj.GetComponent<UIButton>().isEnabled=count>0;
                choices.Add(obj);
            }
        }
        public void Error(string message)
        {
            snapshot = null; summary.text = message;
            groupCounts.text = pageLabel.text = "";
            ClearChoices();
            foreach (var row in rowRoots) row.SetActive(false);
        }
        public void Restore(Vector2 position, float zoom)
        {
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y)) position = Vector2.zero;
            scale = float.IsNaN(zoom) || float.IsInfinity(zoom) ? .85f : Mathf.Clamp(zoom, .5f, 1.5f);
            Root.transform.localPosition = new Vector3(position.x, position.y, 0); Root.transform.localScale = Vector3.one * scale;
        }
        private void Remember() { if (Root && LayoutChanged != null) Safe(() => LayoutChanged(new Vector2(Root.transform.localPosition.x, Root.transform.localPosition.y), scale)); }
        private void Zoom(float amount) { scale = Mathf.Clamp(scale + amount, .5f, 1.5f); Root.transform.localScale = Vector3.one * scale; Remember(); }
        public void Center() { Root.transform.localPosition = Vector3.zero; Remember(); }
        public void Show(bool show) { Remember(); Root.SetActive(show); }
        public void Dispose() { Remember(); if (Root) { Root.SetActive(false); UnityEngine.Object.Destroy(Root); } }
    }
}
