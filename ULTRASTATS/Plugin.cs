using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using PluginConfig.API;
using PluginConfig.API.Decorators;
using PluginConfig.API.Fields;
using PluginConfig.API.Functionals;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ULTRASTATS;

[BepInPlugin(ModGuid, ModName, ModVer)]
[BepInDependency("com.eternalUnion.angryLevelLoader", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "atom.ultrastats";
    public const string ModName = "ULTRASTATS";
    public const string ModVer = "0.0.12";

    internal static Plugin? Instance { get; private set; }
    internal static ManualLogSource Log = null!;

    internal static StringField DataFolderParentPathField = null!;
    internal static BoolField EnableCybergrindLoggingField = null!;
    internal static BoolField EnableCampaignLoggingField = null!;
    internal static BoolField EnableCustomLevelLoggingField = null!;


    internal static BoolField EnableEndscreenDiscardField = null!;
    internal static KeyCodeField DiscardPendingRunKeyField = null!;
    internal static BoolField EnableDebugLoggingField = null!;
    internal static EnumField<MainMenuButtonCornerOption> MainMenuButtonCornerField = null!;
    internal static EnumField<DefaultMainMenuTabOption> DefaultMainMenuTabField = null!;
    internal static EnumField<StatsDefaultDifficultyOption> DefaultStatsDifficultyField = null!;
    internal static EnumField<StatsIdSortOrderOption> StatsIdSortOrderField = null!;
    internal static bool DebugLoggingEnabled =>
        EnableDebugLoggingField?.value ?? false;

    internal enum MainMenuButtonCornerOption
    {
        BottomRight = 0,
        BottomLeft = 1,
        TopRight = 2,
        TopLeft = 3
    }

    internal enum DefaultMainMenuTabOption
    {
        Info = 0,
        Stats = 1,
        Plots = 2
    }

    internal enum StatsDefaultDifficultyOption
    {
        Harmless = 0,
        Lenient = 1,
        Standard = 2,
        Violent = 3,
        Brutal = 4,
        UltrakillMustDie = 5
    }

    internal enum StatsIdSortOrderOption
    {
        Ascending = 0,
        Descending = 1
    }

    private Harmony? _harmony;
    private PluginConfigurator? _config;

    internal static string DefaultDataFolderParentPath
    {
        get
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roaming, "AtomSmasher1586");
        }
    }

    internal static string DataFolderParentPath =>
        NormalizeDataFolderParentPath(DataFolderParentPathField?.value);

    internal static string DataFolderPath =>
        Path.Combine(DataFolderParentPath, "ULTRASTATS");

    internal static bool CybergrindLoggingEnabled =>
        EnableCybergrindLoggingField?.value ?? true;

    internal static bool AngryLevelLoaderInstalled =>
        LoggerShared.AngryAvailable;

    internal static bool CustomLevelLoggingEnabled =>
        AngryLevelLoaderInstalled && (EnableCustomLevelLoggingField?.value ?? true);

    internal static bool CampaignLoggingEnabled =>
        EnableCampaignLoggingField?.value ?? true;

    internal static bool EndscreenDiscardEnabled =>
        EnableEndscreenDiscardField?.value ?? false;

    internal static KeyCode EndscreenDiscardKey =>
        DiscardPendingRunKeyField?.value ?? KeyCode.Delete;

    internal static MainMenuButtonCornerOption MainMenuButtonCorner =>
        MainMenuButtonCornerField?.value ?? MainMenuButtonCornerOption.BottomRight;

    internal static DefaultMainMenuTabOption DefaultMainMenuTab =>
        DefaultMainMenuTabField?.value ?? DefaultMainMenuTabOption.Info;

    internal static int DefaultStatsDifficultyNumber =>
        DefaultStatsDifficultyField?.value switch
        {
            StatsDefaultDifficultyOption.Harmless => 0,
            StatsDefaultDifficultyOption.Lenient => 1,
            StatsDefaultDifficultyOption.Standard => 2,
            StatsDefaultDifficultyOption.Violent => 3,
            StatsDefaultDifficultyOption.Brutal => 4,
            StatsDefaultDifficultyOption.UltrakillMustDie => 5,
            _ => 2
        };

    internal static StatsIdSortOrderOption StatsIdSortOrder =>
        StatsIdSortOrderField?.value ?? StatsIdSortOrderOption.Ascending;

    private static string NormalizeDataFolderParentPath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return DefaultDataFolderParentPath;

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(rawPath.Trim());
            string fullPath = Path.GetFullPath(expanded);

            string trimmed = fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );

            string leafName = Path.GetFileName(trimmed);

            if (leafName.Equals("ULTRASTATS", StringComparison.OrdinalIgnoreCase))
            {
                string? parent = Path.GetDirectoryName(trimmed);
                if (!string.IsNullOrWhiteSpace(parent))
                    return parent;
            }

            return fullPath;
        }
        catch
        {
            return DefaultDataFolderParentPath;
        }
    }

    private static ConfigHeader AddSectionHeader(ConfigPanel panel, string title, Color color, int size = 26)
    {
        var header = new ConfigHeader(panel, title, size);
        header.textColor = color;
        return header;
    }

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        SetupConfig();

        PendingRunManager.Initialize();
        MainMenuButton_US.Init();
        BepInExLogs_US.ModLoaded();

        BepInExLogs_US.Debug(() => $"Data folder parent = {DataFolderParentPath}");
        BepInExLogs_US.Debug(() => $"Data folder = {DataFolderPath}");
        BepInExLogs_US.Debug(() => $"Campaign logging enabled = {CampaignLoggingEnabled}");
        BepInExLogs_US.Debug(() => $"Cybergrind logging enabled = {CybergrindLoggingEnabled}");
        BepInExLogs_US.Debug(() => $"Angry Level Loader installed = {AngryLevelLoaderInstalled}");
        BepInExLogs_US.Debug(() => $"Custom level logging enabled = {CustomLevelLoggingEnabled}");
        BepInExLogs_US.Debug(() => $"Main menu button corner = {MainMenuButtonCorner}");
        BepInExLogs_US.Debug(() => $"Main menu default tab = {DefaultMainMenuTab}");
        BepInExLogs_US.Debug(() => $"Stats default difficulty = {DefaultStatsDifficultyNumber}");
        BepInExLogs_US.Debug(() => $"Stats ID sort order = {StatsIdSortOrder}");
        BepInExLogs_US.Debug(() =>
            Chainloader.PluginInfos.ContainsKey("com.eternalUnion.angryLevelLoader")
                ? "Angry Level Loader is loaded."
                : "Angry Level Loader is not loaded."
        );

        if (!AngryLevelLoaderInstalled)
            BepInExLogs_US.Warn("Angry Level Loader not found. Custom level logging is disabled.");

        Application.quitting += OnAppQuitting;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        _harmony = new Harmony(ModGuid);
        _harmony.PatchAll();

        BepInExLogs_US.Debug("Harmony patches applied.");
    }

    private void SetupConfig()
    {
        _config = PluginConfigurator.Create(ModName, ModGuid);

        Color strongSectionColor = new Color32(170, 225, 255, 255);
        Color softSectionColor = new Color32(125, 165, 185, 255);

        // =========================
        // Data Storage
        // =========================
        AddSectionHeader(_config.rootPanel, "Data Storage", strongSectionColor);
        DataFolderParentPathField = new StringField(
            _config.rootPanel,
            "ULTRASTATS parent folder",
            "data_folder_parent_path",
            DefaultDataFolderParentPath
        );
        DataFolderParentPathField.allowEmptyValues = false;
        DataFolderParentPathField.value = NormalizeDataFolderParentPath(DataFolderParentPathField.value);

        // =========================
        // Data Saving
        // =========================
        AddSectionHeader(_config.rootPanel, "Data Saving", softSectionColor);
        EnableCybergrindLoggingField = new BoolField(
            _config.rootPanel,
            "Enable Cybergrind logging",
            "enable_cybergrind_logging",
            true
        );
        EnableCampaignLoggingField = new BoolField(
            _config.rootPanel,
            "Enable campaign logging",
            "enable_campaign_logging",
            true
        );
        EnableCustomLevelLoggingField = new BoolField(
            _config.rootPanel,
            "Enable custom level logging",
            "enable_custom_level_logging",
            true
        );
        EnableCustomLevelLoggingField.interactable = AngryLevelLoaderInstalled;
        if (!AngryLevelLoaderInstalled)
            EnableCustomLevelLoggingField.value = false;

        // =========================
        // Preferences
        // =========================
        AddSectionHeader(_config.rootPanel, "Preferences", softSectionColor);
        MainMenuButtonCornerField = new EnumField<MainMenuButtonCornerOption>(
            _config.rootPanel,
            "Main menu button corner (Restart)",
            "main_menu_button_corner",
            MainMenuButtonCornerOption.BottomRight
        );
        MainMenuButtonCornerField.SetEnumDisplayName(MainMenuButtonCornerOption.BottomRight, "Bottom Right");
        MainMenuButtonCornerField.SetEnumDisplayName(MainMenuButtonCornerOption.BottomLeft, "Bottom Left");
        MainMenuButtonCornerField.SetEnumDisplayName(MainMenuButtonCornerOption.TopRight, "Top Right");
        MainMenuButtonCornerField.SetEnumDisplayName(MainMenuButtonCornerOption.TopLeft, "Top Left");
        MainMenuButtonCornerField.onValueChange += _ =>
        {
            MainMenuButton_US.RefreshButtonPosition();
        };

        DefaultMainMenuTabField = new EnumField<DefaultMainMenuTabOption>(
            _config.rootPanel,
            "Main menu button default tab",
            "default_main_menu_tab",
            DefaultMainMenuTabOption.Info
        );
        DefaultMainMenuTabField.SetEnumDisplayName(DefaultMainMenuTabOption.Info, "Info");
        DefaultMainMenuTabField.SetEnumDisplayName(DefaultMainMenuTabOption.Stats, "Stats");
        DefaultMainMenuTabField.SetEnumDisplayName(DefaultMainMenuTabOption.Plots, "Plots");

        DefaultStatsDifficultyField = new EnumField<StatsDefaultDifficultyOption>(
            _config.rootPanel,
            "Stats tab default difficulty",
            "default_stats_difficulty",
            StatsDefaultDifficultyOption.Standard
        );
        DefaultStatsDifficultyField.SetEnumDisplayName(StatsDefaultDifficultyOption.Harmless, "Harmless");
        DefaultStatsDifficultyField.SetEnumDisplayName(StatsDefaultDifficultyOption.Lenient, "Lenient");
        DefaultStatsDifficultyField.SetEnumDisplayName(StatsDefaultDifficultyOption.Standard, "Standard");
        DefaultStatsDifficultyField.SetEnumDisplayName(StatsDefaultDifficultyOption.Violent, "Violent");
        DefaultStatsDifficultyField.SetEnumDisplayName(StatsDefaultDifficultyOption.Brutal, "Brutal");
        DefaultStatsDifficultyField.SetEnumDisplayName(StatsDefaultDifficultyOption.UltrakillMustDie, "UKMD");

        StatsIdSortOrderField = new EnumField<StatsIdSortOrderOption>(
            _config.rootPanel,
            "Stats ID order",
            "stats_id_sort_order",
            StatsIdSortOrderOption.Ascending
        );
        StatsIdSortOrderField.SetEnumDisplayName(StatsIdSortOrderOption.Ascending, "Ascending");
        StatsIdSortOrderField.SetEnumDisplayName(StatsIdSortOrderOption.Descending, "Descending");

        // =========================
        // Debug
        // =========================
        AddSectionHeader(_config.rootPanel, "Debug", strongSectionColor);
        EnableDebugLoggingField = new BoolField(
            _config.rootPanel,
            "Enable debug logging",
            "enable_debug_logging",
            false
        );
        EnableEndscreenDiscardField = new BoolField(
            _config.rootPanel,
            "Allow discard on endscreen",
            "allow_discard_on_endscreen",
            false
        );
        ConfigDivision discardDivision = new ConfigDivision(
            _config.rootPanel,
            "discard_division"
        );
        DiscardPendingRunKeyField = new KeyCodeField(
            discardDivision,
            "Discard pending run key",
            "discard_pending_run_key",
            KeyCode.Delete
        );
        EnableEndscreenDiscardField.onValueChange += e =>
        {
            discardDivision.interactable = e.value;
        };
        discardDivision.interactable = EnableEndscreenDiscardField.value;


        try
        {
            Directory.CreateDirectory(DataFolderPath);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"[ULTRASTATS] Could not create configured data folder, falling back to default: {ex}");
            DataFolderParentPathField.value = DefaultDataFolderParentPath;
            Directory.CreateDirectory(DataFolderPath);
        }

        string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string iconPath = Path.Combine(pluginDir, "icon.png");

        if (File.Exists(iconPath))
        {
            string iconUri = new Uri(iconPath).AbsoluteUri;
            Log.LogInfo($"[ULTRASTATS] Icon uri: {iconUri}");
            _config.SetIconWithURL(iconUri);
        }
        else
        {
            Log.LogWarning($"[ULTRASTATS] icon.png not found at: {iconPath}");
        }
    }

    private static void OnAppQuitting()
    {
        PendingRunManager.FlushPendingIfAny("Application.quitting");
    }

    private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        PendingRunManager.FlushPendingIfAny("SceneManager.activeSceneChanged");
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Application.quitting -= OnAppQuitting;

        MainMenuButton_US.Shutdown();
        PendingRunManager.FlushPendingIfAny("Plugin.OnDestroy");
        PendingRunManager.Shutdown();
        _harmony?.UnpatchSelf();
    }
}