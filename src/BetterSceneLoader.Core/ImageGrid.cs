using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UILib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using KeelPlugins.Utils;
using UnityEngine.Networking;
using KKAPI.Utilities;
using UniRx.Triggers;
using UniRx;

namespace BetterSceneLoader
{
    public class ImageGrid
    {
        private readonly float buttonSize = 10f;
        private readonly float marginSize = 5f;
        private readonly float headerSize = 20f;
        private readonly float UIScale = 1.0f;
        private readonly float dropdownWidth = 250f;

        private readonly Color dragColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        private readonly Color backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1f);
        private readonly Color outlineColor = new Color(0f, 0f, 0f, 1f);

        private Canvas UISystem;
        private Image mainPanel;
        private ScrollRect imagelist;
        private Image optionspanel;
        private Image confirmpanel;
        private Button yesbutton;
        private Button nobutton;
        private Text nametext;
        private Dropdown category;
        private Image infopanel;
        private Text infotext;
        private Dropdown sorting;
        private FolderTreeView folderTree;

        private readonly Dictionary<string, CategoryData> sceneCache = new Dictionary<string, CategoryData>();
        private Button currentButton;
        private Button hoveredButton;

        public bool IsVisible => UISystem != null && UISystem.gameObject.activeSelf;

        /// <summary>Frame in which Esc was consumed by this window (it may already be closed when Studio checks Esc).</summary>
        public int EscConsumedFrame { get; private set; } = -1;

        private class UpdateRelay : MonoBehaviour
        {
            public Action OnUpdate;
            private void Update() => OnUpdate?.Invoke();
        }
        private readonly string defaultPath;
        private string currentPath;
        private string currentCategoryFolder;
        private readonly Dictionary<string, string> CategoryFolders = new Dictionary<string, string>();

        public readonly UnityAction OnSaveButtonClick;
        public readonly UnityAction<string> OnLoadButtonClick;
        public readonly UnityAction<string> OnImportButtonClick;

        private class CategoryData
        {
            public Image Container;
            public SortBy SortBy;
        }

        public ImageGrid(string defaultPath, UnityAction onSaveButtonClick,
                         UnityAction<string> onLoadButtonClick, UnityAction<string> onImportButtonClick)
        {
            this.defaultPath = defaultPath;
            currentCategoryFolder = defaultPath;
            OnSaveButtonClick = onSaveButtonClick;
            OnLoadButtonClick = onLoadButtonClick;
            OnImportButtonClick = onImportButtonClick;
        }

        public virtual void ShowWindow(bool flag)
        {
            UISystem.gameObject.SetActive(flag);
            if(category != null)
                category.template.SetRect(0f, 1f, 0f, 0f, 0f, headerSize - marginSize, dropdownWidth, mainPanel.rectTransform.rect.height / 2);
        }

        public virtual void HideWindow()
        {
            ShowWindow(false);
        }

        public virtual void OpenWindow()
        {
            ShowWindow(true);
        }

        public void UpdateWindow()
        {
            foreach(var scene in sceneCache.Values)
            {
                var gridlayout = scene.Container.gameObject.GetComponent<AutoGridLayout>();
                if(gridlayout != null)
                {
                    gridlayout.m_Column = BetterSceneLoader.ColumnAmount.Value;
                    gridlayout.CalculateLayoutInputHorizontal();
                }
            }

            if(imagelist != null)
            {
                imagelist.scrollSensitivity = Mathf.Lerp(30f, 300f, BetterSceneLoader.ScrollSensitivity.Value / 10f);
            }

            if(imagelist != null)
            {
                var showTree = BetterSceneLoader.ShowFolderTree.Value;
                var treeWidth = BetterSceneLoader.FolderTreeWidth.Value;
                folderTree?.SetVisible(showTree);
                if(showTree && folderTree != null)
                    folderTree.RectTransform.SetRect(0f, 0f, 0f, 1f, marginSize, marginSize, marginSize + treeWidth, -headerSize - marginSize / 2f);
                var listLeft = showTree ? marginSize * 2f + treeWidth : marginSize;
                imagelist.transform.SetRect(0f, 0f, 1f, 1f, listLeft, marginSize, -marginSize, -headerSize - marginSize / 2f);
            }

            if(mainPanel)
            {
                mainPanel.transform.SetRect(BetterSceneLoader.AnchorLeft.Value, BetterSceneLoader.AnchorBottom.Value,
                                            BetterSceneLoader.AnchorRight.Value, BetterSceneLoader.AnchorTop.Value,
                                            BetterSceneLoader.UIMargin.Value, BetterSceneLoader.UIMargin.Value,
                                            -BetterSceneLoader.UIMargin.Value, -BetterSceneLoader.UIMargin.Value);
            }
        }

