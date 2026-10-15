using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Code.Menu
{
    [ExecuteAlways]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class StoreScrollView : MonoBehaviour
    {
        private const string ViewportName = "Viewport";
        private const string ContentName = "Content";
        private const string ScrollbarName = "VerticalScrollbar";
        private const string GeneratedMarker = "StoreItem_";

        private static readonly Color CardColor = new Color(0.98f, 0.93f, 0.82f, 1f);
        private static readonly Color CardBorderColor = new Color(0.58f, 0.27f, 0.14f, 1f);
        private static readonly Color TextColor = new Color(0.27f, 0.14f, 0.09f, 1f);

        private ScrollRect _scrollRect;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
        }

        private void BuildIfNeeded()
        {
            _scrollRect = GetComponent<ScrollRect>();
            if (_scrollRect == null)
                return;

            Transform viewport = FindOrCreate(transform, ViewportName);
            Transform content = FindOrCreate(viewport, ContentName);
            Transform scrollbar = FindOrCreate(transform, ScrollbarName);

            ConfigureRoot();
            ConfigureViewport(viewport);
            ConfigureContent(content);
            ConfigureScrollbar(scrollbar);
            ConfigureScrollRect(viewport, content, scrollbar);

            if (!HasGeneratedCards(content))
                CreateCards(content);
        }

        private void ConfigureRoot()
        {
            RectTransform root = GetComponent<RectTransform>();
            if (root == null)
                return;

            Image image = GetOrAdd<Image>(gameObject);
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;
        }

        private static void ConfigureViewport(Transform viewportTransform)
        {
            RectTransform viewport = GetRectTransform(viewportTransform.gameObject);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(18f, 18f);
            viewport.offsetMax = new Vector2(-54f, -18f);

            Image image = GetOrAdd<Image>(viewport.gameObject);
            image.color = new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;
            GetOrAdd<Mask>(viewport.gameObject).showMaskGraphic = false;
        }

        private static void ConfigureContent(Transform contentTransform)
        {
            RectTransform content = GetRectTransform(contentTransform.gameObject);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(content.gameObject);
            grid.padding = new RectOffset(18, 18, 18, 18);
            grid.cellSize = new Vector2(430f, 220f);
            grid.spacing = new Vector2(22f, 22f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureScrollbar(Transform scrollbarTransform)
        {
            RectTransform scrollbarRect = GetRectTransform(scrollbarTransform.gameObject);
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(-14f, 0f);
            scrollbarRect.sizeDelta = new Vector2(28f, -36f);

            Image background = GetOrAdd<Image>(scrollbarRect.gameObject);
            background.color = new Color(0.23f, 0.15f, 0.13f, 0.28f);
            background.raycastTarget = true;

            Scrollbar scrollbar = GetOrAdd<Scrollbar>(scrollbarRect.gameObject);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.size = 0.25f;

            Transform slidingArea = FindOrCreate(scrollbarTransform, "Sliding Area");
            RectTransform slidingRect = GetRectTransform(slidingArea.gameObject);
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(4f, 4f);
            slidingRect.offsetMax = new Vector2(-4f, -4f);

            Transform handle = FindOrCreate(slidingArea, "Handle");
            RectTransform handleRect = GetRectTransform(handle.gameObject);
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            Image handleImage = GetOrAdd<Image>(handle.gameObject);
            handleImage.color = new Color(0.75f, 0.4f, 0.2f, 1f);
            handleImage.raycastTarget = true;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
        }

        private void ConfigureScrollRect(Transform viewport, Transform content, Transform scrollbar)
        {
            _scrollRect.content = content.GetComponent<RectTransform>();
            _scrollRect.viewport = viewport.GetComponent<RectTransform>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Elastic;
            _scrollRect.elasticity = 0.12f;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.14f;
            _scrollRect.scrollSensitivity = 28f;
            _scrollRect.verticalScrollbar = scrollbar.GetComponent<Scrollbar>();
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            _scrollRect.verticalScrollbarSpacing = -3f;
        }

        private static bool HasGeneratedCards(Transform content)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).name.StartsWith(GeneratedMarker))
                    return true;
            }

            return false;
        }

        private static void CreateCards(Transform content)
        {
            CreateCard(content, "광고 제거", "게임 중 광고를 표시하지 않아요", "₩5,000");
            CreateCard(content, "골드 보급 상자", "게임에 사용할 골드를 얻어요", "준비 중");
            CreateCard(content, "추가 생명", "게임 오버를 한 번 더 막아줘요", "준비 중");
            CreateCard(content, "고양이 스킨", "귀여운 고양이 블럭을 꾸며요", "준비 중");
            CreateCard(content, "치즈 보너스", "치즈 보상을 더 많이 받아요", "준비 중");
            CreateCard(content, "특별 묶음", "게임을 더 편하게 즐겨보세요", "준비 중");
        }

        private static void CreateCard(Transform content, string title, string description, string price)
        {
            GameObject card = new GameObject("StoreItem_" + title, typeof(RectTransform), typeof(Image), typeof(Outline));
            card.transform.SetParent(content, false);

            Image image = card.GetComponent<Image>();
            image.color = CardColor;
            image.raycastTarget = false;

            Outline outline = card.GetComponent<Outline>();
            outline.effectColor = CardBorderColor;
            outline.effectDistance = new Vector2(3f, -3f);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(430f, 220f);

            CreateText(card.transform, title, new Vector2(0f, 56f), 34f, TextColor, FontStyles.Bold);
            CreateText(card.transform, description, new Vector2(0f, 5f), 20f, TextColor, FontStyles.Normal);
            CreateText(card.transform, price, new Vector2(0f, -68f), 27f, new Color(0.65f, 0.29f, 0.12f, 1f), FontStyles.Bold);
        }

        private static void CreateText(Transform parent, string value, Vector2 position, float fontSize, Color color, FontStyles style)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.5f);
            rect.anchorMax = new Vector2(0.95f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 48f);
            rect.anchoredPosition = position;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
        }

        private static Transform FindOrCreate(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                return child;

            GameObject childObject = new GameObject(childName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static RectTransform GetRectTransform(GameObject gameObject)
        {
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            return rect != null ? rect : gameObject.AddComponent<RectTransform>();
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
