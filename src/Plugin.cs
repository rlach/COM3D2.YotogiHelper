using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using COM3D2API;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]
namespace COM3D2.YotogiHelper
{
    [BepInPlugin(Guid, "Yotogi Helper", Version)]
    [BepInDependency("deathweasel.com3d2.api", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "COM3D2.YotogiHelper", Version = "0.2.1";
        internal static Plugin Instance;
        private Harmony harmony;
        private YotogiStageSelectManager screen;
        private YotogiManager manager;
        private NativeView view;
        private Coroutine attaching;
        private bool dirty;
        private ConfigEntry<Vector2> flatPosition, vrPosition;
        private ConfigEntry<float> flatZoom, vrZoom;
        private ConfigEntry<KeyboardShortcut> shortcut;
        private void Awake()
        {
            Instance = this;
            flatPosition = Config.Bind("Window", "Flat position", new Vector2(330, 0), "Native UI coordinates.");
            vrPosition = Config.Bind("Window", "VR position", Vector2.zero, "Native UI coordinates.");
            flatZoom = Config.Bind("Window", "Flat zoom", .85f, "Scale of the window.");
            vrZoom = Config.Bind("Window", "VR zoom", .85f, "Scale of the window.");
            shortcut = Config.Bind("General", "Shortcut", new KeyboardShortcut(KeyCode.F9), "Show/hide in room selection; reopening centers the window.");
        }
        private void Start()
        {
            try
            {
                harmony = new Harmony(Guid); harmony.PatchAll(typeof(Plugin).Assembly);
                var icon = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var pixels = new Color[1024];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    pixels[y * 32 + x] = ((x>=6 && x<=25 && (y>=6 && y<=8 || y>=23 && y<=25)) || (y>=6 && y<=25 && (x>=6 && x<=8 || x>=23 && x<=25)) || (x>=14 && x<=17 && y>=8 && y<=18)) ? new Color(.35f,.39f,.42f,1) : Color.white;
                icon.SetPixels(pixels); icon.Apply(); byte[] png = icon.EncodeToPNG(); Destroy(icon);
                SystemShortcutAPI.AddButton("YotogiHelper", Toggle, "Yotogi Helper (room selection)", png);
                Logger.LogInfo("YotogiHelper " + Version + "; Unity " + Application.unityVersion);
            }
            catch (Exception ex) { Logger.LogError(ex); if (harmony != null) harmony.UnpatchSelf(); }
        }
        private void Open(YotogiStageSelectManager source)
        {
            Detach();
            manager = source.GetComponentInParent<YotogiManager>();
            // New-style/scripted sequences automatically skip this room picker.
            if (!manager || manager.is_new_yotogi_mode) return;
            screen = source;
            attaching = StartCoroutine(Attach());
        }
        private IEnumerator Attach()
        {
            yield return null;
            GameObject root = null;
            for (int i = 0; screen && i < 120; i++)
            {
                root = GameObject.Find("SystemUI Root");
                if (root && root.GetComponentInChildren<UICamera>()) break;
                root = null; yield return null;
            }
            attaching = null;
            if (!screen) yield break;
            try
            {
                if (!root) throw new InvalidOperationException("SystemUI Root / UICamera unavailable.");
                view = new NativeView(root, message => Logger.LogError(message));
                bool vr = GameMain.Instance.VRMode;
                var position = vr ? vrPosition : flatPosition;
                var zoom = vr ? vrZoom : flatZoom;
                view.Restore(position.Value, zoom.Value);
                view.LayoutChanged = delegate(Vector2 p, float z) { position.Value = p; zoom.Value = z; };

                dirty = true;
                Logger.LogInfo("Attached to room selection; VR=" + vr);
            }
            catch (Exception ex) { Logger.LogError(ex); Detach(); }
        }
        private void Update()
        {
            if (view == null) return;
            if (!screen || !manager || !screen.root_obj || !screen.root_obj.activeInHierarchy)
            { Detach(); return; }
            if (UIInput.selection == null && shortcut.Value.IsDown()) Toggle();
            if (!dirty) return;
            dirty = false;
            try { view.Display(GameReader.Read(manager, YotogiStageSelectManager.SelectedStage)); }
            catch (Exception ex) { Logger.LogError(ex); view.Error("Could not read room data. See BepInEx log."); }
        }
        private void Toggle()
        {
            if (view == null || !screen) return;
            bool show = !view.Visible;
            if (show) { view.Center(); dirty = true; }
            view.Show(show);
        }
        private void Detach()
        {
            if (attaching != null) { StopCoroutine(attaching); attaching = null; }
            if (view != null) { view.Dispose(); view = null; }
            screen = null; manager = null; dirty = false;
        }
        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            Detach(); Instance = null;
        }
        [HarmonyPatch(typeof(YotogiStageSelectManager), "OnCall")]
        private static class Entered
        {
            private static void Postfix(YotogiStageSelectManager __instance)
            { if (Instance) { try { Instance.Open(__instance); } catch (Exception ex) { Instance.Logger.LogError(ex); } } }
        }
        [HarmonyPatch(typeof(YotogiStageSelectManager), "SelectStage")]
        private static class Selected
        {
            private static void Postfix() { if (Instance) Instance.dirty = true; }
        }
        [HarmonyPatch(typeof(YotogiStageSelectManager), "OnFinish")]
        private static class Left
        {
            private static void Postfix(YotogiStageSelectManager __instance)
            { if (Instance && Instance.screen == __instance) Instance.Detach(); }
        }
    }
}
