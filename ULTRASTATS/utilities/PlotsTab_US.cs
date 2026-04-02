using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ULTRASTATS;

internal static class PlotsTab_US
{
    private const string PlotText = "PLOTS TAB\n\nTHIS FEATURE IS CURRENTLY IN DEVELOPMENT.\n\nThe long-term plan is for this tab to hold ULTRASTATS visualizations such as:\n- run history plots\n- performance trends\n- weapon usage breakdowns\n- comparison charts\n other useful graphs\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n";
    private const float WheelPixelsPerTick = 96f;

    public static void Build(Transform parent, Transform styleRoot)
    {
        GameObject root = new GameObject("PlotsTabRoot", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        BuildScrollView(root.transform, styleRoot);
    }

    private static void BuildScrollView(Transform parent, Transform styleRoot)
    {
        GameObject scrollRoot = new GameObject("PlotsScrollRoot", typeof(RectTransform));
        scrollRoot.transform.SetParent(parent, false);

        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = Vector2.zero;
        scrollRootRect.anchorMax = Vector2.one;
        scrollRootRect.offsetMin = Vector2.zero;
        scrollRootRect.offsetMax = Vector2.zero;

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D),
            typeof(UltraStatsFixedWheelScroll)
        );
        viewport.transform.SetParent(scrollRoot.transform, false);

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-30f, 0f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(UltraStatsPlotsContentSizer)
        );
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        GameObject bodyObj = new GameObject("PlotsBodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(content.transform, false);

        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.offsetMin = new Vector2(18f, 0f);
        bodyRect.offsetMax = new Vector2(-18f, 0f);
        bodyRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI body = bodyObj.GetComponent<TextMeshProUGUI>();
        MainMenuButton_US.ApplyPanelTextStyle(styleRoot, body);
        body.fontSize = 22;
        body.color = Color.white;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 8f;
        body.text = PlotText;
        body.raycastTarget = false;

        GameObject imageObj = new GameObject("DoomahImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObj.transform.SetParent(content.transform, false);

        RectTransform imageRect = imageObj.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 1f);
        imageRect.anchorMax = new Vector2(0.5f, 1f);
        imageRect.pivot = new Vector2(0.5f, 1f);
        imageRect.sizeDelta = new Vector2(1080f, 1080f);
        imageRect.anchoredPosition = Vector2.zero;

        Image doomahImage = imageObj.GetComponent<Image>();
        doomahImage.sprite = MainMenuButton_US.LoadSpriteFromPluginFile("images/doomah.png")
                            ?? MainMenuButton_US.LoadSpriteFromPluginFile("doomah.png");
        doomahImage.type = Image.Type.Simple;
        doomahImage.preserveAspect = true;
        doomahImage.color = Color.white;
        doomahImage.raycastTarget = false;

        GameObject trackObj = new GameObject(
            "ScrollbarTrack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UltraStatsScrollbarDrag),
            typeof(UltraStatsFixedWheelScroll)
        );
        trackObj.transform.SetParent(scrollRoot.transform, false);

        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = new Vector2(1f, 1f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(18f, 0f);
        trackRect.anchoredPosition = Vector2.zero;

        Image trackImage = trackObj.GetComponent<Image>();
        trackImage.color = Color.black;
        trackImage.raycastTarget = true;

        GameObject handleObj = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        handleObj.transform.SetParent(trackObj.transform, false);

        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(1f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(0f, 96f);
        handleRect.anchoredPosition = Vector2.zero;

        Image handleImage = handleObj.GetComponent<Image>();
        handleImage.color = Color.black;
        handleImage.raycastTarget = false;

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 0f;
        scrollRect.inertia = false;

        UltraStatsPlotsContentSizer sizer = content.GetComponent<UltraStatsPlotsContentSizer>();
        sizer.ContentRect = contentRect;
        sizer.TextRect = bodyRect;
        sizer.Text = body;
        sizer.ImageRect = imageRect;
        sizer.Image = doomahImage;
        sizer.TopPadding = 12f;
        sizer.RevealSpacer = 100f;
        sizer.ImageGap = 28f;
        sizer.BottomPadding = 24f;
        sizer.ImageMaxHeight = 1080f;
        sizer.ImageMaxWidth = 1080f;

        UltraStatsScrollbarDrag drag = trackObj.GetComponent<UltraStatsScrollbarDrag>();
        drag.ScrollRect = scrollRect;
        drag.TrackRect = trackRect;
        drag.HandleRect = handleRect;
        drag.MinHandleHeight = 72f;

        UltraStatsFixedWheelScroll viewportWheel = viewport.GetComponent<UltraStatsFixedWheelScroll>();
        viewportWheel.ScrollRect = scrollRect;
        viewportWheel.PixelsPerWheelTick = WheelPixelsPerTick;

        UltraStatsFixedWheelScroll trackWheel = trackObj.GetComponent<UltraStatsFixedWheelScroll>();
        trackWheel.ScrollRect = scrollRect;
        trackWheel.PixelsPerWheelTick = WheelPixelsPerTick;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
        drag.RefreshHandle();
    }
}

internal sealed class UltraStatsPlotsContentSizer : MonoBehaviour
{
    public RectTransform ContentRect = null!;
    public RectTransform TextRect = null!;
    public TextMeshProUGUI Text = null!;
    public RectTransform ImageRect = null!;
    public Image Image = null!;
    public float TopPadding = 12f;
    public float RevealSpacer = 220f;
    public float ImageGap = 28f;
    public float BottomPadding = 24f;
    public float ImageMaxHeight = 1080f;
    public float ImageMaxWidth = 1080f;

    private void LateUpdate()
    {
        if (ContentRect == null || TextRect == null || Text == null || ImageRect == null || Image == null)
            return;

        Canvas.ForceUpdateCanvases();

        float textHeight = Mathf.Ceil(Text.preferredHeight);
        TextRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);

        float imageWidth = 0f;
        float imageHeight = 0f;

        if (Image.sprite != null)
        {
            imageWidth = ImageMaxWidth;
            imageHeight = ImageMaxHeight;

            Rect spriteRect = Image.sprite.rect;
            if (spriteRect.width > 0f && spriteRect.height > 0f)
            {
                float aspect = spriteRect.width / spriteRect.height;
                if (aspect >= 1f)
                    imageHeight = imageWidth / aspect;
                else
                    imageWidth = imageHeight * aspect;
            }
        }

        ImageRect.anchoredPosition = new Vector2(0f, -(TopPadding + textHeight + RevealSpacer + (imageHeight > 0f ? ImageGap : 0f)));
        ImageRect.sizeDelta = new Vector2(imageWidth, imageHeight);
        Image.enabled = Image.sprite != null;

        float totalHeight = TopPadding + textHeight + RevealSpacer + (imageHeight > 0f ? ImageGap + imageHeight : 0f) + BottomPadding;
        ContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
    }
}
