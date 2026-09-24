using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UILib;
using UnityEngine;
using UnityEngine.UI;

namespace BetterSceneLoader
{
    /// <summary>
    /// Folder tree panel shown on the left side of the scene grid window.
    /// Clicking a folder name selects it, the +/- button expands or collapses it.
    /// </summary>
    public class FolderTreeView
    {
        private const float RowHeight = 20f;
        private const float IndentSize = 14f;
        private const float ExpandButtonSize = 16f;
        private const float SearchHeight = 22f;
        private const int FontSize = 14;

        public static readonly Color PanelColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        private static readonly Color textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        private static readonly Color selectedTextColor = new Color(0f, 1f, 1f, 1f);
        private static readonly Color expandButtonColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        private class Node
        {
            public string FullPath;
            public string Name;
            public readonly List<Node> Children = new List<Node>();
        }

        private readonly Func<List<SceneRoot>> rootsProvider;
        private readonly Action<string> onFolderSelected;
        private readonly HashSet<string> expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Text> rowTexts = new Dictionary<string, Text>(StringComparer.OrdinalIgnoreCase);

        private readonly List<Node> roots = new List<Node>();
        private readonly HashSet<string> rootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string selectedPath;
        private string filter = "";

        private Image panel;
        private ScrollRect scroll;
        private InputField searchField;

        public RectTransform RectTransform => panel.rectTransform;
        public ScrollRect Scroll => scroll;

        public FolderTreeView(Func<List<SceneRoot>> rootsProvider, Action<string> onFolderSelected)
        {
            this.rootsProvider = rootsProvider;
            this.onFolderSelected = onFolderSelected;
        }

        public void CreateUI(Transform parent)
        {
            panel = UIUtility.CreatePanel("FolderTreePanel", parent);
            panel.color = PanelColor;
            UIUtility.AddOutlineToObject(panel.transform, new Color(0f, 0f, 0f, 1f));

            var title = UIUtility.CreateText("FolderTreeTitle", panel.transform, "Select folder to view");
            title.transform.SetRect(0f, 1f, 1f, 1f, 4f, -RowHeight, -4f, 0f);
            title.resizeTextForBestFit = false;
            title.fontSize = FontSize;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = textColor;

            scroll = UIUtility.CreateScrollView("FolderTreeScroll", panel.transform);
            scroll.transform.SetRect(0f, 0f, 1f, 1f, 2f, SearchHeight + 4f, -2f, -RowHeight);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            ScrollbarStyle.Apply(scroll, 3f);

            var content = scroll.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, 0f);

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var searchLabel = UIUtility.CreateText("SearchLabel", panel.transform, "Search:");
            searchLabel.transform.SetRect(0f, 0f, 0f, 0f, 4f, 2f, 60f, SearchHeight + 2f);
            searchLabel.resizeTextForBestFit = false;
            searchLabel.fontSize = FontSize;
            searchLabel.alignment = TextAnchor.MiddleLeft;
            searchLabel.color = textColor;

            searchField = UIUtility.CreateInputField("FolderSearch", panel.transform, "");
            searchField.transform.SetRect(0f, 0f, 1f, 0f, 62f, 2f, -4f, SearchHeight + 2f);
            searchField.onValueChanged.AddListener(x =>
            {
                filter = x ?? "";
                RenderRows();
            });

            Rebuild();
        }

        public void SetVisible(bool visible)
        {
            if(panel != null)
                panel.gameObject.SetActive(visible);
        }

        /// <summary>Rescan folders from disk and redraw.</summary>
        public void Rebuild()
        {
            roots.Clear();
            rootPaths.Clear();

            foreach(var sceneRoot in rootsProvider())
            {
                if(sceneRoot.IsDefault && !Directory.Exists(sceneRoot.Path))
                    Directory.CreateDirectory(sceneRoot.Path);

                var node = BuildNode(sceneRoot.Path);
                node.Name = sceneRoot.Label;
                roots.Add(node);
                rootPaths.Add(sceneRoot.Path);

                // default root starts expanded, extra roots start collapsed
                if(sceneRoot.IsDefault && selectedPath == null)
                    expandedPaths.Add(sceneRoot.Path);
            }

            if(selectedPath == null && roots.Count > 0)
                selectedPath = roots[0].FullPath;

            RenderRows();
        }

