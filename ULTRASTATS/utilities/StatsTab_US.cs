using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Eventually I will move a lot of this code into a shared .cs with the PLOTS tab.

namespace ULTRASTATS;

internal static class StatsTab_US
{
    public static void Build(Transform parent, Transform styleRoot)
    {
        GameObject root = new GameObject(
            "StatsTabRoot",
            typeof(RectTransform),
            typeof(StatsTabController_US)
        );
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        StatsTabController_US controller = root.GetComponent<StatsTabController_US>();
        controller.Build(styleRoot);
    }
}

internal sealed class StatsDropdown_US : MonoBehaviour
{
    private static StatsDropdown_US? _openDropdown;

    private readonly List<string> _options = new();
    private readonly List<Image> _itemImages = new();

    private Transform _styleRoot = null!;
    private RectTransform _rootRect = null!;
    private Button _rootButton = null!;
    private TextMeshProUGUI _captionText = null!;
    private RectTransform _popupRect = null!;
    private RectTransform _contentRect = null!;
    private ScrollRect _popupScrollRect = null!;
    private RectTransform _overlayRect = null!;

    private Color _itemNormalColor;
    private Color _itemSelectedColor;
    private Color _itemHoverColor;
    private int _value;
    public int MaxVisibleItems = 4;

    public event Action<int>? OnValueChanged;

    public int value => _value;
    public IReadOnlyList<string> Options => _options;
    public string SelectedText => _options.Count == 0 ? "-" : _options[Mathf.Clamp(_value, 0, _options.Count - 1)];

    public void Initialize(
        Transform styleRoot,
        Button rootButton,
        TextMeshProUGUI captionText,
        RectTransform popupRect,
        RectTransform contentRect,
        ScrollRect popupScrollRect,
        RectTransform overlayRect,
        Color itemNormalColor,
        Color itemSelectedColor,
        Color itemHoverColor)
    {
        _styleRoot = styleRoot;
        _rootRect = (RectTransform)transform;
        _rootButton = rootButton;
        _captionText = captionText;
        _popupRect = popupRect;
        _contentRect = contentRect;
        _popupScrollRect = popupScrollRect;
        _overlayRect = overlayRect;
        _itemNormalColor = itemNormalColor;
        _itemSelectedColor = itemSelectedColor;
        _itemHoverColor = itemHoverColor;

        _rootButton.onClick.AddListener(TogglePopup);
        HidePopupImmediate();
    }

    private void Update()
    {
        if (_popupRect == null || !_popupRect.gameObject.activeSelf)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        bool insideRoot = RectTransformUtility.RectangleContainsScreenPoint(_rootRect, Input.mousePosition, null);
        bool insidePopup = RectTransformUtility.RectangleContainsScreenPoint(_popupRect, Input.mousePosition, null);

        if (!insideRoot && !insidePopup)
            HidePopupImmediate();
    }

    private void OnDisable()
    {
        HidePopupImmediate();
    }

    public void SetOptions(IEnumerable<string> values, int defaultIndex)
    {
        foreach (Transform child in _contentRect)
            Destroy(child.gameObject);

        _itemImages.Clear();
        _options.Clear();

        foreach (string value in values)
            _options.Add(value);

        if (_options.Count == 0)
            _options.Add("-");

        for (int i = 0; i < _options.Count; i++)
            CreateOptionButton(i, _options[i]);

        SetValueWithoutNotify(Mathf.Clamp(defaultIndex, 0, _options.Count - 1));
        RefreshShownValue();
        RefreshPopupHeight();
    }

    public void SetValueWithoutNotify(int index)
    {
        if (_options.Count == 0)
        {
            _value = 0;
            _captionText.text = "-";
            return;
        }

        _value = Mathf.Clamp(index, 0, _options.Count - 1);
        RefreshShownValue();
        RefreshSelectionVisuals();
    }

    public void RefreshShownValue()
    {
        _captionText.text = SelectedText;
    }

    private void TogglePopup()
    {
        bool show = !_popupRect.gameObject.activeSelf;

        if (_openDropdown != null && _openDropdown != this)
            _openDropdown.HidePopupImmediate();

        if (!show)
        {
            HidePopupImmediate();
            return;
        }

        _openDropdown = this;
        RefreshPopupHeight();
        PositionPopup();
        _popupRect.gameObject.SetActive(true);
        _popupRect.SetAsLastSibling();
        _popupScrollRect.verticalNormalizedPosition = 1f;
    }

    private void PositionPopup()
    {
        if (_overlayRect == null || _rootRect == null || _popupRect == null)
            return;

        Vector3[] corners = new Vector3[4];
        _rootRect.GetWorldCorners(corners);

        Vector3 bottomLeft = corners[0];

        float width = Mathf.Max(1f, _rootRect.rect.width);
        _popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

        _popupRect.position = new Vector3(bottomLeft.x, bottomLeft.y - 2f, bottomLeft.z);
    }

    private void HidePopupImmediate()
    {
        if (_openDropdown == this)
            _openDropdown = null;

        if (_popupRect != null)
            _popupRect.gameObject.SetActive(false);
    }

    private void OnOptionClicked(int index)
    {
        SetValueWithoutNotify(index);
        HidePopupImmediate();
        OnValueChanged?.Invoke(_value);
    }

    private void RefreshPopupHeight()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        Canvas.ForceUpdateCanvases();

        const float rowHeight = 28f;
        int visibleItems = Mathf.Min(Mathf.Max(1, MaxVisibleItems), Mathf.Max(1, _options.Count));
        float spacing = Mathf.Max(0f, _contentRect.GetComponent<VerticalLayoutGroup>()?.spacing ?? 0f);
        float viewportHeight = visibleItems * rowHeight + Mathf.Max(0f, visibleItems - 1) * spacing;

        _popupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight + 4f);
        _popupScrollRect.vertical = _options.Count > visibleItems;
    }

    private void RefreshSelectionVisuals()
    {
        for (int i = 0; i < _itemImages.Count; i++)
        {
            if (_itemImages[i] != null)
                _itemImages[i].color = i == _value ? _itemSelectedColor : _itemNormalColor;
        }
    }

    private void CreateOptionButton(int index, string value)
    {
        GameObject item = new GameObject(
            "Item_" + index,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement)
        );
        item.transform.SetParent(_contentRect, false);

        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);
        itemRect.sizeDelta = new Vector2(0f, 28f);

        Image itemImage = item.GetComponent<Image>();
        itemImage.color = _itemNormalColor;
        itemImage.raycastTarget = true;
        _itemImages.Add(itemImage);

        LayoutElement itemLayout = item.GetComponent<LayoutElement>();
        itemLayout.preferredHeight = 28f;
        itemLayout.minHeight = 28f;

        Button itemButton = item.GetComponent<Button>();
        itemButton.transition = Selectable.Transition.ColorTint;
        itemButton.targetGraphic = itemImage;

        ColorBlock colors = itemButton.colors;
        colors.normalColor = _itemNormalColor;
        colors.highlightedColor = _itemHoverColor;
        colors.pressedColor = _itemHoverColor;
        colors.selectedColor = _itemHoverColor;
        colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
        itemButton.colors = colors;
        itemButton.onClick.AddListener(() => OnOptionClicked(index));

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(item.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
        MainMenuButton_US.ApplyPanelTextStyle(_styleRoot, label);
        label.text = value;
        label.fontSize = 16f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
    }
}

internal sealed class StatsTabController_US : MonoBehaviour
{
    private enum StatsMode
    {
        Cybergrind = 0,
        Campaign = 1,
        Custom = 2
    }

    private sealed class ChartDefinition
    {
        public string[] Headers = Array.Empty<string>();
        public float[] WidthFractions = Array.Empty<float>();
    }

    private sealed class StatsRowData
    {
        public long SortId;
        public string Version = "-";
        public string[] Cells = Array.Empty<string>();

        public string TimeRank = string.Empty;
        public string KillsRank = string.Empty;
        public string StyleRank = string.Empty;
        public string TotalRank = string.Empty;
    }

    [Serializable]
    private sealed class AngryLevelCatalogData
    {
        public AngryLevelCatalogBundle[] Levels = Array.Empty<AngryLevelCatalogBundle>();
    }

    [Serializable]
    private sealed class AngryLevelCatalogBundle
    {
        public string Name = string.Empty;
        public string Guid = string.Empty;
        public string Hash = string.Empty;

        public string buildHash = string.Empty;
        public string bundleGuid = string.Empty;
        public string bundleDataPath = string.Empty;

        public AngryLevelCatalogLevel[] Levels = Array.Empty<AngryLevelCatalogLevel>();
    }

    [Serializable]
    private sealed class AngryLocalBundleData
    {
        public string bundleName = string.Empty;
        public string bundleAuthor = string.Empty;
        public string bundleGuid = string.Empty;
        public string buildHash = string.Empty;
        public string bundleDataPath = string.Empty;
    }

    [Serializable]
    private sealed class AngryLevelCatalogLevel
    {
        public string LevelName = string.Empty;
        public string LevelId = string.Empty;
    }

    private sealed class CustomBundleCatalogEntry
    {
        public string Guid = string.Empty;
        public string Hash = string.Empty;
        public string DisplayName = string.Empty;
        public readonly Dictionary<string, string> LevelDisplayNames = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> LevelOrder = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CustomLevelFileInfo
    {
        public string DisplayName = string.Empty;
        public string Path = string.Empty;
        public int CatalogOrder = int.MaxValue;
    }

