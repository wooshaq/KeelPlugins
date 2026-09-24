using BepInEx;
using System.Collections;
using UnityEngine;
using KeelPlugins.Utils;
using KKAPI.Studio.UI.Toolbars;
using Studio;

namespace BetterSceneLoader
{
    public class SceneGrid : ImageGrid
    {
        private SimpleToolbarToggle toolbarToggle;
        private AddButtonCtrl addButtonCtrl;

        public SceneGrid() : base(
            defaultPath: BepInEx.Utility.CombinePaths(Paths.GameRootPath, "UserData", "Studio", "scene"),
            onSaveButtonClick: () => Studio.Studio.Instance.systemButtonCtrl.OnClickSave(),
            onLoadButtonClick: x => Studio.Studio.Instance.StartCoroutine(Studio.Studio.Instance.LoadSceneCoroutine(x)),
            onImportButtonClick: x => Studio.Studio.Instance.ImportScene(x))
        { }

        public override void ShowWindow(bool flag)
        {
            base.ShowWindow(flag);
            if(flag && (addButtonCtrl.select == 0 || addButtonCtrl.select == 1))
                addButtonCtrl.OnClick(addButtonCtrl.select);
        }

        public override void HideWindow()
        {
            toolbarToggle.Toggled.OnNext(false);
        }

        private bool pendingOpen;

        public override void OpenWindow()
        {
            if(toolbarToggle != null)
                toolbarToggle.Toggled.OnNext(true); // keeps the toolbar button state in sync
            else
                pendingOpen = true; // toolbar button not created yet (Studio startup)
        }

        public override void CreateUI(string name, int sortingOrder, string titleText)
        {
            base.CreateUI(name, sortingOrder, titleText);
            ThreadingHelper.Instance.StartCoroutine(AddToolbarButton());
            addButtonCtrl = GameObject.Find("StudioScene/Canvas Main Menu/01_Add").GetComponent<AddButtonCtrl>();
        }

        private IEnumerator AddToolbarButton()
        {
            yield return null;
            toolbarToggle = new SimpleToolbarToggle(BetterSceneLoader.PluginName, null, GetTex, false, BetterSceneLoader.plugin, ShowWindow);
            ToolbarManager.AddLeftToolbarControl(toolbarToggle);

            if(pendingOpen || BetterSceneLoader.OpenOnStudioStart.Value)
            {
                pendingOpen = false;
                // wait until Studio finished building its UI
                yield return null;
                yield return null;
                OpenWindow();
            }
            Texture2D GetTex() => PngAssist.ChangeTextureFromByte(Resource.GetResourceAsBytes(typeof(ImageGrid).Assembly, "Resources.pluginicon"));
        }
    }
}
