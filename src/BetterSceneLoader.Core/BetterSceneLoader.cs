using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KeelPlugins;
using KeelPlugins.Utils;
using UILib;

[assembly: System.Reflection.AssemblyVersion(BetterSceneLoader.BetterSceneLoader.Version)]

namespace BetterSceneLoader
{
    [BepInProcess(Constants.StudioProcessName)]
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID)]
    public class BetterSceneLoader : BaseUnityPlugin
    {
        public const string Version = "1.1.1." + BuildNumber.Version;
        public const string GUID = "keelhauled.bettersceneloader";
        public const string PluginName = "BetterSceneLoader";

        private const string CATEGORY_GENERAL = "General";
        private const string CATEGORY_UISIZE = "UI Size";

        public static ConfigEntry<int> ColumnAmount { get; set; }
        public static ConfigEntry<float> ScrollSensitivity { get; set; }
        public static ConfigEntry<bool> AutoClose { get; set; }
        public static ConfigEntry<float> AnchorLeft { get; set; }
        public static ConfigEntry<float> AnchorBottom { get; set; }
        public static ConfigEntry<float> AnchorRight { get; set; }
        public static ConfigEntry<float> AnchorTop { get; set; }
        public static ConfigEntry<float> UIMargin { get; set; }
        public static ConfigEntry<bool> ConfirmDelete { get; set; }
        public static ConfigEntry<SortBy> SceneSorting { get; set; }
        public static ConfigEntry<bool> ShowFolderTree { get; set; }
        public static ConfigEntry<float> FolderTreeWidth { get; set; }
        public static ConfigEntry<string> ExtraSceneFolders { get; set; }
        public static ConfigEntry<bool> SaveToCurrentFolder { get; set; }
        public static ConfigEntry<bool> ReplaceVanillaLoader { get; set; }
        public static ConfigEntry<bool> OpenOnStudioStart { get; set; }
        public static ConfigEntry<bool> DoubleClickLoad { get; set; }
        public static ConfigEntry<bool> DeleteKey { get; set; }

        private static ImageGrid sceneLoaderUI;
        public static BaseUnityPlugin plugin;

        private void Awake()
        {
            sceneLoaderUI = new SceneGrid();
            plugin = this;

            AutoClose = Config.Bind(CATEGORY_GENERAL, "Auto Close", true, new ConfigDescription("Automatically close scene window after loading", null, new ConfigurationManagerAttributes { Order = 3 }));
            ConfirmDelete = Config.Bind(CATEGORY_GENERAL, "Confirm Delete", true, new ConfigDescription("Ask Y/N before deleting a scene (Delete button and Del key). Deleted scenes go to the recycle bin.", null, new ConfigurationManagerAttributes { Order = 2 }));
            DoubleClickLoad = Config.Bind(CATEGORY_GENERAL, "Double Click To Load", true, new ConfigDescription("Double click a scene thumbnail to load it.", null, new ConfigurationManagerAttributes { Order = 2 }));
            DeleteKey = Config.Bind(CATEGORY_GENERAL, "Del Key Deletes Scene", true, new ConfigDescription("Pressing Del while the mouse is over a scene thumbnail deletes that scene.", null, new ConfigurationManagerAttributes { Order = 2 }));
            SceneSorting = Config.Bind(CATEGORY_GENERAL, "Default Sort Order", SortBy.DateDescending, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
            ExtraSceneFolders = Config.Bind(CATEGORY_GENERAL, "Extra Scene Folders", "", new ConfigDescription(
                "Additional scene folders shown in the folder tree and category list, e.g. from another game install.\n" +
                "Separate entries with |, optionally give them a name with Name=Path.\n" +
                "Example: KKS2=D:\\Games\\KKS2\\UserData\\Studio\\scene|E:\\Scenes", null, new ConfigurationManagerAttributes { Order = 0 }));
            SaveToCurrentFolder = Config.Bind(CATEGORY_GENERAL, "Save To Current Folder", true, new ConfigDescription(
                "Scenes saved with the Save button go into the folder currently open in the loader (including extra folders).\n" +
                "When off, scenes are always saved into UserData\\Studio\\scene like vanilla.", null, new ConfigurationManagerAttributes { Order = 0 }));
            ReplaceVanillaLoader = Config.Bind(CATEGORY_GENERAL, "Replace Vanilla Loader", true, new ConfigDescription(
                "The Load button in Studio's system menu opens this scene loader instead of the vanilla one.\n" +
                "Hold Shift while clicking Load to open the vanilla loader anyway.", null, new ConfigurationManagerAttributes { Order = 5 }));
            OpenOnStudioStart = Config.Bind(CATEGORY_GENERAL, "Open On Studio Start", false, new ConfigDescription(
                "Open this scene loader automatically after Studio starts.", null, new ConfigurationManagerAttributes { Order = 4 }));
            ColumnAmount = Config.Bind(CATEGORY_GENERAL, "Column Amount", 7, new ConfigDescription("", new AcceptableValueRange<int>(1, 12)));
            ScrollSensitivity = Config.Bind(CATEGORY_GENERAL, "Scroll Sensitivity", 3f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 10f)));
            AnchorLeft = Config.Bind(CATEGORY_UISIZE, "Left Anchor", 0f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));
            AnchorBottom = Config.Bind(CATEGORY_UISIZE, "Bottom Anchor", 0f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));
            AnchorRight = Config.Bind(CATEGORY_UISIZE, "Right Anchor", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));
            AnchorTop = Config.Bind(CATEGORY_UISIZE, "Top Anchor", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f)));
            ShowFolderTree = Config.Bind(CATEGORY_UISIZE, "Show Folder Tree", true, new ConfigDescription("Show a folder browser on the left side of the window", null, new ConfigurationManagerAttributes { Order = 3 }));
            FolderTreeWidth = Config.Bind(CATEGORY_UISIZE, "Folder Tree Width", 220f, new ConfigDescription("", new AcceptableValueRange<float>(100f, 600f), new ConfigurationManagerAttributes { Order = 2 }));
            UIMargin = Config.Bind(CATEGORY_UISIZE, "Margin", 60f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 200f), new ConfigurationManagerAttributes { Order = 1 }));

            ColumnAmount.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            ScrollSensitivity.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            AnchorLeft.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            AnchorBottom.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            AnchorRight.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            AnchorTop.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            UIMargin.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            ShowFolderTree.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            FolderTreeWidth.SettingChanged += (x, y) => sceneLoaderUI.UpdateWindow();
            ExtraSceneFolders.SettingChanged += (x, y) => sceneLoaderUI.RefreshFolders();

            Harmony.CreateAndPatchAll(typeof(Hooks));
            UIUtility.InitKOI(typeof(BetterSceneLoader).Assembly);
            Log.SetLogSource(Logger);
        }

        private class Hooks
        {
            [HarmonyPrefix, HarmonyPatch(typeof(StudioScene), "Start"), HarmonyWrapSafe]
            public static void StudioEntrypoint()
            {
                sceneLoaderUI.CreateUI("BetterSceneLoaderCanvas", 10, "Scenes");
            }

            // Studio's own Del shortcut deletes the selected workspace objects - block it while our window is open
            [HarmonyPrefix, HarmonyPatch(typeof(Studio.WorkspaceCtrl), nameof(Studio.WorkspaceCtrl.OnClickDelete)), HarmonyWrapSafe]
            public static bool WorkspaceDeletePrefix()
            {
                return !(sceneLoaderUI.IsVisible && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Delete));
            }

            // Esc in Studio opens the "exit Studio?" dialog - while our window is open Esc closes the window instead
            [HarmonyPrefix, HarmonyPatch(typeof(ExitDialog), nameof(ExitDialog.GameEnd)), HarmonyWrapSafe]
            public static bool ExitDialogPrefix()
            {
                if(!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
                    return true;
                return !(sceneLoaderUI.IsVisible || sceneLoaderUI.EscConsumedFrame == UnityEngine.Time.frameCount);
            }

            // Catches every way the vanilla loader gets opened: the Load button in the system menu
            // and the loader Studio opens by itself on startup.
            [HarmonyPrefix, HarmonyPatch(typeof(Manager.Scene), nameof(Manager.Scene.LoadReserve), typeof(Manager.Scene.Data), typeof(bool)), HarmonyWrapSafe]
            public static bool LoadReservePrefix(Manager.Scene.Data data)
            {
                if(!ReplaceVanillaLoader.Value || data == null || data.levelName != "StudioSceneLoad")
                    return true;
                if(UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift) || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                    return true;

                sceneLoaderUI.OpenWindow();
                return false;
            }
        }
    }
}
