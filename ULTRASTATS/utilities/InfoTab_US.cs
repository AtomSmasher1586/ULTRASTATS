using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ULTRASTATS;

internal static class InfoTab_US
{
    private const string InfoText = @"
For any issues, suggestions, bugs, or complaints you have about ULTRASTATS, please contact me through discord. I'd prefer DMs but I am also active on the New Blood Discord server's ultrakill-modding channel and the ULTRAKILL legacy server's ultramodding channel so you can reach me there too. I'd love to hear from you! 

    As of writing this ULTRASTATS has 1613 downloads on Thunderstore. I expected maybe 100 or so downloads, and instead got 10x the downloads I expected.

    Thanks to each and everyone of you who chose to download ULTRASTATS, it means the world to me that people want to use my silly passion project.

    Thanks to my fellow modders for helping me with my questions and issues.
 
    Expect the next big update within a couple weeks. I might also do a couple smaller updates with small improvements and features.

P.S. You can change what tab is the default through PluginConfigurator, along with some other preference settings.

Known issues:
 -- Stuttering when spam clicking through the endscreen.
 -- UI is garbage... I tried my best okay :(
 -- When discard on endscreen is enabled, spam clicking can cause incomplete run data to be saved.
If you find any other issues DM me on discord.
";
    private const float WheelPixelsPerTick = 96f;

    public static void Build(Transform parent, Transform styleRoot)
    {
        GameObject root = new GameObject("InfoTabRoot", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        BuildHeader(root.transform, styleRoot);
        BuildScrollView(root.transform, styleRoot);
    }

    private static void BuildHeader(Transform parent, Transform styleRoot)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(parent, false);

        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 178f);
        headerRect.anchoredPosition = Vector2.zero;

        GameObject iconObj = new GameObject("ModIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObj.transform.SetParent(header.transform, false);

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.sizeDelta = new Vector2(152f, 152f);
        iconRect.anchoredPosition = new Vector2(0f, -2f);

        Image icon = iconObj.GetComponent<Image>();
        icon.sprite = MainMenuButton_US.LoadSpriteFromPluginFile("icon.png");
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;

        GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(header.transform, false);

        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(178f, -92f);
        titleRect.offsetMax = new Vector2(12f, 0f);

        TextMeshProUGUI title = titleObj.GetComponent<TextMeshProUGUI>();
        MainMenuButton_US.ApplyPanelTextStyle(styleRoot, title);
        title.text = Plugin.ModName;
        title.fontSize = 80;
        title.characterSpacing = 2f;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.TopLeft;
        title.enableWordWrapping = false;
        title.overflowMode = TextOverflowModes.Overflow;
        title.raycastTarget = false;

        GameObject metaObj = new GameObject("Meta", typeof(RectTransform), typeof(TextMeshProUGUI));
        metaObj.transform.SetParent(header.transform, false);

        RectTransform metaRect = metaObj.GetComponent<RectTransform>();
        metaRect.anchorMin = new Vector2(0f, 1f);
        metaRect.anchorMax = new Vector2(1f, 1f);
        metaRect.pivot = new Vector2(0f, 1f);
        metaRect.offsetMin = new Vector2(182f, -164f);
        metaRect.offsetMax = new Vector2(12f, -92f);

        TextMeshProUGUI meta = metaObj.GetComponent<TextMeshProUGUI>();
        MainMenuButton_US.ApplyPanelTextStyle(styleRoot, meta);
        meta.text = $"Version {Plugin.ModVer}\nDiscord: atomsmasher_1586";
        meta.fontSize = 28;
        meta.color = Color.white;
        meta.lineSpacing = 6f;
        meta.alignment = TextAlignmentOptions.TopLeft;
        meta.enableWordWrapping = false;
        meta.overflowMode = TextOverflowModes.Overflow;
        meta.raycastTarget = false;

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(header.transform, false);

        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 0f);
        dividerRect.anchorMax = new Vector2(1f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0f);
        dividerRect.offsetMin = new Vector2(-38f, 0f);
        dividerRect.offsetMax = new Vector2(38f, 1f);

        Image dividerImage = divider.GetComponent<Image>();
        dividerImage.color = Color.white;
        dividerImage.raycastTarget = false;
    }

    private static void BuildScrollView(Transform parent, Transform styleRoot)
    {
        GameObject scrollRoot = new GameObject("InfoScrollRoot", typeof(RectTransform));
        scrollRoot.transform.SetParent(parent, false);

        RectTransform scrollRootRect = scrollRoot.GetComponent<RectTransform>();
        scrollRootRect.anchorMin = new Vector2(0f, 0f);
        scrollRootRect.anchorMax = new Vector2(1f, 1f);
        scrollRootRect.offsetMin = new Vector2(0f, 8f);
        scrollRootRect.offsetMax = new Vector2(0f, -184f);

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
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-46f, 0f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(UltraStatsInfoContentSizer));
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        GameObject bodyObj = new GameObject("InfoBodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
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
        body.fontSize = 20;
        body.color = Color.white;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 8f;
        body.text = InfoText;
        body.raycastTarget = false;

        UltraStatsInfoContentSizer sizer = content.GetComponent<UltraStatsInfoContentSizer>();
        sizer.ContentRect = contentRect;
        sizer.TextRect = bodyRect;
        sizer.Text = body;
        sizer.BottomPadding = 24f;

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
        trackRect.sizeDelta = new Vector2(18f, 12f);
        trackRect.anchoredPosition = new Vector2(20f, -12f);

        Image trackImage = trackObj.GetComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.18f);
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
        handleImage.color = Color.white;
        handleImage.raycastTarget = false;

        ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 0f;
        scrollRect.inertia = false;

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

internal sealed class UltraStatsInfoContentSizer : MonoBehaviour
{
    public RectTransform ContentRect = null!;
    public RectTransform TextRect = null!;
    public TextMeshProUGUI Text = null!;
    public float BottomPadding = 24f;

    private float _lastTextHeight = -1f;

    private void LateUpdate()
    {
        if (ContentRect == null || TextRect == null || Text == null)
            return;

        Canvas.ForceUpdateCanvases();

        float textHeight = Mathf.Ceil(Text.preferredHeight);
        if (Mathf.Abs(textHeight - _lastTextHeight) < 0.5f)
            return;

        _lastTextHeight = textHeight;
        ContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + BottomPadding);
        TextRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
    }
}