    private const float HeaderHeight = 46f;
    private const float RowHeight = 42f;
    private const float WheelPixelsPerTick = 96f;
    private const float LeftPaneWidth = 300f;
    private const float LeftPaneToDividerGap = 14f;
    private const float DividerToRightPaneGap = 20f;
    private const float RowsAreaRightInset = 30f;
    private const int LoadBatchSize = 32;

    private static readonly Color DividerColor = new Color(0.70f, 0.70f, 0.70f, 1f);
    private static readonly Color HeaderColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color RowColorEven = new Color(0f, 0f, 0f, 0.96f);
    private static readonly Color RowColorOdd = new Color(0.13f, 0.13f, 0.13f, 0.96f);
    private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color DropdownFillColor = new Color(0f, 0f, 0f, 0.92f);
    private static readonly Color DropdownPopupColor = new Color(0f, 0f, 0f, 0.98f);
    private static readonly Color DropdownItemColor = new Color(0.10f, 0.10f, 0.10f, 1f);
    private static readonly Color DropdownItemHoverColor = new Color(0.22f, 0.22f, 0.22f, 1f);

    private static readonly ChartDefinition CybergrindChart = new ChartDefinition
    {
        Headers = new[] { "ID", "Date", "Time", "Kills", "Style", "Wave" },
        WidthFractions = new[] { 0.08f, 0.31f, 0.18f, 0.10f, 0.18f, 0.15f }
    };

    private static readonly ChartDefinition StandardChart = new ChartDefinition
    {
        Headers = new[] { "ID", "Date", "Time", "Kills", "Style", "Deaths", "Rank" },
        WidthFractions = new[] { 0.08f, 0.28f, 0.18f, 0.10f, 0.15f, 0.11f, 0.10f }
    };

    private Transform _styleRoot = null!;
    private RectTransform _controlsStackRect = null!;
    private RectTransform _dropdownOverlayRect = null!;
    private StatsDropdown_US _difficultyDropdown = null!;
    private StatsDropdown_US _modeDropdown = null!;
    private StatsDropdown_US _campaignLayerDropdown = null!;
    private StatsDropdown_US _campaignLevelDropdown = null!;
    private StatsDropdown_US _customBundleDropdown = null!;
    private StatsDropdown_US _customLevelDropdown = null!;
    private GameObject _campaignLayerGroup = null!;
    private GameObject _campaignLevelGroup = null!;
    private GameObject _customBundleGroup = null!;
    private GameObject _customLevelGroup = null!;
    private TextMeshProUGUI _versionsText = null!;
    private TextMeshProUGUI _statusText = null!;
    private RectTransform _headerRect = null!;
    private RectTransform _rowsViewportRect = null!;
    private RectTransform _rowsContentRect = null!;
    private ScrollRect _rowsScrollRect = null!;
    private UltraStatsScrollbarDrag _rowsScrollbarDrag = null!;
    private readonly List<GameObject> _spawnedRows = new();
    private Coroutine? _loadCoroutine;
    private int _loadGeneration;

    private string[] _difficultyOptions = Array.Empty<string>();
    private string[] _difficultyPaths = Array.Empty<string>();
    private string[] _campaignLayerOptions = Array.Empty<string>();
    private string[] _campaignLayerPaths = Array.Empty<string>();
    private string[] _campaignLevelOptions = Array.Empty<string>();
    private string[] _campaignLevelPaths = Array.Empty<string>();
    private string[] _customBundleOptions = Array.Empty<string>();
    private string[] _customBundlePaths = Array.Empty<string>();
    private string[] _customLevelOptions = Array.Empty<string>();
    private string[] _customLevelPaths = Array.Empty<string>();
    private readonly Dictionary<string, CustomBundleCatalogEntry> _customBundleCatalog = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _bundleLastPlayedUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AngryLevelCatalogBundle> _angryBundleById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _angryLastPlayedById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _localBundleDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _angryBundleCacheLoaded;
    private bool _hasBuiltUi;

    public void Build(Transform styleRoot)
    {
        _styleRoot = styleRoot;
        LoadCustomBundleMetadata();
        BuildLayout();
        PopulateModeDropdown();
        RefreshDifficultyOptions();
        ApplyConfiguredDifficultySelection(false);
        RefreshAllDependentSelectors();
        RefreshVisibleControls();
        ReloadTable();
        _hasBuiltUi = true;
    }

    private void OnEnable()
    {
        if (!_hasBuiltUi)
            return;

        RefreshAllDependentSelectors();
        RefreshVisibleControls();
        ReloadTable();
    }

    private void OnDisable()
    {
        StopLoadingRoutine();
    }

    private void BuildLayout()
    {
        GameObject leftPane = CreateRect("LeftPane", transform);
        RectTransform leftRect = leftPane.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0f, 1f);
        leftRect.pivot = new Vector2(0f, 0.5f);
        leftRect.sizeDelta = new Vector2(LeftPaneWidth, 0f);
        leftRect.anchoredPosition = Vector2.zero;

        GameObject divider = CreateImage("CenterDivider", transform, DividerColor, false);
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 0f);
        dividerRect.anchorMax = new Vector2(0f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(2f, 0f);
        dividerRect.anchoredPosition = new Vector2(LeftPaneWidth + LeftPaneToDividerGap, 0f);

        GameObject rightPane = CreateRect("RightPane", transform);
        RectTransform rightRect = rightPane.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.offsetMin = new Vector2(LeftPaneWidth + LeftPaneToDividerGap + DividerToRightPaneGap, 0f);
        rightRect.offsetMax = Vector2.zero;

        GameObject dropdownOverlay = new GameObject(
            "DropdownOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster)
        );
        dropdownOverlay.transform.SetParent(transform, false);
        _dropdownOverlayRect = dropdownOverlay.GetComponent<RectTransform>();
        _dropdownOverlayRect.anchorMin = Vector2.zero;
        _dropdownOverlayRect.anchorMax = Vector2.one;
        _dropdownOverlayRect.offsetMin = Vector2.zero;
        _dropdownOverlayRect.offsetMax = Vector2.zero;
        _dropdownOverlayRect.SetAsLastSibling();

        Canvas overlayCanvas = dropdownOverlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 600;

        BuildControls(leftRect);
        BuildChartArea(rightRect);
    }

    private void BuildControls(RectTransform parent)
    {
        GameObject stackObj = CreateRect("ControlsStack", parent);
        _controlsStackRect = stackObj.GetComponent<RectTransform>();
        _controlsStackRect.anchorMin = Vector2.zero;
        _controlsStackRect.anchorMax = Vector2.one;
        _controlsStackRect.offsetMin = Vector2.zero;
        _controlsStackRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup stackLayout = stackObj.AddComponent<VerticalLayoutGroup>();
        stackLayout.padding = new RectOffset(0, 0, 0, 0);
        stackLayout.spacing = 8f;
        stackLayout.childAlignment = TextAnchor.UpperLeft;
        stackLayout.childControlWidth = true;
        stackLayout.childControlHeight = true;
        stackLayout.childForceExpandWidth = true;
        stackLayout.childForceExpandHeight = false;

        _difficultyDropdown = CreateDropdownGroup(_controlsStackRect, "Difficulty", out _);
        _difficultyDropdown.OnValueChanged += _ =>
        {
            RefreshAllDependentSelectors();
            RefreshVisibleControls();
            ReloadTable();
        };

        _modeDropdown = CreateDropdownGroup(_controlsStackRect, "Mode", out _);
        _modeDropdown.OnValueChanged += _ =>
        {
            RefreshAllDependentSelectors();
            RefreshVisibleControls();
            ReloadTable();
        };

        _campaignLayerDropdown = CreateDropdownGroup(_controlsStackRect, "Campaign Layer", out _campaignLayerGroup);
        _campaignLayerDropdown.MaxVisibleItems = 8;
        _campaignLayerDropdown.OnValueChanged += _ =>
        {
            RefreshCampaignLevelOptions();
            ReloadTable();
        };

        _campaignLevelDropdown = CreateDropdownGroup(_controlsStackRect, "Campaign Level", out _campaignLevelGroup);
        _campaignLevelDropdown.OnValueChanged += _ => ReloadTable();

        _customBundleDropdown = CreateDropdownGroup(_controlsStackRect, "Custom Bundle", out _customBundleGroup);
        _customBundleDropdown.MaxVisibleItems = 8;
        _customBundleDropdown.OnValueChanged += _ =>
        {
            RefreshCustomLevelOptions();
            ReloadTable();
        };

        _customLevelDropdown = CreateDropdownGroup(_controlsStackRect, "Custom Level", out _customLevelGroup);
        _customLevelDropdown.OnValueChanged += _ => ReloadTable();

        _versionsText = CreateSection(_controlsStackRect, "Stored Version(s)", 60f, false, out GameObject versionsRoot);
        versionsRoot.SetActive(false);
        _versionsText.fontSize = 18f;
        _versionsText.enableWordWrapping = true;
        _versionsText.overflowMode = TextOverflowModes.Overflow;

        _statusText = CreateSection(_controlsStackRect, "Status", 0f, true, out _);
        _statusText.fontSize = 17f;
        _statusText.enableWordWrapping = true;
        _statusText.overflowMode = TextOverflowModes.Overflow;
    }