        public virtual void CreateUI(string name, int sortingOrder, string titleText)
        {
            UISystem = UIUtility.CreateNewUISystem(name);
            UISystem.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f / UIScale, 1080f / UIScale);
            UISystem.sortingOrder = sortingOrder;
            UISystem.gameObject.AddComponent<UpdateRelay>().OnUpdate = OnUpdate;
            ShowWindow(false);

            mainPanel = UIUtility.CreatePanel("Panel", UISystem.transform);
            mainPanel.color = backgroundColor;
            UIUtility.AddOutlineToObject(mainPanel.transform, outlineColor);

            var drag = UIUtility.CreatePanel("Draggable", mainPanel.transform);
            drag.transform.SetRect(0f, 1f, 1f, 1f, 0f, -headerSize);
            drag.color = dragColor;
            UIUtility.MakeObjectDraggable(drag.rectTransform, mainPanel.rectTransform);

            nametext = UIUtility.CreateText("Nametext", drag.transform, titleText);
            nametext.transform.SetRect(0f, 0f, 1f, 1f, 340f, 0f, -buttonSize * 2f);
            nametext.alignment = TextAnchor.MiddleCenter;

            var close = UIUtility.CreateButton("CloseButton", drag.transform, "");
            close.transform.SetRect(1f, 0f, 1f, 1f, -buttonSize * 2f);
            close.onClick.AddListener(HideWindow);

            var x1 = UIUtility.CreatePanel("x1", close.transform);
            x1.transform.SetRect(0f, 0f, 1f, 1f, 8f, 0f, -8f);
            x1.rectTransform.eulerAngles = new Vector3(0f, 0f, 45f);
            x1.color = new Color(0f, 0f, 0f, 1f);
            var x2 = UIUtility.CreatePanel("x2", close.transform);
            x2.transform.SetRect(0f, 0f, 1f, 1f, 8f, 0f, -8f);
            x2.rectTransform.eulerAngles = new Vector3(0f, 0f, -45f);
            x2.color = new Color(0f, 0f, 0f, 1f);

