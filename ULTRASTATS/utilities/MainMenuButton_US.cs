using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ULTRASTATS;

internal static class MainMenuButton_US
{
    private const string MenuSceneName = "b3e7f2f8052488a45b35549efb98d902";
    private const string ButtonObjectName = "ULTRASTATS_MenuButton";
    private const string PanelObjectName = "ULTRASTATS_MenuPanel";
    private const string BlockerObjectName = "ULTRASTATS_MenuBlocker";
    private static readonly Dictionary<string, Sprite> BevelSpriteCache = new();

    private const float PanelWidth = 1200f;
    private const float PanelHeight = 770f;
    private const float BorderThickness = 4f;
    private const float TabWidth = 186f;
    private const float TabHeight = 44f;
    private const float TabSpacing = -2f;
    private const float TabOverlapIntoPanel = 16f;

    private static bool _subscribed;
    private static bool _injecting;

    private enum MenuCorner
    {
        BottomRight = 0,
        BottomLeft = 1,
        TopRight = 2,
        TopLeft = 3
    }

    public static void Init()
    {
        if (_subscribed)
            return;

        _subscribed = true;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        BepInExLogs_US.Debug("MainMenuButtonInjector.Init called");
    }

    public static void Shutdown()
    {
        if (!_subscribed)
            return;

        _subscribed = false;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    public static void RefreshButtonPosition()
    {
        GameObject? canvasRoot = FindRootCanvasObject(SceneManager.GetActiveScene());
        Transform? mainMenu = canvasRoot?.transform.Find("Main Menu (1)");
        Transform? button = mainMenu?.Find(ButtonObjectName);
        if (button != null)
            ApplyButtonPosition(button.GetComponent<RectTransform>());
    }

    private static void ApplyButtonPosition(RectTransform rect)
    {
        if (rect == null)
            return;

        MenuCorner corner = (MenuCorner)Plugin.MainMenuButtonCorner;

        switch (corner)
        {
            case MenuCorner.BottomLeft:
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(28f, 28f);
                break;

            case MenuCorner.TopRight:
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-28f, -28f);
                break;

            case MenuCorner.TopLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(28f, -28f);
                break;

            default:
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-28f, 28f);
                break;
        }
    }

    private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        BepInExLogs_US.Debug($"Scene changed: '{oldScene.name}' -> '{newScene.name}'");

        if (Plugin.Instance == null)
            return;

        if (newScene.name != MenuSceneName)
            return;

        if (_injecting)
            return;

        Plugin.Instance.StartCoroutine(InjectIntoMenuScene(newScene));
    }

    private static IEnumerator InjectIntoMenuScene(Scene scene)
    {
        _injecting = true;

        for (int i = 0; i < 600; i++)
        {
            if (!scene.isLoaded)
            {
                _injecting = false;
                yield break;
            }

            GameObject? canvasRoot = FindRootCanvasObject(scene);
            if (canvasRoot != null)
            {
                Transform? mainMenu = canvasRoot.transform.Find("Main Menu (1)");
                Transform? leftSide = mainMenu?.Find("LeftSide");

                BepInExLogs_US.Debug($"mainMenu found = {mainMenu != null}, leftSide found = {leftSide != null}");

                if (mainMenu != null && leftSide != null)
                {
                    Inject(leftSide, mainMenu);
                    _injecting = false;
                    yield break;
                }
            }

            yield return null;
        }

        BepInExLogs_US.Warn("ULTRASTATS could not find Canvas/Main Menu (1)/LeftSide.");
        _injecting = false;
    }

    private static GameObject? FindRootCanvasObject(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Canvas")
                return root;
        }

        return null;
    }

    private static void TogglePanel(Transform mainMenu)
    {
        Transform? existing = mainMenu.Find(PanelObjectName);
        Transform? blocker = mainMenu.Find(BlockerObjectName);

        if (existing == null)
        {
            BuildPanel(mainMenu);
            existing = mainMenu.Find(PanelObjectName);
            blocker = mainMenu.Find(BlockerObjectName);
        }

        if (existing != null)
        {
            bool nextState = !existing.gameObject.activeSelf;
            existing.gameObject.SetActive(nextState);

            if (blocker != null)
                blocker.gameObject.SetActive(nextState);

            if (nextState)
                ApplyConfiguredDefaults(existing.gameObject, true);
        }
    }

    private static int GetConfiguredDefaultTabIndex()
    {
        return Plugin.DefaultMainMenuTab switch
        {
            Plugin.DefaultMainMenuTabOption.Stats => 1,
            Plugin.DefaultMainMenuTabOption.Plots => 2,
            _ => 0
        };
    }

    private static void ApplyConfiguredDefaults(GameObject panel, bool reloadStats)
    {
        if (panel == null)
            return;

        UltraStatsTabbedPanelController? tabs = panel.GetComponent<UltraStatsTabbedPanelController>();
        tabs?.ShowTab(GetConfiguredDefaultTabIndex());

        StatsTabController_US? statsController = panel.GetComponentInChildren<StatsTabController_US>(true);
        if (statsController != null)
            statsController.ApplyConfiguredDifficultySelection(reloadStats);
    }

    private static void BuildPanel(Transform mainMenu)
    {
        Transform? leftSide = mainMenu.Find("LeftSide");
        if (leftSide == null)
        {
            BepInExLogs_US.Warn("Could not find LeftSide while building ULTRASTATS panel.");
            return;
        }

        ResolveButtonTemplates(leftSide, out GameObject inactiveTemplate, out GameObject activeTemplate);
        CreateScreenBlocker(mainMenu);

        const float panelMargin = 80f;
        const float minPanelWidth = 640f;
        const float minPanelHeight = 420f;
        const float tabTopInset = 8f;
        const float closeButtonReserve = 52f;

        GameObject panel = new GameObject(
            PanelObjectName,
            typeof(RectTransform),
            typeof(UltraStatsMenuPanelCloser),
            typeof(UltraStatsTabbedPanelController)
        );

        panel.transform.SetParent(mainMenu, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 5f);

        RectTransform? mainMenuRect = mainMenu as RectTransform;

        float availableWidth = PanelWidth;
        float availableHeight = PanelHeight;

        if (mainMenuRect != null)
        {
            availableWidth = Mathf.Max(minPanelWidth, mainMenuRect.rect.width - panelMargin);
            availableHeight = Mathf.Max(minPanelHeight, mainMenuRect.rect.height - panelMargin);
        }

        float actualPanelWidth = Mathf.Min(PanelWidth, availableWidth);
        float actualPanelHeight = Mathf.Min(PanelHeight, availableHeight);

        panelRect.sizeDelta = new Vector2(actualPanelWidth, actualPanelHeight);

        UltraStatsTabbedPanelController tabs = panel.GetComponent<UltraStatsTabbedPanelController>();

        GameObject body = CreatePanelBody(panel.transform, "PanelBody");
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.sizeDelta = new Vector2(
            actualPanelWidth,
            actualPanelHeight - (TabHeight + tabTopInset - TabOverlapIntoPanel)
        );
        bodyRect.anchoredPosition = new Vector2(
            0f,
            -(TabHeight + tabTopInset - TabOverlapIntoPanel)
        );

        GameObject contentRoot = new GameObject("ContentRoot", typeof(RectTransform));
        contentRoot.transform.SetParent(body.transform, false);

        RectTransform contentRootRect = contentRoot.GetComponent<RectTransform>();
        contentRootRect.anchorMin = Vector2.zero;
        contentRootRect.anchorMax = Vector2.one;
        contentRootRect.offsetMin = new Vector2(38f, 30f);
        contentRootRect.offsetMax = new Vector2(-38f, -30f);

        GameObject tabBar = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabBar.transform.SetParent(panel.transform, false);

        RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0f, 1f);
        tabBarRect.anchorMax = new Vector2(1f, 1f);
        tabBarRect.pivot = new Vector2(0f, 1f);
        tabBarRect.offsetMin = new Vector2(0f, -TabHeight - tabTopInset);
        tabBarRect.offsetMax = new Vector2(-closeButtonReserve, -tabTopInset);

        HorizontalLayoutGroup tabLayout = tabBar.GetComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = TabSpacing;
        tabLayout.padding = new RectOffset(0, 0, 0, 0);
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = false;
        tabLayout.childAlignment = TextAnchor.UpperLeft;

        CreateCloseButton(panel.transform, panel);

        GameObject infoPage = CreateTabPage(contentRoot.transform, "InfoPage");
        GameObject statsPage = CreateTabPage(contentRoot.transform, "StatsPage");
        GameObject plotsPage = CreateTabPage(contentRoot.transform, "PlotsPage");

        InfoTab_US.Build(infoPage.transform, mainMenu);
        StatsTab_US.Build(statsPage.transform, mainMenu);
        PlotsTab_US.Build(plotsPage.transform, mainMenu);

        const int tabCount = 3;
        float availableTabWidth = actualPanelWidth - closeButtonReserve;
        float dynamicTabWidth = Mathf.Floor(
            (availableTabWidth - ((tabCount - 1) * TabSpacing)) / tabCount
        );
        dynamicTabWidth = Mathf.Clamp(dynamicTabWidth, 120f, TabWidth);

        tabs.AddTab(CreateTabButton(tabBar.transform, inactiveTemplate, activeTemplate, "INFO", dynamicTabWidth, () => tabs.ShowTab(0)), infoPage);
        tabs.AddTab(CreateTabButton(tabBar.transform, inactiveTemplate, activeTemplate, "STATS", dynamicTabWidth, () => tabs.ShowTab(1)), statsPage);
        tabs.AddTab(CreateTabButton(tabBar.transform, inactiveTemplate, activeTemplate, "PLOTS", dynamicTabWidth, () => tabs.ShowTab(2)), plotsPage);

        ApplyConfiguredDefaults(panel, false);

        panel.SetActive(false);

        Transform? blocker = mainMenu.Find(BlockerObjectName);
        if (blocker != null)
            blocker.gameObject.SetActive(false);
    }

    private static GameObject CreatePanelBody(Transform parent, string name)
    {
        GameObject body = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        body.transform.SetParent(parent, false);

        Image fill = body.GetComponent<Image>();
        fill.color = new Color(0f, 0f, 0f, 0.975f);
        fill.raycastTarget = true;

        CreateBorderStrip(body.transform, "TopBorder", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -BorderThickness), new Vector2(0f, 0f));
        CreateBorderStrip(body.transform, "BottomBorder", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, BorderThickness));
        CreateBorderStrip(body.transform, "LeftBorder", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(BorderThickness, 0f));
        CreateBorderStrip(body.transform, "RightBorder", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-BorderThickness, 0f), new Vector2(0f, 0f));

        return body;
    }

    private static void CreateScreenBlocker(Transform mainMenu)
    {
        Transform? existing = mainMenu.Find(BlockerObjectName);
        if (existing != null)
            return;

        GameObject blocker = new GameObject(
            BlockerObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        blocker.transform.SetParent(mainMenu, false);

        RectTransform rect = blocker.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = blocker.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.001f);
        image.raycastTarget = true;

        blocker.SetActive(false);
    }

    private static GameObject CreateBeveledFrame(
        Transform parent,
        string name,
        Vector2 size,
        Color fillColor,
        int borderThickness,
        int bevelSize,
        bool interceptClicks)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        int width = Mathf.Max(1, Mathf.RoundToInt(size.x));
        int height = Mathf.Max(1, Mathf.RoundToInt(size.y));
        int innerWidth = Mathf.Max(1, width - borderThickness * 2);
        int innerHeight = Mathf.Max(1, height - borderThickness * 2);
        int innerBevel = Mathf.Max(0, bevelSize - borderThickness);

        Image borderImage = root.GetComponent<Image>();
        borderImage.sprite = GetBevelSprite(width, height, bevelSize);
        borderImage.type = Image.Type.Simple;
        borderImage.color = Color.white;
        borderImage.raycastTarget = interceptClicks;

        GameObject fillObj = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        fillObj.transform.SetParent(root.transform, false);

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(borderThickness, borderThickness);
        fillRect.offsetMax = new Vector2(-borderThickness, -borderThickness);

        Image fillImage = fillObj.GetComponent<Image>();
        fillImage.sprite = GetBevelSprite(innerWidth, innerHeight, innerBevel);
        fillImage.type = Image.Type.Simple;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        return root;
    }

    private static Sprite GetBevelSprite(int width, int height, int bevelSize)
    {
        string key = $"{width}x{height}x{bevelSize}";
        if (BevelSpriteCache.TryGetValue(key, out Sprite existing))
            return existing;

        int clampedBevel = Mathf.Clamp(bevelSize, 0, Mathf.Min(width, height) / 2);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[width * height];
        Vector2[] polygon = BuildBevelPolygon(width, height, clampedBevel);

        const int sampleGrid = 4;
        float inv = 1f / (sampleGrid * sampleGrid);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < sampleGrid; sy++)
                {
                    for (int sx = 0; sx < sampleGrid; sx++)
                    {
                        Vector2 p = new Vector2(
                            x + (sx + 0.5f) / sampleGrid,
                            y + (sy + 0.5f) / sampleGrid
                        );

                        if (PointInPolygon(p, polygon))
                            covered++;
                    }
                }

                byte a = (byte)Mathf.RoundToInt(255f * covered * inv);
                pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        BevelSpriteCache[key] = sprite;
        return sprite;
    }

    private static Vector2[] BuildBevelPolygon(int width, int height, int bevel)
    {
        float maxX = width;
        float maxY = height;

        return new[]
        {
            new Vector2(0f, bevel),
            new Vector2(bevel, 0f),
            new Vector2(maxX - bevel, 0f),
            new Vector2(maxX, bevel),
            new Vector2(maxX, maxY - bevel),
            new Vector2(maxX - bevel, maxY),
            new Vector2(bevel, maxY),
            new Vector2(0f, maxY - bevel)
        };
    }

    private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            bool intersect = ((pi.y > point.y) != (pj.y > point.y))
                             && (point.x < (pj.x - pi.x) * (point.y - pi.y) / ((pj.y - pi.y) + float.Epsilon) + pi.x);

            if (intersect)
                inside = !inside;

            j = i;
        }

        return inside;
    }

    private static void CreateBorderStrip(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject strip = new GameObject(name, typeof(RectTransform), typeof(Image));
        strip.transform.SetParent(parent, false);

        RectTransform rect = strip.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = strip.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static void CreateCloseButton(Transform parent, GameObject panel)
    {
        GameObject buttonObj = CreateBeveledFrame(
            parent,
            "CloseButton",
            new Vector2(40f, 40f),
            new Color(0.72f, 0.10f, 0.10f, 1f),
            Mathf.RoundToInt(BorderThickness),
            10,
            true
        );

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(0f, -10f);

        Button button = buttonObj.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            panel.SetActive(false);
            Transform? blocker = panel.transform.parent != null
                ? panel.transform.parent.Find(BlockerObjectName)
                : null;
            if (blocker != null)
                blocker.gameObject.SetActive(false);
        });

        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(buttonObj.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        label.text = "X";
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontSize = 20;
        label.raycastTarget = false;
    }

    private static GameObject CreateTabPage(Transform parent, string name)
    {
        GameObject page = new GameObject(name, typeof(RectTransform));
        page.transform.SetParent(parent, false);

        RectTransform rect = page.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return page;
    }

    private static void CreateBodyText(Transform parent, Transform styleRoot, string text)
    {
        GameObject bodyObj = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObj.transform.SetParent(parent, false);

        RectTransform bodyRect = bodyObj.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        TextMeshProUGUI body = bodyObj.GetComponent<TextMeshProUGUI>();
        ApplyPanelTextStyle(styleRoot, body);
        body.fontSize = 18;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 8f;
        body.text = text;
        body.raycastTarget = false;
    }

    private static Button CreateTabButton(
    Transform parent,
    GameObject inactiveTemplate,
    GameObject activeTemplate,
    string text,
    float width,
    Action onClick)
    {
        GameObject root = new GameObject(
            $"{text}_TabButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(UltraStatsVanillaTabState)
        );
        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, TabHeight);

        Image hitImage = root.GetComponent<Image>();
        hitImage.color = new Color(0f, 0f, 0f, 0f);
        hitImage.raycastTarget = true;

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
        layout.preferredHeight = TabHeight;
        layout.minHeight = TabHeight;
        layout.flexibleWidth = 0f;

        GameObject inactiveVisual = CloneVanillaButtonVisual(inactiveTemplate, root.transform, "InactiveVisual", text);
        GameObject activeVisual = CloneVanillaButtonVisual(activeTemplate, root.transform, "ActiveVisual", text);

        RectTransform inactiveRect = inactiveVisual.GetComponent<RectTransform>();
        inactiveRect.anchorMin = Vector2.zero;
        inactiveRect.anchorMax = Vector2.one;
        inactiveRect.offsetMin = Vector2.zero;
        inactiveRect.offsetMax = Vector2.zero;

        RectTransform activeRect = activeVisual.GetComponent<RectTransform>();
        activeRect.anchorMin = Vector2.zero;
        activeRect.anchorMax = Vector2.one;
        activeRect.offsetMin = Vector2.zero;
        activeRect.offsetMax = Vector2.zero;

        UltraStatsVanillaTabState state = root.GetComponent<UltraStatsVanillaTabState>();
        state.InactiveVisual = inactiveVisual;
        state.ActiveVisual = activeVisual;

        Button button = root.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());

        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        return button;
    }

    private static GameObject CloneVanillaButtonVisual(
        GameObject template,
        Transform parent,
        string name,
        string text)
    {
        GameObject clone = UnityEngine.Object.Instantiate(template, parent, false);
        clone.name = name;

        foreach (Graphic graphic in clone.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        foreach (Button button in clone.GetComponentsInChildren<Button>(true))
            button.enabled = false;

        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour.GetType().Name == "HudOpenEffect")
                behaviour.enabled = false;
        }

        SetButtonLabelText(clone, text);
        return clone;
    }

    private static void ResolveButtonTemplates(
        Transform leftSide,
        out GameObject inactiveTemplate,
        out GameObject activeTemplate)
    {
        inactiveTemplate = FindNamedDirectChild(leftSide, "Options") ?? FindFirstButtonTemplate(leftSide)
            ?? throw new InvalidOperationException("Could not find a vanilla menu button template.");

        activeTemplate = FindNamedDirectChild(leftSide, "Continue") ?? inactiveTemplate;

        if (!LooksDifferent(activeTemplate, inactiveTemplate))
        {
            foreach (Transform child in leftSide)
            {
                if (child.gameObject == inactiveTemplate)
                    continue;

                if (child.GetComponent<Button>() == null || child.GetComponent<Image>() == null)
                    continue;

                if (LooksDifferent(child.gameObject, inactiveTemplate))
                {
                    activeTemplate = child.gameObject;
                    break;
                }
            }
        }

        BepInExLogs_US.Debug($"Inactive template = {inactiveTemplate.name}, active template = {activeTemplate.name}");
    }

    private static bool LooksDifferent(GameObject a, GameObject b)
    {
        Image? ai = a.GetComponent<Image>();
        Image? bi = b.GetComponent<Image>();
        if (ai != null && bi != null)
        {
            if (ai.sprite != bi.sprite)
                return true;

            if (ai.color != bi.color)
                return true;

            if (Math.Abs(ai.pixelsPerUnitMultiplier - bi.pixelsPerUnitMultiplier) > 0.001f)
                return true;
        }

        Graphic? ag = FindLabelGraphic(a);
        Graphic? bg = FindLabelGraphic(b);
        if (ag != null && bg != null && ag.color != bg.color)
            return true;

        return false;
    }

    private static GameObject? FindNamedDirectChild(Transform parent, string name)
    {
        Transform? child = parent.Find(name);
        return child != null ? child.gameObject : null;
    }

    private static GameObject? FindFirstButtonTemplate(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child.GetComponent<Button>() != null && child.GetComponent<Image>() != null)
                return child.gameObject;
        }

        return null;
    }

    private static Graphic? FindLabelGraphic(GameObject root)
    {
        TMP_Text? tmp = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if (tmp != null)
            return tmp;

        Text? text = root.GetComponentsInChildren<Text>(true).FirstOrDefault();
        return text;
    }

    private static void SetButtonLabelText(GameObject root, string value)
    {
        TMP_Text? tmp = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if (tmp != null)
        {
            tmp.text = value;
            return;
        }

        Text? text = root.GetComponentsInChildren<Text>(true).FirstOrDefault();
        if (text != null)
            text.text = value;
    }

    internal static void ApplyPanelTextStyle(Transform root, TextMeshProUGUI target)
    {
        TextMeshProUGUI? source = root
            .GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(t =>
            {
                string s = (t.text ?? string.Empty).Trim().ToUpperInvariant();
                return s == "PLAY" || s == "CONTINUE" || s == "OPTIONS" || s == "CREDITS" || s == "QUIT";
            });

        if (source != null)
        {
            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.color = source.color;
            target.characterSpacing = source.characterSpacing;
        }

        target.enableWordWrapping = true;
    }

    private static void ApplyImageStyleFromTemplate(GameObject template, Image target)
    {
        Image? source = template.GetComponent<Image>();
        if (source == null)
            return;

        target.sprite = source.sprite;
        target.overrideSprite = source.overrideSprite;
        target.type = source.type;
        target.material = source.material;
        target.color = source.color;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillOrigin = source.fillOrigin;
        target.fillClockwise = source.fillClockwise;
        target.fillAmount = source.fillAmount;
        target.preserveAspect = source.preserveAspect;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.useSpriteMesh = source.useSpriteMesh;
        target.maskable = source.maskable;
    }

    private static void Inject(Transform leftSide, Transform mainMenu)
    {
        if (mainMenu.Find(ButtonObjectName) != null)
        {
            BepInExLogs_US.Debug("ULTRASTATS button already exists.");
            return;
        }

        ResolveButtonTemplates(leftSide, out GameObject inactiveTemplate, out _);

        GameObject buttonObj = new GameObject(
            ButtonObjectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObj.transform.SetParent(mainMenu, false);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(96f, 96f);
        ApplyButtonPosition(rect);

        Image bg = buttonObj.GetComponent<Image>();
        ApplyImageStyleFromTemplate(inactiveTemplate, bg);
        bg.raycastTarget = true;

        Button button = buttonObj.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => TogglePanel(mainMenu));

        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        GameObject iconObj = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        iconObj.transform.SetParent(buttonObj.transform, false);

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.14f, 0.14f);
        iconRect.anchorMax = new Vector2(0.86f, 0.86f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image icon = iconObj.GetComponent<Image>();
        icon.sprite = LoadButtonSprite();
        icon.type = Image.Type.Simple;
        icon.material = null;
        icon.preserveAspect = true;
        icon.color = Color.white;
        icon.raycastTarget = false;

        if (mainMenu.Find(PanelObjectName) == null)
            BuildPanel(mainMenu);

        BepInExLogs_US.Debug("ULTRASTATS menu button injected.");
    }

    private static Sprite? LoadButtonSprite() => LoadSpriteFromPluginFile("images/sigma.png");

    internal static Sprite? LoadSpriteFromPluginFile(string fileName)
    {
        try
        {
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string spritePath = Path.Combine(pluginDir, fileName);

            if (!File.Exists(spritePath))
            {
                BepInExLogs_US.Warn($"{fileName} not found at: {spritePath}");
                return null;
            }

            byte[] data = File.ReadAllBytes(spritePath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            if (!ImageConversion.LoadImage(tex, data, false))
            {
                BepInExLogs_US.Warn($"Failed to load {fileName} into Texture2D.");
                return null;
            }

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to create sprite from {fileName}: {ex}");
            return null;
        }
    }
}