        /// <summary>Highlight the given folder and expand its parents.</summary>
        public void SetSelected(string path)
        {
            if(string.IsNullOrEmpty(path))
                return;

            var previous = selectedPath;
            selectedPath = path;

            var needsRender = false;
            if(!rootPaths.Contains(path) && rootPaths.Any(r => path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                var parent = Path.GetDirectoryName(path);
                while(!string.IsNullOrEmpty(parent))
                {
                    if(expandedPaths.Add(parent))
                        needsRender = true;
                    if(rootPaths.Contains(parent))
                        break;
                    parent = Path.GetDirectoryName(parent);
                }
            }

            if(needsRender || panel == null)
            {
                RenderRows();
                return;
            }

            if(previous != null && rowTexts.TryGetValue(previous, out var prevText))
                prevText.color = textColor;
            if(rowTexts.TryGetValue(path, out var newText))
                newText.color = selectedTextColor;
        }

        private static Node BuildNode(string path)
        {
            var node = new Node
            {
                FullPath = path,
                Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            };

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(path);
            }
            catch(Exception)
            {
                return node;
            }

            foreach(var dir in dirs.OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase))
                node.Children.Add(BuildNode(dir));

            return node;
        }

        private void RenderRows()
        {
            if(scroll == null)
                return;

            var content = scroll.content;
            for(var i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                GameObject.Destroy(child);
            }
            rowTexts.Clear();

            HashSet<Node> visible = null;
            if(!string.IsNullOrEmpty(filter))
            {
                visible = new HashSet<Node>();
                foreach(var root in roots)
                    CollectMatches(root, filter, visible);
            }

            foreach(var root in roots)
                RenderNode(root, 0, visible);
        }

        private static bool CollectMatches(Node node, string filter, HashSet<Node> visible)
        {
            var match = node.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            foreach(var child in node.Children)
            {
                if(CollectMatches(child, filter, visible))
                    match = true;
            }

            if(match)
                visible.Add(node);
            return match;
        }

        private void RenderNode(Node node, int depth, HashSet<Node> visible)
        {
            // roots are always shown so the user can go back to them
            if(visible != null && depth > 0 && !visible.Contains(node))
                return;

            CreateRow(node, depth, visible != null);

            var expanded = visible != null || expandedPaths.Contains(node.FullPath);
            if(!expanded)
                return;

            foreach(var child in node.Children)
                RenderNode(child, depth + 1, visible);
        }

        private void CreateRow(Node node, int depth, bool filtering)
        {
            var row = UIUtility.CreateNewUIObject(scroll.content, "Row_" + node.Name);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;

            var indent = depth * IndentSize;

            if(node.Children.Count > 0)
            {
                var isExpanded = filtering || expandedPaths.Contains(node.FullPath);
                var expand = UIUtility.CreateButton("Expand", row, isExpanded ? "-" : "+");
                expand.transform.SetRect(0f, 0.5f, 0f, 0.5f, indent + 2f, -ExpandButtonSize / 2f, indent + 2f + ExpandButtonSize, ExpandButtonSize / 2f);
                var expandImage = expand.GetComponent<Image>();
                expandImage.color = expandButtonColor;
                var expandColors = expand.colors;
                expandColors.normalColor = Color.white;
                expandColors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
                expandColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
#if KKS
                expandColors.selectedColor = Color.white;
#endif
                expandColors.colorMultiplier = 1f;
                expandColors.fadeDuration = 0f;
                expand.colors = expandColors;
                expandImage.canvasRenderer.SetColor(Color.white);
                var expandText = expand.GetComponentInChildren<Text>();
                expandText.resizeTextForBestFit = false;
                expandText.fontSize = FontSize;
                expandText.fontStyle = FontStyle.Bold;
                expandText.color = textColor;
                expandText.rectTransform.SetRect(0f, 0f, 1f, 1f);
                expand.onClick.AddListener(() =>
                {
                    if(filtering)
                        return;
                    if(!expandedPaths.Remove(node.FullPath))
                        expandedPaths.Add(node.FullPath);
                    RenderRows();
                });
            }

            var label = UIUtility.CreateButton("Label", row, node.Name);
            label.transform.SetRect(0f, 0f, 1f, 1f, indent + ExpandButtonSize + 6f, 0f, 0f, 0f);

            var labelImage = label.GetComponent<Image>();
            labelImage.sprite = null;
            labelImage.color = Color.white;
            var colors = label.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.25f);
            colors.fadeDuration = 0f;
#if KKS
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
#endif
            label.colors = colors;
            // new rows start fully transparent instead of fading in from white
            labelImage.canvasRenderer.SetColor(colors.normalColor);

            var text = label.GetComponentInChildren<Text>();
            text.resizeTextForBestFit = false;
            text.fontSize = FontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.rectTransform.SetRect(0f, 0f, 1f, 1f, 2f, 0f, 0f, 0f);
            var isSelected = string.Equals(node.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase);
            text.color = isSelected ? selectedTextColor : textColor;
            rowTexts[node.FullPath] = text;

            label.onClick.AddListener(() =>
            {
                if(!filtering && node.Children.Count > 0 && expandedPaths.Add(node.FullPath))
                {
                    selectedPath = node.FullPath;
                    RenderRows();
                }
                else
                {
                    SetSelected(node.FullPath);
                }
                onFolderSelected?.Invoke(node.FullPath);
            });
        }
    }
}