            var curPos = 0f;
            category = UIUtility.CreateDropdown("CategoryDropdown", drag.transform, "Categories");
            category.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=dropdownWidth);
            category.captionText.transform.SetRect(0f, 0f, 1f, 1f, 0f, 2f, -15f, -2f);
            category.captionText.alignment = TextAnchor.MiddleCenter;
            category.template.GetComponent<ScrollRect>().scrollSensitivity = 40f;
            category.options = GetCategories();
            category.onValueChanged.AddListener(x =>
            {
                currentCategoryFolder = CategoryFolders[category.options[x].text];
                imagelist.content.GetComponentInChildren<Image>().gameObject.SetActive(false);
                imagelist.content.anchoredPosition = new Vector2(0f, 0f);
                PopulateGrid();
                folderTree?.SetSelected(currentCategoryFolder);
            });
            DropdownAutoScroll.Setup(category);
            DropdownFilter.AddFilterUI(category, "BetterSceneLoaderDropdown");

            sorting = UIUtility.CreateDropdown("SortDropdown", drag.transform, "Sort");
            sorting.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=150f);
            sorting.captionText.transform.SetRect(0f, 0f, 1f, 1f, 0f, 2f, -15f, -2f);
            sorting.captionText.alignment = TextAnchor.MiddleCenter;
            sorting.options = Enum.GetNames(typeof(SortBy)).Select(x => new Dropdown.OptionData(x)).ToList();
            sorting.onValueChanged.AddListener(x =>
            {
                sceneCache[currentCategoryFolder].SortBy = (SortBy)x;
                var container = imagelist.content.GetComponentInChildren<Image>();
                if(container && container.gameObject.activeSelf) // only reload when not changing category
                    ReloadImages();
            });

            var refresh = UIUtility.CreateButton("RefreshButton", drag.transform, "Refresh");
            refresh.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=80f);
            refresh.onClick.AddListener(ReloadImages);

            var save = UIUtility.CreateButton("SaveButton", drag.transform, "Save");
            save.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=80f);
            save.onClick.AddListener(OnSave);

            var folder = UIUtility.CreateButton("FolderButton", drag.transform, "Folder");
            folder.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=80f);
            folder.onClick.AddListener(() => Application.OpenURL($"file:///{EncodePath(currentCategoryFolder)}"));

            var autoCloseToggle = UIUtility.CreateToggle("AutoCloseToggle", drag.transform, "Auto Close");
            autoCloseToggle.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=80f);
            autoCloseToggle.GetComponentInChildren<Text>().color = new Color(1f, 1f, 1f, 1f);
            autoCloseToggle.onValueChanged.AddListener(x => BetterSceneLoader.AutoClose.Value = x);

            var loadingPanel = UIUtility.CreatePanel("LoadingIconPanel", drag.transform);
            loadingPanel.transform.SetRect(0f, 0f, 0f, 1f, curPos, 0f, curPos+=headerSize);
            loadingPanel.color = new Color(0f, 0f, 0f, 0f);
            var loadingIcon = UIUtility.CreatePanel("LoadingIcon", loadingPanel.transform);
            loadingIcon.transform.SetRect(0.1f, 0.1f, 0.9f, 0.9f);
            var loadiconTex = PngAssist.ChangeTextureFromByte(Resource.GetResourceAsBytes(typeof(ImageGrid).Assembly, "Resources.loadicon"));
            loadingIcon.sprite = Sprite.Create(loadiconTex, new Rect(0, 0, loadiconTex.width, loadiconTex.height), new Vector2(0.5f, 0.5f));
            LoadingIcon.Init(loadingPanel.gameObject, loadingIcon, -5f);

            imagelist = UIUtility.CreateScrollView("Imagelist", mainPanel.transform);
            imagelist.transform.SetRect(0f, 0f, 1f, 1f, marginSize, marginSize, -marginSize, -headerSize - marginSize / 2f);
            imagelist.gameObject.AddComponent<Mask>();
            imagelist.GetComponent<Image>().color = FolderTreeView.PanelColor;
            imagelist.content.gameObject.AddComponent<VerticalLayoutGroup>();
            imagelist.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollbarStyle.Apply(imagelist, marginSize);
            imagelist.movementType = ScrollRect.MovementType.Clamped;

            folderTree = new FolderTreeView(GetRoots, SelectFolder);
            folderTree.CreateUI(mainPanel.transform);
            ScrollbarStyle.CopyLook(imagelist.verticalScrollbar, folderTree.Scroll.verticalScrollbar);

            optionspanel = UIUtility.CreatePanel("ButtonPanel", imagelist.transform);
            optionspanel.gameObject.SetActive(false);

            confirmpanel = UIUtility.CreatePanel("ConfirmPanel", imagelist.transform);
            confirmpanel.gameObject.SetActive(false);

            yesbutton = UIUtility.CreateButton("YesButton", confirmpanel.transform, "Y");
            yesbutton.transform.SetRect(0f, 0f, 0.5f, 1f);
            yesbutton.onClick.AddListener(() =>
            {
                confirmpanel.gameObject.SetActive(false);
                DeleteCurrent();
            });

            nobutton = UIUtility.CreateButton("NoButton", confirmpanel.transform, "N");
            nobutton.transform.SetRect(0.5f, 0f, 1f, 1f);
            nobutton.onClick.AddListener(() => confirmpanel.gameObject.SetActive(false));

            var loadbutton = UIUtility.CreateButton("LoadButton", optionspanel.transform, "Load");
            loadbutton.transform.SetRect(0f, 0f, 0.3f, 1f);
            loadbutton.onClick.AddListener(LoadCurrent);

            var importbutton = UIUtility.CreateButton("ImportButton", optionspanel.transform, "Import");
            importbutton.transform.SetRect(0.35f, 0f, 0.65f, 1f);
            importbutton.onClick.AddListener(() =>
            {
                OnImportButtonClick?.Invoke(currentPath);
                confirmpanel.gameObject.SetActive(false);
                optionspanel.gameObject.SetActive(false);
            });

            var deletebutton = UIUtility.CreateButton("DeleteButton", optionspanel.transform, "Delete");
            deletebutton.transform.SetRect(0.7f, 0f, 1f, 1f);
            deletebutton.onClick.AddListener(RequestDelete);

            infopanel = UIUtility.CreatePanel("InfoPanel", imagelist.transform);
            infopanel.gameObject.SetActive(false);

            infotext = UIUtility.CreateText("InfoText", infopanel.transform, "INFO");
            infotext.transform.SetRect(0f, 0f, 1f, 1f);
            infotext.alignment = TextAnchor.MiddleCenter;
            UIUtility.AddOutlineToObject(infotext.transform);

            UpdateWindow();
            PopulateGrid();
        }

        private void LoadCurrent()
        {
            if(string.IsNullOrEmpty(currentPath))
                return;

            confirmpanel.gameObject.SetActive(false);
            OnLoadButtonClick?.Invoke(currentPath);
            if(BetterSceneLoader.AutoClose.Value)
                HideWindow();
        }

        private void RequestDelete()
        {
            if(currentButton == null || string.IsNullOrEmpty(currentPath))
                return;

            if(BetterSceneLoader.ConfirmDelete.Value)
                confirmpanel.gameObject.SetActive(true);
            else
                DeleteCurrent();
        }

        private void DeleteCurrent()
        {
            if(currentButton == null || string.IsNullOrEmpty(currentPath))
                return;

            RecycleBinUtil.MoveToRecycleBin(currentPath);
            currentButton.gameObject.SetActive(false);
            if(hoveredButton == currentButton)
                hoveredButton = null;
        }

        private void OnUpdate()
        {
            // keyboard answer for the Y/N confirm panel: Del/Enter/Y = yes, N/Esc = no
            if(Input.GetKeyDown(KeyCode.Escape))
                EscConsumedFrame = Time.frameCount;

            if(confirmpanel != null && confirmpanel.gameObject.activeInHierarchy && !IsTypingInInputField())
            {
                if(Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Y))
                {
                    confirmpanel.gameObject.SetActive(false);
                    DeleteCurrent();
                    return;
                }
                if(Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape))
                {
                    confirmpanel.gameObject.SetActive(false);
                    return;
                }
            }

            // Esc closes the window (Studio's exit dialog is blocked by a hook while we're open)
            if(Input.GetKeyDown(KeyCode.Escape) && !IsTypingInInputField())
            {
                HideWindow();
                return;
            }

            if(!BetterSceneLoader.DeleteKey.Value || hoveredButton == null || !hoveredButton.gameObject.activeInHierarchy)
                return;
            if(!Input.GetKeyDown(KeyCode.Delete) || IsTypingInInputField())
                return;

            currentButton = hoveredButton;
            RequestDelete();
        }

        private static bool IsTypingInInputField()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if(selected == null)
                return false;
            var field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        private List<SceneRoot> GetRoots()
        {
            return SceneRoots.Get(defaultPath, BetterSceneLoader.ExtraSceneFolders.Value);
        }

        private List<Dropdown.OptionData> GetCategories()
        {
            if(!Directory.Exists(defaultPath))
                Directory.CreateDirectory(defaultPath);

            CategoryFolders.Clear();
            var result = new List<Dropdown.OptionData>();

            foreach(var root in GetRoots())
            {
                List<string> folders;
                try
                {
                    folders = Directory.GetDirectories(root.Path, "*", SearchOption.AllDirectories).OrderBy(x => x).ToList();
                }
                catch(Exception ex)
                {
                    Log.Warning($"Failed to read scene folder \"{root.Path}\": {ex.Message}");
                    folders = new List<string>();
                }
                folders.Insert(0, root.Path);

                var prefix = root.IsDefault ? "" : $"[{root.Label}]";
                foreach(var x in folders)
                {
                    string catname;
                    if(x == root.Path)
                        catname = root.IsDefault ? "/" : prefix;
                    else
                        catname = (root.IsDefault ? "" : prefix + "/") + x.Remove(0, root.Path.Length + 1).Replace("\\", "/");

                    CategoryFolders[catname] = x;
                    result.Add(new Dropdown.OptionData(catname));
                }
            }

            return result;
        }

        private void OnSave()
        {
            // Studio always saves into UserData/Studio/scene, remember what was there before
            HashSet<string> before;
            try
            {
                before = new HashSet<string>(Directory.GetFiles(defaultPath, "*.png"), StringComparer.OrdinalIgnoreCase);
            }
            catch(Exception)
            {
                before = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            OnSaveButtonClick?.Invoke();

            FileInfo fileInfo;
            try
            {
                fileInfo = new DirectoryInfo(defaultPath).GetFiles("*.png")
                                                         .Where(f => !before.Contains(f.FullName))
                                                         .OrderByDescending(f => f.LastWriteTime)
                                                         .FirstOrDefault();
            }
            catch(Exception ex)
            {
                Log.Warning($"Could not find saved scene: {ex.Message}");
                return;
            }

            if(fileInfo == null)
                return;

            var target = currentCategoryFolder;
            if(!string.Equals(target, defaultPath, StringComparison.OrdinalIgnoreCase))
            {
                if(!BetterSceneLoader.SaveToCurrentFolder.Value)
                    return;

                try
                {
                    var destination = Path.Combine(target, fileInfo.Name);
                    File.Move(fileInfo.FullName, destination);
                    fileInfo = new FileInfo(destination);
                    Log.Info($"Scene saved to {destination}");
                }
                catch(Exception ex)
                {
                    Log.Message($"Could not move saved scene to \"{target}\", it stays in the default folder: {ex.Message}");
                    return;
                }
            }

            var gridContainer = imagelist.content.GetComponentInChildren<Image>();
            if(gridContainer == null)
                return;
            var button = CreateSceneButton(gridContainer.transform, PngAssist.LoadTexture(fileInfo.FullName), fileInfo);
            button.transform.SetAsFirstSibling();
        }

        /// <summary>Rescan all scene folders (used when the folder settings change).</summary>
        public void RefreshFolders()
        {
            if(category == null || imagelist == null)
                return;
            ReloadImages();
        }

        private void SelectFolder(string path)
        {
            if(string.Equals(path, currentCategoryFolder, StringComparison.OrdinalIgnoreCase))
                return;

            var index = FirstIndexMatch(category.options, x =>
                CategoryFolders.TryGetValue(x.text, out var folder) && string.Equals(folder, path, StringComparison.OrdinalIgnoreCase));

            if(index == -1)
            {
                // folder created after the last refresh
                ReloadImages();
                index = FirstIndexMatch(category.options, x =>
                    CategoryFolders.TryGetValue(x.text, out var folder) && string.Equals(folder, path, StringComparison.OrdinalIgnoreCase));
                if(index == -1)
                    return;
            }

            category.value = index; // fires onValueChanged which switches the grid
            category.RefreshShownValue();
        }

        private void ReloadImages()
        {
            optionspanel.transform.SetParent(imagelist.transform);
            confirmpanel.transform.SetParent(imagelist.transform);
            infopanel.transform.SetParent(imagelist.transform);
            optionspanel.gameObject.SetActive(false);
            confirmpanel.gameObject.SetActive(false);
            infopanel.gameObject.SetActive(false);

            folderTree?.Rebuild();

            var oldIndex = category.value;
            var oldText = oldIndex >= 0 && oldIndex < category.options.Count ? category.options[oldIndex].text : null;
            var newCats = GetCategories();
            var newIndex = FirstIndexMatch(newCats, x => x.text == oldText);
            if(newIndex == -1)
                newIndex = 0;
            category.options = newCats;
            if(oldIndex != newIndex)
            {
                category.value = newIndex; // fires onValueChanged
                category.RefreshShownValue();
            }
            else
            {
                var container = imagelist.content.GetComponentInChildren<Image>();
                if(container != null)
                    GameObject.Destroy(container.gameObject);
                imagelist.content.anchoredPosition = new Vector2(0f, 0f);
                currentCategoryFolder = CategoryFolders[newCats[newIndex].text];
                category.RefreshShownValue();
                PopulateGrid(true);
                folderTree?.SetSelected(currentCategoryFolder);
            }
        }

        private void PopulateGrid(bool forceUpdate = false)
        {
            var sortBy = BetterSceneLoader.SceneSorting.Value;
            
            if(forceUpdate)
            {
                if(sceneCache.TryGetValue(currentCategoryFolder, out var temp))
                    sortBy = temp.SortBy;
                sceneCache.Remove(currentCategoryFolder);
            }

            if(sceneCache.TryGetValue(currentCategoryFolder, out var catData))
            {
                sorting.value = (int)catData.SortBy;
                catData.Container.gameObject.SetActive(true);
            }
            else
            {
                var dirInfo = new DirectoryInfo(currentCategoryFolder);
                var scenefiles = dirInfo.GetFiles("*.png").ToList();
                switch(sortBy)
                {
                    case SortBy.DateAscending:
                        scenefiles = scenefiles.OrderBy(x => x.LastWriteTime).ToList();
                        break;
                    case SortBy.DateDescending:
                        scenefiles = scenefiles.OrderByDescending(x => x.LastWriteTime).ToList();
                        break;
                    case SortBy.SizeAscending:
                        scenefiles = scenefiles.OrderBy(x => x.Length).ToList();
                        break;
                    case SortBy.SizeDescending:
                        scenefiles = scenefiles.OrderByDescending(x => x.Length).ToList();
                        break;
                }

                var container = UIUtility.CreatePanel("GridContainer", imagelist.content.transform);
                container.transform.SetRect(0f, 0f, 1f, 1f);
                container.color = new Color(0f, 0f, 0f, 0f);
                container.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var gridlayout = container.gameObject.AddComponent<AutoGridLayout>();
                gridlayout.spacing = new Vector2(marginSize, marginSize);
                gridlayout.m_IsColumn = true;
                gridlayout.m_Column = BetterSceneLoader.ColumnAmount.Value;

                ThreadingHelper.Instance.StartCoroutine(LoadButtonsAsync(container.transform, scenefiles));
                sceneCache.Add(currentCategoryFolder, new CategoryData{ Container = container, SortBy = sortBy });
                sorting.value = (int)sortBy;
            }
        }

        private IEnumerator LoadButtonsAsync(Transform parent, List<FileInfo> scenefiles)
        {
            LoadingIcon.loadingCount++;
            foreach(var scene in scenefiles)
            {
                var uri = "file:///" + EncodePath(scene.FullName);
#if KKS
                using(var uwr = UnityWebRequestTexture.GetTexture(uri, true))
                {
                    yield return uwr.SendWebRequest();

                    if(uwr.isNetworkError || uwr.isHttpError)
                        throw new Exception(uwr.error);

                    var tex = DownloadHandlerTexture.GetContent(uwr);
                    CreateSceneButton(parent, tex, scene);
                }
#else
                using(var www = new WWW(uri))
                {
                    yield return www;

                    if(!string.IsNullOrEmpty(www.error))
                        throw new Exception(www.error);

                    var tex = PngAssist.ChangeTextureFromByte(www.bytes);
                    CreateSceneButton(parent, tex, scene);
                }
#endif
            }
            LoadingIcon.loadingCount--;
        }

        private Button CreateSceneButton(Transform parent, Texture2D texture, FileInfo fileInfo)
        {
            var button = UIUtility.CreateButton("ImageButton", parent, "");
            button.OnPointerExitAsObservable().Subscribe(e =>
            {
                if(hoveredButton == button)
                    hoveredButton = null;
            });

            button.OnPointerClickAsObservable().Subscribe(e =>
            {
                // double click on the thumbnail loads the scene (clicks on Load/Import/Delete are handled by those buttons)
                if(e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left && e.clickCount == 2 && BetterSceneLoader.DoubleClickLoad.Value)
                {
                    currentButton = button;
                    currentPath = fileInfo.FullName;
                    LoadCurrent();
                }
            });

            button.OnPointerEnterAsObservable().Subscribe(e =>
            {
                hoveredButton = button;
                currentButton = button;
                currentPath = fileInfo.FullName;

                if(optionspanel.transform.parent != button.transform)
                {
                    optionspanel.transform.SetParent(button.transform);
                    optionspanel.transform.SetRect(0f, 0f, 1f, 0.15f);
                    optionspanel.gameObject.SetActive(true);

                    confirmpanel.transform.SetParent(button.transform);
                    confirmpanel.transform.SetRect(0.4f, 0.4f, 0.6f, 0.6f);

                    infopanel.transform.SetParent(button.transform);
                    infopanel.transform.SetRect(0f, 0.88f, 1f, 1f);
                    infopanel.gameObject.SetActive(true);
                    infotext.text = $"{FormatFilesize(fileInfo.Length)} {fileInfo.LastWriteTime}";
                }

                confirmpanel.gameObject.SetActive(false);
            });

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            button.gameObject.GetComponent<Image>().sprite = sprite;

            return button;
        }

        private string EncodePath(string path)
        {
            return path.Replace("#", "%23").Replace("+", "%2B").Replace("&", "%26");
        }

        private int FirstIndexMatch<TItem>(IEnumerable<TItem> items, Func<TItem,bool> matchCondition)
        {
            var index = 0;
            foreach(var item in items)
            {
                if(matchCondition.Invoke(item))
                    return index;
                index++;
            }
            return -1;
        }

        private string FormatFilesize(long length)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var size = Convert.ToDecimal(length);

            int order = 0;
            while(size >= 1024 && order < units.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.#} {units[order]}";
        }
    }
}