internal sealed class UltraStatsMenuPanelCloser : MonoBehaviour
{
    private void Update()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        {
            gameObject.SetActive(false);

            Transform? blocker = transform.parent != null
                ? transform.parent.Find("ULTRASTATS_MenuBlocker")
                : null;

            if (blocker != null)
                blocker.gameObject.SetActive(false);
        }
    }
}

internal sealed class UltraStatsVanillaTabState : MonoBehaviour
{
    public GameObject InactiveVisual = null!;
    public GameObject ActiveVisual = null!;

    public void SetActiveState(bool active)
    {
        if (InactiveVisual != null)
            InactiveVisual.SetActive(!active);

        if (ActiveVisual != null)
            ActiveVisual.SetActive(active);
    }
}

internal sealed class UltraStatsTabbedPanelController : MonoBehaviour
{
    private readonly List<Button> _tabButtons = new();
    private readonly List<GameObject> _tabPages = new();

    public void AddTab(Button button, GameObject page)
    {
        _tabButtons.Add(button);
        _tabPages.Add(page);
    }

    public void ShowTab(int index)
    {
        for (int i = 0; i < _tabPages.Count; i++)
        {
            bool active = i == index;
            _tabPages[i].SetActive(active);

            UltraStatsVanillaTabState? state = _tabButtons[i].GetComponent<UltraStatsVanillaTabState>();
            if (state != null)
                state.SetActiveState(active);
        }
    }
}