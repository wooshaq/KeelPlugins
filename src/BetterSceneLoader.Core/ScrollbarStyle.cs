using UnityEngine;
using UnityEngine.UI;

namespace BetterSceneLoader
{
    /// <summary>
    /// Shared look for the vertical scrollbars of the scene grid and the folder tree.
    /// </summary>
    public static class ScrollbarStyle
    {
        public const float Width = 15f;

        public static void Apply(ScrollRect scroll, float gap)
        {
            scroll.horizontal = false;
            if(scroll.horizontalScrollbar != null)
            {
                var h = scroll.horizontalScrollbar.gameObject;
                scroll.horizontalScrollbar = null;
                h.SetActive(false);
                Object.Destroy(h);
            }

            var bar = scroll.verticalScrollbar;
            if(bar == null)
                return;

            var rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Width, 0f);

            // ScrollRect shrinks the viewport by scrollbar width + spacing while the scrollbar is shown
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = gap;

            var view = scroll.viewport;
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.pivot = new Vector2(0f, 1f);
            view.offsetMin = Vector2.zero;
            view.offsetMax = Vector2.zero;
        }

        /// <summary>Copy colors and sprites so both scrollbars look identical.</summary>
        public static void CopyLook(Scrollbar from, Scrollbar to)
        {
            if(from == null || to == null)
                return;

            CopyImage(from.GetComponent<Image>(), to.GetComponent<Image>());
            if(from.handleRect != null && to.handleRect != null)
                CopyImage(from.handleRect.GetComponent<Image>(), to.handleRect.GetComponent<Image>());
            to.colors = from.colors;

            if(from.handleRect != null && to.handleRect != null)
            {
                var slidingFrom = (RectTransform)from.handleRect.parent;
                var slidingTo = (RectTransform)to.handleRect.parent;
                slidingTo.offsetMin = slidingFrom.offsetMin;
                slidingTo.offsetMax = slidingFrom.offsetMax;
                to.handleRect.offsetMin = from.handleRect.offsetMin;
                to.handleRect.offsetMax = from.handleRect.offsetMax;
            }
        }

        private static void CopyImage(Image from, Image to)
        {
            if(from == null || to == null)
                return;
            to.sprite = from.sprite;
            to.type = from.type;
            to.color = from.color;
        }
    }
}