    private void BuildChartArea(RectTransform parent)
    {
        GameObject headerRoot = CreateRect("ChartHeader", parent);
        _headerRect = headerRoot.GetComponent<RectTransform>();
        _headerRect.anchorMin = new Vector2(0f, 1f);
        _headerRect.anchorMax = new Vector2(1f, 1f);
        _headerRect.pivot = new Vector2(0.5f, 1f);
        _headerRect.offsetMin = new Vector2(0f, -HeaderHeight);
        _headerRect.offsetMax = new Vector2(-RowsAreaRightInset, 0f);

        GameObject viewport = new GameObject(
            "RowsViewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D),
            typeof(UltraStatsFixedWheelScroll)
        );
        viewport.transform.SetParent(parent, false);
        _rowsViewportRect = viewport.GetComponent<RectTransform>();
        _rowsViewportRect.anchorMin = new Vector2(0f, 0f);
        _rowsViewportRect.anchorMax = new Vector2(1f, 1f);
        _rowsViewportRect.offsetMin = Vector2.zero;
        _rowsViewportRect.offsetMax = new Vector2(-RowsAreaRightInset, -HeaderHeight - 8f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = CreateRect("RowsContent", viewport.transform);
        _rowsContentRect = content.GetComponent<RectTransform>();
        _rowsContentRect.anchorMin = new Vector2(0f, 1f);
        _rowsContentRect.anchorMax = new Vector2(1f, 1f);
        _rowsContentRect.pivot = new Vector2(0.5f, 1f);
        _rowsContentRect.anchoredPosition = Vector2.zero;
        _rowsContentRect.sizeDelta = new Vector2(0f, 0f);

        GameObject trackObj = new GameObject(
            "RowsScrollbarTrack",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UltraStatsScrollbarDrag),
            typeof(UltraStatsFixedWheelScroll)
        );
        trackObj.transform.SetParent(parent, false);

        RectTransform trackRect = trackObj.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = new Vector2(1f, 1f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(18f, 0f);
        trackRect.anchoredPosition = Vector2.zero;

        Image trackImage = trackObj.GetComponent<Image>();
        trackImage.color = TrackColor;
        trackImage.raycastTarget = true;

        GameObject handleObj = CreateImage("Handle", trackObj.transform, Color.white, false);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(1f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(0f, 96f);
        handleRect.anchoredPosition = Vector2.zero;

        _rowsScrollRect = parent.gameObject.AddComponent<ScrollRect>();
        _rowsScrollRect.content = _rowsContentRect;
        _rowsScrollRect.viewport = _rowsViewportRect;
        _rowsScrollRect.horizontal = false;
        _rowsScrollRect.vertical = true;
        _rowsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _rowsScrollRect.scrollSensitivity = 0f;
        _rowsScrollRect.inertia = false;

        _rowsScrollbarDrag = trackObj.GetComponent<UltraStatsScrollbarDrag>();
        _rowsScrollbarDrag.ScrollRect = _rowsScrollRect;
        _rowsScrollbarDrag.TrackRect = trackRect;
        _rowsScrollbarDrag.HandleRect = handleRect;
        _rowsScrollbarDrag.MinHandleHeight = 72f;

        UltraStatsFixedWheelScroll viewportWheel = viewport.GetComponent<UltraStatsFixedWheelScroll>();
        viewportWheel.ScrollRect = _rowsScrollRect;
        viewportWheel.PixelsPerWheelTick = WheelPixelsPerTick;

        UltraStatsFixedWheelScroll trackWheel = trackObj.GetComponent<UltraStatsFixedWheelScroll>();
        trackWheel.ScrollRect = _rowsScrollRect;
        trackWheel.PixelsPerWheelTick = WheelPixelsPerTick;
    }

    private StatsDropdown_US CreateDropdownGroup(RectTransform parent, string label, out GameObject groupRoot)
    {
        groupRoot = CreateRect(label.Replace(" ", string.Empty) + "Group", parent);
        LayoutElement groupLayout = groupRoot.AddComponent<LayoutElement>();
        groupLayout.preferredHeight = 54f;
        groupLayout.minHeight = 54f;

        VerticalLayoutGroup groupStack = groupRoot.AddComponent<VerticalLayoutGroup>();
        groupStack.padding = new RectOffset(0, 0, 0, 0);
        groupStack.spacing = 3f;
        groupStack.childAlignment = TextAnchor.UpperLeft;
        groupStack.childControlWidth = true;
        groupStack.childControlHeight = true;
        groupStack.childForceExpandWidth = true;
        groupStack.childForceExpandHeight = false;

        GameObject labelObj = CreateText(label.Replace(" ", string.Empty) + "Label", groupRoot.transform, label, 20f, TextAlignmentOptions.TopLeft);
        LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 18f;
        labelLayout.minHeight = 18f;

        return CreateDropdown(groupRoot.transform);
    }

    private StatsDropdown_US CreateDropdown(Transform parent)
    {
        GameObject root = new GameObject(
            "Dropdown",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(StatsDropdown_US),
            typeof(LayoutElement)
        );
        root.transform.SetParent(parent, false);

        LayoutElement rootLayout = root.GetComponent<LayoutElement>();
        rootLayout.preferredHeight = 30f;
        rootLayout.minHeight = 30f;

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = DropdownFillColor;
        rootImage.raycastTarget = true;
        CreateOutline(root.transform, 2f, Color.white);

        Button rootButton = root.GetComponent<Button>();
        rootButton.transition = Selectable.Transition.None;

        GameObject captionObj = CreateText("Caption", root.transform, string.Empty, 17f, TextAlignmentOptions.MidlineLeft);
        RectTransform captionRect = captionObj.GetComponent<RectTransform>();
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.offsetMin = new Vector2(9f, 1f);
        captionRect.offsetMax = new Vector2(-28f, -1f);

        TextMeshProUGUI captionText = captionObj.GetComponent<TextMeshProUGUI>();
        captionText.enableWordWrapping = false;
        captionText.overflowMode = TextOverflowModes.Ellipsis;

        GameObject arrowObj = CreateText("Arrow", root.transform, "▼", 14f, TextAlignmentOptions.Center);
        RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(18f, 0f);
        arrowRect.anchoredPosition = new Vector2(-7f, 0f);

        GameObject popupRoot = new GameObject(
            "Popup",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(ScrollRect)
        );
        popupRoot.transform.SetParent(_dropdownOverlayRect, false);
        popupRoot.SetActive(false);

        RectTransform popupRect = popupRoot.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0f, 1f);
        popupRect.anchorMax = new Vector2(0f, 1f);
        popupRect.pivot = new Vector2(0f, 1f);
        popupRect.sizeDelta = new Vector2(0f, 0f);
        popupRect.anchoredPosition = Vector2.zero;

        Image popupImage = popupRoot.GetComponent<Image>();
        popupImage.color = DropdownPopupColor;
        popupImage.raycastTarget = true;
        CreateOutline(popupRoot.transform, 2f, Color.white);

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D)
        );
        viewport.transform.SetParent(popupRoot.transform, false);

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(2f, 2f);
        viewportRect.offsetMax = new Vector2(-2f, -2f);

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        );
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.spacing = 1f;
        contentLayout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect popupScrollRect = popupRoot.GetComponent<ScrollRect>();
        popupScrollRect.content = contentRect;
        popupScrollRect.viewport = viewportRect;
        popupScrollRect.horizontal = false;
        popupScrollRect.vertical = true;
        popupScrollRect.movementType = ScrollRect.MovementType.Clamped;
        popupScrollRect.scrollSensitivity = 24f;
        popupScrollRect.inertia = false;

        StatsDropdown_US dropdown = root.GetComponent<StatsDropdown_US>();
        dropdown.Initialize(
            _styleRoot,
            rootButton,
            captionText,
            popupRect,
            contentRect,
            popupScrollRect,
            _dropdownOverlayRect,
            DropdownItemColor,
            DropdownItemHoverColor,
            DropdownItemHoverColor
        );

        return dropdown;
    }

    private TextMeshProUGUI CreateSection(RectTransform parent, string label, float preferredHeight, bool flexibleHeight, out GameObject root)
    {
        root = CreateRect(label.Replace(" ", string.Empty) + "Section", parent);
        LayoutElement rootLayout = root.AddComponent<LayoutElement>();
        rootLayout.preferredHeight = preferredHeight;
        rootLayout.minHeight = flexibleHeight ? 0f : preferredHeight;
        rootLayout.flexibleHeight = flexibleHeight ? 1f : 0f;

        VerticalLayoutGroup stack = root.AddComponent<VerticalLayoutGroup>();
        stack.padding = new RectOffset(0, 0, 0, 0);
        stack.spacing = 4f;
        stack.childAlignment = TextAnchor.UpperLeft;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        GameObject labelObj = CreateText(label.Replace(" ", string.Empty) + "Label", root.transform, label, 20f, TextAlignmentOptions.TopLeft);
        LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = 18f;
        labelLayout.minHeight = 18f;

        GameObject valueObj = CreateText(label.Replace(" ", string.Empty) + "Value", root.transform, "-", 18f, TextAlignmentOptions.TopLeft);
        LayoutElement valueLayout = valueObj.AddComponent<LayoutElement>();
        valueLayout.preferredHeight = flexibleHeight ? 80f : Mathf.Max(28f, preferredHeight - 24f);
        valueLayout.minHeight = 28f;
        valueLayout.flexibleHeight = flexibleHeight ? 1f : 0f;

        TextMeshProUGUI valueText = valueObj.GetComponent<TextMeshProUGUI>();
        valueText.enableWordWrapping = true;
        valueText.overflowMode = TextOverflowModes.Overflow;

        return valueText;
    }

    private void PopulateModeDropdown()
    {
        SetDropdownOptions(_modeDropdown, new[] { "Cybergrind", "Campaign", "Custom" }, "Cybergrind");
    }

    private void RefreshDifficultyOptions()
    {
        string previouslySelected = _difficultyDropdown != null ? _difficultyDropdown.SelectedText : string.Empty;
        List<string> displayNames = new();
        List<string> paths = new();

        try
        {
            string basePath = Plugin.DataFolderPath;
            if (Directory.Exists(basePath))
            {
                foreach (string dir in Directory.GetDirectories(basePath, "Difficulty_*", SearchOption.TopDirectoryOnly).OrderBy(NaturalDifficultyOrder))
                {
                    string? folderName = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(folderName))
                        continue;

                    displayNames.Add(ToDifficultyDisplayName(folderName));
                    paths.Add(dir);
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to enumerate difficulty folders: {ex.Message}");
        }

        if (displayNames.Count == 0)
        {
            displayNames.Add("None");
            paths.Add(string.Empty);
        }

        _difficultyOptions = displayNames.ToArray();
        _difficultyPaths = paths.ToArray();
        SetDropdownOptions(_difficultyDropdown, _difficultyOptions, previouslySelected);
    }

    public void ApplyConfiguredDifficultySelection(bool reloadTable)
    {
        if (_difficultyDropdown == null || _difficultyOptions.Length == 0 || _difficultyPaths.Length == 0)
            return;

        int targetDifficulty = Plugin.DefaultStatsDifficultyNumber;
        int selectedIndex = 0;

        for (int i = 0; i < _difficultyPaths.Length; i++)
        {
            string path = _difficultyPaths[i] ?? string.Empty;
            int? difficulty = TryGetDifficultyNumber(Path.GetFileName(path));
            if (difficulty == targetDifficulty)
            {
                selectedIndex = i;
                break;
            }
        }

        if (_difficultyDropdown.value != selectedIndex)
        {
            _difficultyDropdown.SetValueWithoutNotify(selectedIndex);
            _difficultyDropdown.RefreshShownValue();
        }

        if (reloadTable && isActiveAndEnabled)
        {
            RefreshAllDependentSelectors();
            RefreshVisibleControls();
            ReloadTable();
        }
    }

    private void RefreshAllDependentSelectors()
    {
        RefreshCampaignLayerOptions();
        RefreshCampaignLevelOptions();
        RefreshCustomBundleOptions();
        RefreshCustomLevelOptions();
    }

    private void RefreshCampaignLayerOptions()
    {
        string selected = _campaignLayerDropdown != null ? _campaignLayerDropdown.SelectedText : "None";
        List<string> names = new();
        List<string> paths = new();

        try
        {
            string difficultyPath = GetSelectedDifficultyPath();
            if (Directory.Exists(difficultyPath))
            {
                foreach (string dir in Directory.GetDirectories(difficultyPath, "*", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    string? name = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (name.Equals("custom", StringComparison.OrdinalIgnoreCase) || name.Equals("misc", StringComparison.OrdinalIgnoreCase))
                        continue;

                    names.Add(ToCampaignLayerDisplayName(name));
                    paths.Add(dir);
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to enumerate campaign layers: {ex.Message}");
        }

        if (names.Count == 0)
        {
            names.Add("None");
            paths.Add(string.Empty);
        }

        _campaignLayerOptions = names.ToArray();
        _campaignLayerPaths = paths.ToArray();
        SetDropdownOptions(_campaignLayerDropdown, _campaignLayerOptions, selected);
    }

    private void RefreshCampaignLevelOptions()
    {
        string selected = _campaignLevelDropdown != null ? _campaignLevelDropdown.SelectedText : "None";
        List<string> names = new();
        List<string> paths = new();

        try
        {
            string layerPath = GetSelectedCampaignLayerPath();
            if (!string.IsNullOrWhiteSpace(layerPath) && Directory.Exists(layerPath))
            {
                foreach (string file in Directory.GetFiles(layerPath, "*.jsonl", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(ToDisplayName(layerPath, file, GetSelectedDifficultyNumber()));
                    paths.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to enumerate campaign levels: {ex.Message}");
        }

        if (names.Count == 0)
        {
            names.Add("None");
            paths.Add(string.Empty);
        }

        _campaignLevelOptions = names.ToArray();
        _campaignLevelPaths = paths.ToArray();
        SetDropdownOptions(_campaignLevelDropdown, _campaignLevelOptions, selected);
    }

    private void RefreshCustomBundleOptions()
    {
        string selected = _customBundleDropdown != null ? _customBundleDropdown.SelectedText : "None";
        List<(string DisplayName, string Path, long LastPlayed, string FolderKey)> entries = new();

        try
        {
            string customRoot = Path.Combine(GetSelectedDifficultyPath(), "custom");
            if (Directory.Exists(customRoot))
            {
                foreach (string dir in Directory.GetDirectories(customRoot, "*", SearchOption.TopDirectoryOnly))
                {
                    string? folderName = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(folderName))
                        continue;

                    string displayName = GetCustomBundleDisplayName(folderName);

                    long lastPlayed = long.MinValue;
                    if (_bundleLastPlayedUtc.TryGetValue(folderName, out long directMatch))
                        lastPlayed = directMatch;
                    else
                    {
                        CustomBundleCatalogEntry? entry = TryGetCustomBundleCatalogEntry(folderName);
                        if (entry != null)
                        {
                            if (!string.IsNullOrWhiteSpace(entry.Guid) &&
                                _bundleLastPlayedUtc.TryGetValue(entry.Guid, out long guidMatch))
                                lastPlayed = guidMatch;
                            else if (!string.IsNullOrWhiteSpace(entry.Hash) &&
                                     _bundleLastPlayedUtc.TryGetValue(entry.Hash, out long hashMatch))
                                lastPlayed = hashMatch;
                        }
                    }

                    entries.Add((displayName, dir, lastPlayed, folderName));
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to enumerate custom bundles: {ex.Message}");
        }

        List<string> names = new();
        List<string> paths = new();
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries
            .OrderByDescending(e => e.LastPlayed)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.FolderKey, StringComparer.OrdinalIgnoreCase))
        {
            names.Add(MakeUniqueBundleDisplayName(entry.DisplayName, entry.FolderKey, usedNames));
            paths.Add(entry.Path);
        }

        if (names.Count == 0)
        {
            names.Add("None");
            paths.Add(string.Empty);
        }

        _customBundleOptions = names.ToArray();
        _customBundlePaths = paths.ToArray();
        SetDropdownOptions(_customBundleDropdown, _customBundleOptions, selected);
    }

    private void RefreshCustomLevelOptions()
    {
        string selected = _customLevelDropdown != null ? _customLevelDropdown.SelectedText : "None";
        List<string> names = new();
        List<string> paths = new();

        try
        {
            string bundlePath = GetSelectedCustomBundlePath();
            if (!string.IsNullOrWhiteSpace(bundlePath) && Directory.Exists(bundlePath))
            {
                IEnumerable<CustomLevelFileInfo> levels = Directory
                    .GetFiles(bundlePath, "*.jsonl", SearchOption.AllDirectories)
                    .Select(file => BuildCustomLevelFileInfo(bundlePath, file, GetSelectedDifficultyNumber()))
                    .OrderBy(info => info.CatalogOrder)
                    .ThenBy(info => info.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(info => info.Path, StringComparer.OrdinalIgnoreCase);

                foreach (CustomLevelFileInfo level in levels)
                {
                    names.Add(ToPreferredCustomLevelDropdownText(level.DisplayName));
                    paths.Add(level.Path);
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to enumerate custom levels: {ex.Message}");
        }

        if (names.Count == 0)
        {
            names.Add("None");
            paths.Add(string.Empty);
        }

        _customLevelOptions = names.ToArray();
        _customLevelPaths = paths.ToArray();
        SetDropdownOptions(_customLevelDropdown, _customLevelOptions, selected);
    }

    private void EnsureAngryBundleCacheLoaded()
    {
        if (_angryBundleCacheLoaded)
            return;

        _angryBundleCacheLoaded = true;
        _angryBundleById.Clear();
        _angryLastPlayedById.Clear();

        try
        {
            string levelCatalogPath = Path.Combine(
                Paths.ConfigPath,
                "AngryLevelLoader",
                "OnlineCache",
                "V2",
                "LevelCatalog.json"
            );

            if (File.Exists(levelCatalogPath))
            {
                string json = File.ReadAllText(levelCatalogPath);
                AngryLevelCatalogData? data = JsonUtility.FromJson<AngryLevelCatalogData>(json);

                if (data?.Levels != null)
                {
                    foreach (AngryLevelCatalogBundle bundle in data.Levels)
                    {
                        if (!string.IsNullOrWhiteSpace(bundle.Guid))
                            _angryBundleById[bundle.Guid] = bundle;

                        if (!string.IsNullOrWhiteSpace(bundle.Hash))
                            _angryBundleById[bundle.Hash] = bundle;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to read Angry LevelCatalog.json: {ex.Message}");
        }

        try
        {
            string lastPlayedPath = Path.Combine(
                Paths.ConfigPath,
                "AngryLevelLoader",
                "lastPlayedMap.txt"
            );

            if (File.Exists(lastPlayedPath))
            {
                string[] lines = File.ReadAllLines(lastPlayedPath);

                for (int i = 0; i + 1 < lines.Length; i += 2)
                {
                    string id = lines[i].Trim();
                    string timestampText = lines[i + 1].Trim();

                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (long.TryParse(timestampText, out long timestamp))
                        _angryLastPlayedById[id] = timestamp;
                }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to read Angry lastPlayedMap.txt: {ex.Message}");
        }
    }

    private AngryLevelCatalogBundle? TryGetAngryBundle(string folderName)
    {
        EnsureAngryBundleCacheLoaded();
        return _angryBundleById.TryGetValue(folderName, out AngryLevelCatalogBundle bundle)
            ? bundle
            : null;
    }

    private long GetAngryLastPlayed(string folderName)
    {
        EnsureAngryBundleCacheLoaded();
        return _angryLastPlayedById.TryGetValue(folderName, out long timestamp)
            ? timestamp
            : long.MinValue;
    }

    private static string CleanAngryDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value, "<.*?>", string.Empty, RegexOptions.Singleline).Trim();
    }

    private void RefreshVisibleControls()
    {
        StatsMode mode = GetSelectedMode();

        _campaignLayerGroup.SetActive(mode == StatsMode.Campaign);
        _campaignLevelGroup.SetActive(mode == StatsMode.Campaign);
        _customBundleGroup.SetActive(mode == StatsMode.Custom);
        _customLevelGroup.SetActive(mode == StatsMode.Custom);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_controlsStackRect);
        Canvas.ForceUpdateCanvases();
    }

    private void ReloadTable()
    {
        StopLoadingRoutine();
        _loadGeneration++;
        int generation = _loadGeneration;

        StatsMode mode = GetSelectedMode();
        ChartDefinition chart = mode == StatsMode.Cybergrind ? CybergrindChart : StandardChart;
        List<string> sourceFiles = ResolveSourceFiles(mode);
        string location = BuildSelectedLocation(mode, sourceFiles);

        BuildHeader(chart);
        BuildMessageRow(sourceFiles.Count == 0 ? "No matching runs found." : "Loading…");
        _versionsText.text = "-";
        _statusText.text = sourceFiles.Count == 0
            ? BuildStatusText(mode, 0, 0, location, false)
            : BuildStatusText(mode, 0, sourceFiles.Count, location, true);

        if (sourceFiles.Count == 0)
            return;

        _loadCoroutine = StartCoroutine(LoadRowsIncrementally(generation, mode, chart, sourceFiles, location));
    }

    private void StopLoadingRoutine()
    {
        if (_loadCoroutine != null)
        {
            StopCoroutine(_loadCoroutine);
            _loadCoroutine = null;
        }
    }

    private IEnumerator LoadRowsIncrementally(int generation, StatsMode mode, ChartDefinition chart, List<string> sourceFiles, string location)
    {
        List<StatsRowData> rows = new();
        HashSet<string> versions = new(StringComparer.OrdinalIgnoreCase);
        int rowsSinceLastRefresh = 0;

        foreach (string file in sourceFiles)
        {

            IEnumerable<string> lines;
            try
            {
                lines = File.ReadLines(file);
            }
            catch (Exception ex)
            {
                BepInExLogs_US.Warn($"Failed to read '{file}': {ex.Message}");
                continue;
            }

            foreach (string rawLine in lines)
            {
                if (generation != _loadGeneration)
                    yield break;

                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("{"))
                    continue;

                StatsRowData? row = ParseRow(mode, line);
                if (row == null)
                    continue;

                rows.Add(row);
                rowsSinceLastRefresh++;

                if (!string.IsNullOrWhiteSpace(row.Version) && row.Version != "-")
                    versions.Add(row.Version);

                if (rowsSinceLastRefresh >= LoadBatchSize)
                {
                    rows.Sort(CompareRows);
                    BuildRows(chart, rows);
                    _versionsText.text = BuildVersionsText(versions);
                    _statusText.text = BuildStatusText(mode, rows.Count, sourceFiles.Count, location, true);
                    rowsSinceLastRefresh = 0;
                    yield return null;
                }
            }

            if (generation != _loadGeneration)
                yield break;

            rows.Sort(CompareRows);
            BuildRows(chart, rows);
            _versionsText.text = BuildVersionsText(versions);
            _statusText.text = BuildStatusText(mode, rows.Count, sourceFiles.Count, location, true);
            yield return null;
        }

        if (generation != _loadGeneration)
            yield break;

        rows.Sort(CompareRows);
        if (rows.Count == 0)
            BuildMessageRow("No matching runs found.");
        else
            BuildRows(chart, rows);

        _versionsText.text = BuildVersionsText(versions);
        _statusText.text = BuildStatusText(mode, rows.Count, sourceFiles.Count, location, false);
        _loadCoroutine = null;
    }

    private void BuildHeader(ChartDefinition chart)
    {
        for (int i = _headerRect.childCount - 1; i >= 0; i--)
            Destroy(_headerRect.GetChild(i).gameObject);

        GameObject background = CreateImage("HeaderBackground", _headerRect, HeaderColor, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        CreateBottomLine(_headerRect, HeaderHeight);
        CreateColumnSeparators(_headerRect, chart.WidthFractions, HeaderHeight);

        float start = 0f;
        for (int i = 0; i < chart.Headers.Length; i++)
        {
            float width = chart.WidthFractions[i];
            CreateCellText(_headerRect, chart.Headers[i], start, start + width, 0f, HeaderHeight, 17f, FontStyles.Bold, TextAlignmentOptions.Center);
            start += width;
        }
    }

    private void BuildMessageRow(string message)
    {
        ClearSpawnedRows();
        float height = Mathf.Max(_rowsViewportRect.rect.height, RowHeight);
        _rowsContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        GameObject rowObj = CreateImage("MessageRow", _rowsContentRect, RowColorEven, false);
        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, RowHeight);
        rowRect.anchoredPosition = Vector2.zero;

        CreateBottomLine(rowRect, RowHeight);

        GameObject textObj = CreateText("Message", rowRect, message, 18f, TextAlignmentOptions.Center);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _spawnedRows.Add(rowObj);
    }

    private void BuildRows(ChartDefinition chart, List<StatsRowData> rows)
    {
        ClearSpawnedRows();

        if (rows.Count == 0)
        {
            BuildMessageRow("No matching runs found.");
            return;
        }

        _rowsContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rows.Count * RowHeight);

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            StatsRowData row = rows[rowIndex];
            GameObject rowObj = CreateImage(
                $"Row_{rowIndex}",
                _rowsContentRect,
                rowIndex % 2 == 0 ? RowColorEven : RowColorOdd,
                false
            );

            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, RowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -rowIndex * RowHeight);

            float start = 0f;
            for (int i = 0; i < chart.Headers.Length; i++)
            {
                float end = start + chart.WidthFractions[i];

                bool isRankColumn = i == chart.Headers.Length - 1;
                bool isPRank = isRankColumn && NormalizeRankLetter(row.TotalRank) == "P";

                if (isPRank)
                    CreateCellBackground(rowRect, start, end, 0f, RowHeight, PRankCellColor);

                string cellText = i < row.Cells.Length ? row.Cells[i] : string.Empty;
                if (isPRank)
                    cellText = "<color=#FFFFFF>P</color>";

                CreateCellText(
                    rowRect,
                    cellText,
                    start,
                    end,
                    0f,
                    RowHeight,
                    16f,
                    FontStyles.Normal,
                    TextAlignmentOptions.Center
                );

                start = end;
            }

            CreateBottomLine(rowRect, RowHeight);
            CreateColumnSeparators(rowRect, chart.WidthFractions, RowHeight);

            _spawnedRows.Add(rowObj);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rowsContentRect);
        Canvas.ForceUpdateCanvases();
        _rowsScrollbarDrag.RefreshHandle();
    }

    private void ClearSpawnedRows()
    {
        foreach (GameObject row in _spawnedRows)
        {
            if (row != null)
                Destroy(row);
        }

        _spawnedRows.Clear();
    }

    private List<string> ResolveSourceFiles(StatsMode mode)
    {
        string difficultyPath = GetSelectedDifficultyPath();
        List<string> files = new();

        if (!Directory.Exists(difficultyPath))
            return files;

        try
        {
            switch (mode)
            {
                case StatsMode.Campaign:
                    {
                        string selectedFile = GetSelectedCampaignLevelPath();
                        if (!string.IsNullOrWhiteSpace(selectedFile) && File.Exists(selectedFile))
                            files.Add(selectedFile);
                        break;
                    }

                case StatsMode.Custom:
                    {
                        string selectedFile = GetSelectedCustomLevelPath();
                        if (!string.IsNullOrWhiteSpace(selectedFile) && File.Exists(selectedFile))
                            files.Add(selectedFile);
                        break;
                    }

                default:
                    {
                        string[] rootFiles = Directory.GetFiles(difficultyPath, "*.jsonl", SearchOption.TopDirectoryOnly);
                        string[] preferred = rootFiles
                            .Where(path =>
                            {
                                string name = Path.GetFileName(path);
                                return name.IndexOf("cyber", StringComparison.OrdinalIgnoreCase) >= 0
                                    || name.IndexOf("grind", StringComparison.OrdinalIgnoreCase) >= 0
                                    || name.IndexOf("endless", StringComparison.OrdinalIgnoreCase) >= 0;
                            })
                            .ToArray();

                        files.AddRange(preferred.Length > 0 ? preferred : rootFiles);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to enumerate STATS files: {ex.Message}");
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private StatsRowData? ParseRow(StatsMode mode, string line)
    {
        string? id = GetJsonValue(line, "runId", "id", "ID");
        string? version = GetJsonValue(line, "version", "schemaVersion", "dataVersion", "S");

        string? dateRaw = GetJsonValue(line, "date", "utcTimeLogged", "timestamp", "timeOfDeath", "T");

        string? elapsedMillisecondsRaw = GetJsonValue(line, "t");
        string? elapsedSecondsRaw = GetJsonValue(line, "timeSeconds", "timeElapsed", "elapsed");

        string? kills = GetJsonValue(line, "kills", "k");
        string? style = GetJsonValue(line, "style", "s");

        string formattedDate = FormatUnixTimeLocal(dateRaw);
        string formattedElapsed = FormatElapsedForTable(elapsedMillisecondsRaw, elapsedSecondsRaw);

        if (mode == StatsMode.Cybergrind)
        {
            string? waveRaw = GetJsonValue(line, "wave", "w");

            return new StatsRowData
            {
                SortId = ParseSortId(id),
                Version = string.IsNullOrWhiteSpace(version) ? "-" : version,
                Cells = new[]
                {
                DisplayValue(id),
                formattedDate,
                formattedElapsed,
                DisplayValue(kills),
                DisplayValue(style),
                FormatWaveForTable(waveRaw)
            }
            };
        }

        string? restarts = GetJsonValue(line, "restarts", "r");

        string? packedRanks = GetJsonValue(line, "rankString", "individualRanks", "rs");

        string timeRank = NormalizeRankLetter(GetJsonValue(line, "timeRank"));
        string killsRank = NormalizeRankLetter(GetJsonValue(line, "killsRank"));
        string styleRank = NormalizeRankLetter(GetJsonValue(line, "styleRank"));
        string totalRank = NormalizeRankLetter(GetJsonValue(line, "totalRank", "rt"));

        if (string.IsNullOrEmpty(timeRank))
            timeRank = ExtractPackedRank(packedRanks, 0);

        if (string.IsNullOrEmpty(killsRank))
            killsRank = ExtractPackedRank(packedRanks, 1);

        if (string.IsNullOrEmpty(styleRank))
            styleRank = ExtractPackedRank(packedRanks, 2);

        return new StatsRowData
        {
            SortId = ParseSortId(id),
            Version = string.IsNullOrWhiteSpace(version) ? "-" : version,
            TimeRank = timeRank,
            KillsRank = killsRank,
            StyleRank = styleRank,
            TotalRank = totalRank,
            Cells = new[]
            {
            DisplayValue(id),
            formattedDate,
            ColorizeValue(formattedElapsed, timeRank),
            ColorizeValue(kills, killsRank),
            ColorizeValue(style, styleRank),
            DisplayValue(restarts),
            ColorizeValue(totalRank, totalRank)
        }
        };
    }

    private static string? GetJsonValue(string line, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (TryExtractJsonValue(line, key, out string? value))
                return value;
        }

        return null;
    }

    private static bool TryExtractJsonValue(string jsonLine, string key, out string? value)
    {
        value = null;
        string needle = "\"" + key + "\"";
        int keyIndex = jsonLine.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0)
            return false;

        int colonIndex = jsonLine.IndexOf(':', keyIndex + needle.Length);
        if (colonIndex < 0)
            return false;

        int i = colonIndex + 1;
        while (i < jsonLine.Length && char.IsWhiteSpace(jsonLine[i]))
            i++;

        if (i >= jsonLine.Length)
            return false;

        if (jsonLine[i] == '"')
        {
            i++;
            System.Text.StringBuilder builder = new();
            bool escaped = false;
            while (i < jsonLine.Length)
            {
                char c = jsonLine[i++];
                if (escaped)
                {
                    builder.Append(c switch
                    {
                        'n' => ' ',
                        'r' => ' ',
                        't' => ' ',
                        '"' => '"',
                        '\\' => '\\',
                        _ => c
                    });
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                builder.Append(c);
            }

            value = builder.ToString();
            return true;
        }

        int start = i;
        while (i < jsonLine.Length && jsonLine[i] != ',' && jsonLine[i] != '}' && !char.IsWhiteSpace(jsonLine[i]))
            i++;

        string raw = jsonLine.Substring(start, i - start);
        if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            value = null;
            return true;
        }

        value = raw;
        return true;
    }

    private static string ToPreferredCustomLevelDropdownText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        string text = value.Trim();

        int colonIndex = text.IndexOf(':');
        if (colonIndex > 0)
        {
            string prefix = text.Substring(0, colonIndex).Trim();
            if (!string.IsNullOrWhiteSpace(prefix))
                return prefix;
        }

        int underscoreIndex = text.IndexOf('_');
        if (underscoreIndex > 0)
        {
            string prefix = text.Substring(0, underscoreIndex).Trim();
            if (Regex.IsMatch(prefix, @"^[A-Za-z0-9]+-[A-Za-z0-9]+$"))
                return prefix;
        }

        return text;
    }

    private void LoadCustomBundleMetadata()
    {
        _customBundleCatalog.Clear();
        _bundleLastPlayedUtc.Clear();

        LoadBundleLastPlayedMap(GetAngryLastPlayedMapPath());
        LoadLevelCatalog(GetAngryLevelCatalogPath());
        LoadLocalBundleDisplayNames();

        BepInExLogs_US.Debug(
            $"Custom bundle metadata loaded: catalogKeys={_customBundleCatalog.Count}, " +
            $"lastPlayedEntries={_bundleLastPlayedUtc.Count}, " +
            $"localBundleNames={_localBundleDisplayNames.Count}"
        );
    }

    private void LoadBundleLastPlayedMap(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                string key = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (long.TryParse(lines[i + 1].Trim(), out long lastPlayedUtc))
                    _bundleLastPlayedUtc[key] = lastPlayedUtc;
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to read Angry Level Loader lastPlayedMap.txt: {ex.Message}");
        }
    }

    private void AddCustomBundleCatalogKey(string? key, CustomBundleCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        string trimmed = key.Trim();
        _customBundleCatalog[trimmed] = entry;

        string fileName = Path.GetFileName(trimmed);
        if (!string.IsNullOrWhiteSpace(fileName))
            _customBundleCatalog[fileName] = entry;
    }

    private void LoadLevelCatalog(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            string json = File.ReadAllText(path);
            AngryLevelCatalogData? data = JsonUtility.FromJson<AngryLevelCatalogData>(json);
            if (data?.Levels == null)
                return;

            foreach (AngryLevelCatalogBundle bundle in data.Levels)
            {
                CustomBundleCatalogEntry entry = BuildCustomBundleCatalogEntry(bundle);

                AddCustomBundleCatalogKey(entry.Guid, entry);
                AddCustomBundleCatalogKey(entry.Hash, entry);
                AddCustomBundleCatalogKey(bundle.buildHash, entry);
                AddCustomBundleCatalogKey(bundle.bundleGuid, entry);
                AddCustomBundleCatalogKey(bundle.bundleDataPath, entry);
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to read Angry Level Loader LevelCatalog.json: {ex.Message}");
        }
    }

    private CustomBundleCatalogEntry BuildCustomBundleCatalogEntry(AngryLevelCatalogBundle bundle)
    {
        CustomBundleCatalogEntry entry = new CustomBundleCatalogEntry
        {
            Guid = bundle.Guid?.Trim() ?? string.Empty,
            Hash = bundle.Hash?.Trim() ?? string.Empty,
            DisplayName = SanitizeCatalogText(bundle.Name)
        };

        if (string.IsNullOrWhiteSpace(entry.DisplayName))
            entry.DisplayName = !string.IsNullOrWhiteSpace(entry.Guid) ? entry.Guid : entry.Hash;

        if (bundle.Levels != null)
        {
            for (int i = 0; i < bundle.Levels.Length; i++)
            {
                AngryLevelCatalogLevel level = bundle.Levels[i];
                string displayName = SanitizeCatalogText(level.LevelName);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = DisplayValue(level.LevelId);

                AddCatalogLevelLookup(entry, level.LevelName, displayName, i);
                AddCatalogLevelLookup(entry, level.LevelId, displayName, i);
            }
        }

        return entry;
    }

    private static void AddCatalogLevelLookup(CustomBundleCatalogEntry entry, string? rawKey, string displayName, int order)
    {
        string normalized = NormalizeLookupKey(rawKey);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (!entry.LevelDisplayNames.ContainsKey(normalized))
            entry.LevelDisplayNames[normalized] = displayName;

        if (!entry.LevelOrder.ContainsKey(normalized))
            entry.LevelOrder[normalized] = order;
    }

    private CustomLevelFileInfo BuildCustomLevelFileInfo(string bundlePath, string filePath, int? difficultyNumber)
    {
        string fallbackName = ToDisplayName(bundlePath, filePath, difficultyNumber);
        string folderKey = Path.GetFileName(bundlePath) ?? string.Empty;
        CustomBundleCatalogEntry? entry = TryGetCustomBundleCatalogEntry(folderKey);
        string normalized = NormalizeLookupKey(fallbackName);

        if (entry != null && !string.IsNullOrWhiteSpace(normalized))
        {
            if (entry.LevelDisplayNames.TryGetValue(normalized, out string mappedDisplayName))
            {
                return new CustomLevelFileInfo
                {
                    DisplayName = mappedDisplayName,
                    Path = filePath,
                    CatalogOrder = entry.LevelOrder.TryGetValue(normalized, out int order) ? order : int.MaxValue
                };
            }
        }

        return new CustomLevelFileInfo
        {
            DisplayName = fallbackName,
            Path = filePath,
            CatalogOrder = int.MaxValue
        };
    }

    private CustomBundleCatalogEntry? TryGetCustomBundleCatalogEntry(string folderKey)
    {
        if (string.IsNullOrWhiteSpace(folderKey))
            return null;

        return _customBundleCatalog.TryGetValue(folderKey, out CustomBundleCatalogEntry? entry)
            ? entry
            : null;
    }

    private static string MakeUniqueBundleDisplayName(string displayName, string folderKey, ISet<string> usedNames)
    {
        string candidate = string.IsNullOrWhiteSpace(displayName) ? "Unknown Bundle" : displayName;
        if (usedNames.Add(candidate))
            return candidate;

        string suffix = ShortFolderKey(folderKey);
        candidate = $"{candidate} [{suffix}]";
        if (usedNames.Add(candidate))
            return candidate;

        int counter = 2;
        while (true)
        {
            string numbered = $"{candidate} ({counter})";
            if (usedNames.Add(numbered))
                return numbered;

            counter++;
        }
    }

    private static string ShortFolderKey(string? folderKey)
    {
        if (string.IsNullOrWhiteSpace(folderKey))
            return "unknown";

        return folderKey.Length <= 8 ? folderKey : folderKey.Substring(0, 8);
    }

    private static string GetAngryLevelCatalogPath()
    {
        return Path.Combine(Paths.ConfigPath, "AngryLevelLoader", "OnlineCache", "V2", "LevelCatalog.json");
    }

    private static string GetAngryLastPlayedMapPath()
    {
        return Path.Combine(Paths.ConfigPath, "AngryLevelLoader", "lastPlayedMap.txt");
    }

    private static string GetGameRootPath()
    {
        string? assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrWhiteSpace(assemblyDir))
            return Directory.GetCurrentDirectory();

        return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));
    }

    private static string NormalizeRankLetter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        raw = raw.Trim().ToUpperInvariant();
        return raw.Length == 1 ? raw : string.Empty;
    }

    private static string ExtractPackedRank(string? packedRanks, int index)
    {
        if (string.IsNullOrWhiteSpace(packedRanks))
            return string.Empty;

        string trimmed = packedRanks.Trim().ToUpperInvariant();
        if (index < 0 || index >= trimmed.Length)
            return string.Empty;

        return NormalizeRankLetter(trimmed[index].ToString());
    }

    private static string RankToColorHex(string? rank)
    {
        return NormalizeRankLetter(rank) switch
        {
            "S" => "#FF0000",
            "A" => "#FF6900",
            "B" => "#FFD900",
            "C" => "#4BFF00",
            "D" => "#0095FF",
            _ => string.Empty
        };
    }

    private static string ColorizeValue(string? value, string? rank)
    {
        string shown = DisplayValue(value);
        string colorHex = RankToColorHex(rank);

        if (shown == "-" || string.IsNullOrWhiteSpace(colorHex))
            return shown;

        return $"<color={colorHex}>{shown}</color>";
    }

    private static string ColorizeByRank(string? value, string? rank)
    {
        string shown = DisplayValue(value);
        string normalizedRank = NormalizeRankLetter(rank);

        if (shown == "-")
            return shown;

        if (normalizedRank == "P")
        {
            // TMP <mark> needs alpha. CC is a good strong highlight.
            return $"<mark=#FFAE00CC><color=#FFFFFF>{shown}</color></mark>";
        }

        string colorHex = RankToColorHex(normalizedRank);
        if (string.IsNullOrWhiteSpace(colorHex))
            return shown;

        return $"<color={colorHex}>{shown}</color>";
    }

    private static readonly Color PRankCellColor = new Color32(0xFF, 0xAE, 0x00, 0xFF);

    private void CreateCellBackground(
        RectTransform parent,
        float xMinNorm,
        float xMaxNorm,
        float yMin,
        float yMax,
        Color color)
    {
        GameObject bg = CreateImage("CellBackground", parent, color, false);
        RectTransform rect = bg.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMinNorm, 0f);
        rect.anchorMax = new Vector2(xMaxNorm, 0f);
        rect.offsetMin = new Vector2(0f, yMin);
        rect.offsetMax = new Vector2(0f, yMax);
    }

    private string BuildSelectedLocation(StatsMode mode, IReadOnlyList<string> sourceFiles)
    {
        return mode switch
        {
            StatsMode.Campaign => sourceFiles.Count > 0 ? sourceFiles[0] : GetSelectedCampaignLayerPath(),
            StatsMode.Custom => sourceFiles.Count > 0 ? sourceFiles[0] : GetSelectedCustomBundlePath(),
            _ => sourceFiles.Count > 0 ? sourceFiles[0] : GetSelectedDifficultyPath()
        };
    }

    private string BuildStatusText(StatsMode mode, int rowCount, int fileCount, string location, bool loading)
    {
        string modeName = _modeDropdown.SelectedText;
        string loadingLine = loading ? "\nLoading: yes" : string.Empty;
        string displayLocation = string.IsNullOrWhiteSpace(location) ? "-" : location;
        return $"Mode: {modeName}\nFiles read: {fileCount}\nRows loaded: {rowCount}{loadingLine}\nPath:\n{displayLocation}";
    }

    private string GetSelectedDifficultyPath()
    {
        return GetSelectedPath(_difficultyDropdown, _difficultyOptions, _difficultyPaths);
    }

    private int? GetSelectedDifficultyNumber()
    {
        string difficultyPath = GetSelectedDifficultyPath();
        if (string.IsNullOrWhiteSpace(difficultyPath))
            return null;

        string? folderName = Path.GetFileName(difficultyPath);
        return TryGetDifficultyNumber(folderName);
    }

    private string GetSelectedCampaignLayerPath()
    {
        return GetSelectedPath(_campaignLayerDropdown, _campaignLayerOptions, _campaignLayerPaths);
    }

    private string GetSelectedCampaignLevelPath()
    {
        return GetSelectedPath(_campaignLevelDropdown, _campaignLevelOptions, _campaignLevelPaths);
    }

    private string GetSelectedCustomBundlePath()
    {
        return GetSelectedPath(_customBundleDropdown, _customBundleOptions, _customBundlePaths);
    }

    private string GetSelectedCustomLevelPath()
    {
        return GetSelectedPath(_customLevelDropdown, _customLevelOptions, _customLevelPaths);
    }

    private StatsMode GetSelectedMode()
    {
        return (StatsMode)Mathf.Clamp(_modeDropdown != null ? _modeDropdown.value : 0, 0, 2);
    }

    private static string GetSelectedDropdownText(StatsDropdown_US? dropdown, IReadOnlyList<string> options, string fallback)
    {
        if (dropdown == null || options == null || options.Count == 0)
            return fallback;

        int index = Mathf.Clamp(dropdown.value, 0, options.Count - 1);
        return options[index];
    }

    private static string GetSelectedPath(StatsDropdown_US? dropdown, IReadOnlyList<string> options, IReadOnlyList<string> paths)
    {
        if (dropdown == null || options == null || paths == null || options.Count == 0 || paths.Count == 0)
            return string.Empty;

        int index = Mathf.Clamp(dropdown.value, 0, Math.Min(options.Count, paths.Count) - 1);
        return paths[index] ?? string.Empty;
    }

    private static void SetDropdownOptions(StatsDropdown_US? dropdown, IReadOnlyList<string> values, string preferredSelection)
    {
        if (dropdown == null)
            return;

        int index = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], preferredSelection, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        dropdown.SetOptions(values, index);
    }

    private static string BuildVersionsText(IEnumerable<string> versions)
    {
        List<string> cleaned = versions
            .Where(v => !string.IsNullOrWhiteSpace(v) && v != "-")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cleaned.Count == 0 ? "-" : string.Join(", ", cleaned);
    }

    private static int CompareRows(StatsRowData a, StatsRowData b)
    {
        bool descending = Plugin.StatsIdSortOrder == Plugin.StatsIdSortOrderOption.Descending;

        int byId = a.SortId.CompareTo(b.SortId);
        if (byId != 0)
            return descending ? -byId : byId;

        string aDate = a.Cells.Length > 1 ? a.Cells[1] : string.Empty;
        string bDate = b.Cells.Length > 1 ? b.Cells[1] : string.Empty;
        int byDate = StringComparer.OrdinalIgnoreCase.Compare(aDate, bDate);
        return descending ? -byDate : byDate;
    }

    private static int NaturalDifficultyOrder(string difficultyPath)
    {
        string? difficultyName = Path.GetFileName(difficultyPath);
        int? value = TryGetDifficultyNumber(difficultyName);
        return value ?? int.MaxValue - 1;
    }

    private static string ToDifficultyDisplayName(string folderName)
    {
        return TryGetDifficultyNumber(folderName) switch
        {
            0 => "HARMLESS",
            1 => "LENIENT",
            2 => "STANDARD",
            3 => "VIOLENT",
            4 => "BRUTAL",
            5 => "ULTRAKILL MUST DIE",
            _ => folderName
        };
    }

    private static string ToCampaignLayerDisplayName(string folderName)
    {
        return folderName switch
        {
            "0" => "PRELUDE",
            "1" => "LIMBO",
            "2" => "LUST",
            "3" => "GLUTTONY",
            "4" => "GREED",
            "5" => "WRATH",
            "6" => "HERESY",
            "7" => "VIOLENCE",
            "8" => "FRAUD",
            "9" => "TREACHERY",
            "P" => "PRIME SANCTUMS",
            _ => folderName
        };
    }

    private static int? TryGetDifficultyNumber(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        int underscore = folderName.LastIndexOf('_');
        if (underscore >= 0 && int.TryParse(folderName.Substring(underscore + 1), out int value))
            return value;

        return null;
    }

    private static long ParseSortId(string? id)
    {
        return long.TryParse(id, out long parsed) ? parsed : long.MaxValue;
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string SanitizeCatalogText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string withoutTags = Regex.Replace(value, "<.*?>", string.Empty);
        return withoutTags.Trim();
    }

    private static string NormalizeLookupKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string sanitized = SanitizeCatalogText(value);
        if (string.IsNullOrWhiteSpace(sanitized))
            return string.Empty;

        char[] filtered = sanitized
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();

        return new string(filtered);
    }

    private static string ToDisplayName(string rootPath, string filePath)
    {
        string relative = Path.GetRelativePath(rootPath, filePath);
        string withoutExtension = Path.ChangeExtension(relative, null) ?? relative;
        return withoutExtension.Replace('\\', '/');
    }

    private static string ToDisplayName(string rootPath, string filePath, int? difficultyNumber)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);

        if (string.IsNullOrWhiteSpace(name))
            return "-";

        if (difficultyNumber.HasValue)
        {
            string suffix = "_" + difficultyNumber.Value;
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - suffix.Length);
        }

        return name;
    }

    private static string GetAngryInternalConfigPath()
    {
        return Path.Combine(
            Paths.ConfigPath,
            "PluginConfigurator",
            "com.eternalUnion.angryLevelLoader_internal.config"
        );
    }

    private static string? TryReadAngryDataPath()
    {
        try
        {
            string configPath = GetAngryInternalConfigPath();
            if (!File.Exists(configPath))
                return null;

            string[] lines = File.ReadAllLines(configPath);
            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim().Equals("dataPath", StringComparison.OrdinalIgnoreCase))
                    return lines[i + 1].Trim();
            }
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Warn($"Failed to read Angry internal config: {ex.Message}");
        }

        return null;
    }

    private static string FormatUnixTimeLocal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "-";

        raw = raw.Trim();

        if (!long.TryParse(raw, out long unix))
            return DisplayValue(raw);

        try
        {
            DateTimeOffset localTime;

            // 13+ digits -> milliseconds, otherwise seconds
            if (raw.Length >= 13)
                localTime = DateTimeOffset.FromUnixTimeMilliseconds(unix).ToLocalTime();
            else
                localTime = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();

            return localTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            return DisplayValue(raw);
        }
    }

    private static string FormatElapsedForTable(string? millisecondsRaw, string? secondsRaw)
    {
        if (!string.IsNullOrWhiteSpace(millisecondsRaw) &&
            long.TryParse(millisecondsRaw.Trim(), out long totalMilliseconds))
        {
            return FormatDurationMilliseconds(totalMilliseconds);
        }

        if (!string.IsNullOrWhiteSpace(secondsRaw) &&
            double.TryParse(secondsRaw.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double totalSeconds))
        {
            long convertedMilliseconds = (long)Math.Round(totalSeconds * 1000.0);
            return FormatDurationMilliseconds(convertedMilliseconds);
        }

        return DisplayValue(secondsRaw ?? millisecondsRaw);
    }

    private static string FormatDurationMilliseconds(long totalMilliseconds)
    {
        if (totalMilliseconds < 0)
            totalMilliseconds = 0;

        long minutes = totalMilliseconds / 60000;
        long seconds = (totalMilliseconds / 1000) % 60;
        long milliseconds = totalMilliseconds % 1000;

        return $"{minutes}:{seconds:00}.{milliseconds:000}";
    }

    private static string FormatWaveForTable(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "-";

        raw = raw.Trim();

        if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double value))
        {
            // Old Cybergrind format appears to store wave progress * 100
            // Example: 5147 -> 51.47
            if (Math.Abs(value) >= 1000d)
                return (value / 100d).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        return DisplayValue(raw);
    }

    private void AddLocalBundleDisplayKey(string? key, string displayName)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(displayName))
            return;

        string trimmed = key.Trim();
        _localBundleDisplayNames[trimmed] = displayName;

        string fileName = Path.GetFileName(trimmed);
        if (!string.IsNullOrWhiteSpace(fileName))
            _localBundleDisplayNames[fileName] = displayName;
    }

    private void LoadLocalBundleDisplayNames()
    {
        _localBundleDisplayNames.Clear();

        string? dataPath = TryReadAngryDataPath();
        if (string.IsNullOrWhiteSpace(dataPath))
            return;

        string unpackedRoot = Path.Combine(dataPath, "LevelsUnpacked");
        if (!Directory.Exists(unpackedRoot))
            return;

        foreach (string dir in Directory.GetDirectories(unpackedRoot, "*", SearchOption.TopDirectoryOnly))
        {
            string jsonPath = Path.Combine(dir, "data.json");
            if (!File.Exists(jsonPath))
                continue;

            try
            {
                string json = File.ReadAllText(jsonPath);
                AngryLocalBundleData? data = JsonUtility.FromJson<AngryLocalBundleData>(json);
                if (data == null)
                    continue;

                string displayName = SanitizeCatalogText(data.bundleName);
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                string folderKey = Path.GetFileName(dir) ?? string.Empty;

                AddLocalBundleDisplayKey(folderKey, displayName);
                AddLocalBundleDisplayKey(data.bundleGuid, displayName);
                AddLocalBundleDisplayKey(data.bundleDataPath, displayName);
                AddLocalBundleDisplayKey(data.buildHash, displayName);
            }
            catch (Exception ex)
            {
                BepInExLogs_US.Warn($"Failed to read local Angry bundle metadata from '{jsonPath}': {ex.Message}");
            }
        }

        BepInExLogs_US.Debug($"Loaded local Angry bundle display names: {_localBundleDisplayNames.Count}");
    }

    private string GetCustomBundleDisplayName(string folderKey)
    {
        if (_localBundleDisplayNames.TryGetValue(folderKey, out string localName) &&
            !string.IsNullOrWhiteSpace(localName))
        {
            return localName;
        }

        CustomBundleCatalogEntry? entry = TryGetCustomBundleCatalogEntry(folderKey);
        if (entry != null && !string.IsNullOrWhiteSpace(entry.DisplayName))
            return entry.DisplayName;

        AngryLevelCatalogBundle? bundle = TryGetAngryBundle(folderKey);
        string catalogName = CleanAngryDisplayText(bundle?.Name ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(catalogName))
            return catalogName;

        return $"Unknown Bundle [{ShortFolderKey(folderKey)}]";
    }

    private GameObject CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private GameObject CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return obj;
    }

    private GameObject CreateText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.richText = true;
        MainMenuButton_US.ApplyPanelTextStyle(_styleRoot, text);
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return obj;
    }

    private void CreateOutline(Transform parent, float thickness, Color color)
    {
        CreateBorder(parent, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);
        CreateBorder(parent, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
        CreateBorder(parent, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);
        CreateBorder(parent, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);
    }

    private void CreateBorder(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject border = CreateImage(name + "Border", parent, color, false);
        RectTransform rect = border.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void CreateBottomLine(Transform parent, float height)
    {
        GameObject line = CreateImage("BottomLine", parent, Color.white, false);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, -height);
    }

    private void CreateColumnSeparators(Transform parent, IReadOnlyList<float> widths, float height)
    {
        float x = 0f;
        for (int i = 0; i < widths.Count - 1; i++)
        {
            x += widths[i];
            GameObject line = CreateImage("ColumnLine_" + i, parent, Color.white, false);
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(x, 1f);
            rect.anchorMax = new Vector2(x, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(1f, height);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    private void CreateCellText(
        Transform parent,
        string value,
        float anchorMinX,
        float anchorMaxX,
        float offsetTop,
        float height,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject cellTextObj = CreateText("CellText", parent, value, fontSize, alignment);
        RectTransform cellRect = cellTextObj.GetComponent<RectTransform>();
        cellRect.anchorMin = new Vector2(anchorMinX, 1f);
        cellRect.anchorMax = new Vector2(anchorMaxX, 1f);
        cellRect.pivot = new Vector2(0.5f, 1f);
        cellRect.offsetMin = new Vector2(6f, -(offsetTop + height));
        cellRect.offsetMax = new Vector2(-6f, -offsetTop);

        TextMeshProUGUI text = cellTextObj.GetComponent<TextMeshProUGUI>();
        text.fontStyle = style;
        text.margin = Vector4.zero;
    }
}