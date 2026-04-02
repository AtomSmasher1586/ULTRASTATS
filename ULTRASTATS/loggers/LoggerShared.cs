using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Windows;

namespace ULTRASTATS;

internal enum LevelSource
{
    Unknown = 0,
    Campaign = 1,
    Cybergrind = 2,
    Custom = 3
}

internal readonly struct LevelContext
{
    public LevelContext(LevelSource source, string sceneName, string levelId, bool isCustom)
    {
        Source = source;
        SceneName = sceneName ?? "";
        LevelId = string.IsNullOrWhiteSpace(levelId) ? "unknown" : levelId;
        IsCustom = isCustom;
    }

    public LevelSource Source { get; }
    public string SceneName { get; }
    public string LevelId { get; }
    public bool IsCustom { get; }
}

internal readonly struct CustomLevelLogContext
{
    public CustomLevelLogContext(string packFolderKey, string levelFileStem, string levelId)
    {
        PackFolderKey = string.IsNullOrWhiteSpace(packFolderKey) ? "unknown_bundle" : packFolderKey;
        LevelFileStem = string.IsNullOrWhiteSpace(levelFileStem) ? "unknown" : levelFileStem;
        LevelId = string.IsNullOrWhiteSpace(levelId) ? "unknown" : levelId;
    }

    public string PackFolderKey { get; }
    public string LevelFileStem { get; }
    public string LevelId { get; }
}


internal static class LoggerShared
{
    private static readonly object FileLock = new();
    private static readonly object IdLock = new();
    private static readonly object SerializerLock = new();
    private static readonly Dictionary<Type, DataContractJsonSerializer> SerializerCache = new();

    private static readonly Regex SlotRegex =
        new(@"^Slot(?<n>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LayerRegex =
        new(@"^(?<layer>\d+)-", RegexOptions.Compiled);

    private static readonly Regex PrimeRegex =
        new(@"^P-\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LevelRegex =
        new(@"(?<id>\d+-\d+[A-Za-z]?)", RegexOptions.Compiled);

    private static readonly Regex TagRegex =
        new(@"<.*?>", RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex =
        new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex HumanLevelCodeRegex =
        new(@"^(?:\d+-\d+[A-Za-z]?|P-\d+|[A-Z]{1,4}-\d+[A-Za-z]?)$",
            RegexOptions.Compiled);

    public static string RootDir => Plugin.DataFolderPath;

    public static long UnixSecondsNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static int Difficulty()
    {
        try { return MonoSingleton<PrefsManager>.Instance.GetInt("difficulty", 0); }
        catch { return 0; }
    }

    public static string DifficultyDir(int difficulty) =>
        Path.Combine(RootDir, $"Difficulty_{difficulty}");

    public static int GetSaveSlotId()
    {
        try
        {
            string folder = Path.GetFileName(GameProgressSaver.SavePath.TrimEnd('\\', '/'));
            Match m = SlotRegex.Match(folder);
            if (m.Success && int.TryParse(m.Groups["n"].Value, out int slot))
                return slot;

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public static string LayerFolder(string levelId)
    {
        Match m = LayerRegex.Match(levelId ?? "");
        if (m.Success && int.TryParse(m.Groups["layer"].Value, out int layer) && layer >= 0)
            return layer.ToString();

        if (PrimeRegex.IsMatch(levelId ?? ""))
            return "P";

        return "misc";
    }

    public static string SanitizeFileName(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "unknown";

        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');

        // Clean spaces around underscores created by replaced punctuation like ": "
        s = Regex.Replace(s, @"\s*_\s*", "_");

        return s.Trim();
    }

    public static string CampaignOutputPath(int difficulty, string levelId)
    {
        string safeLevel = SanitizeFileName(levelId);
        return Path.Combine(
            DifficultyDir(difficulty),
            LayerFolder(levelId),
            $"{safeLevel}_{difficulty}.jsonl"
        );
    }

    public static string CybergrindOutputPath(int difficulty) =>
        Path.Combine(DifficultyDir(difficulty), $"cybergrind_{difficulty}.jsonl");

    public static bool AngryAvailable =>
        AccessTools.TypeByName("AngryLevelLoader.Managers.AngrySceneManager") != null;

    public static string CustomOutputPath(int difficulty, string packFolderKey, string levelFileStem)
    {
        string safePack = SanitizeFileName(packFolderKey);
        string safeStem = SanitizeFileName(levelFileStem);

        return Path.Combine(
            DifficultyDir(difficulty),
            "custom",
            safePack,
            $"{safeStem}_{difficulty}.jsonl"
        );
    }

    public static string StripTags(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        return TagRegex.Replace(s, "").Trim();
    }

    public static int BuildFlags(bool majorUsed, bool cheatsUsed)
    {
        int flags = 0;
        if (majorUsed) flags |= 1;
        if (cheatsUsed) flags |= 2;
        return flags;
    }

    public static LevelContext GetCurrentLevelContext()
    {
        string scene = "";
        try { scene = SceneHelper.CurrentScene ?? ""; } catch { }

        string trimmed = scene.Trim();
        bool isCustom = false;
        try { isCustom = SceneHelper.IsPlayingCustom; } catch { }

        if (string.Equals(trimmed, "Endless", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Cybergrind", StringComparison.OrdinalIgnoreCase))
        {
            return new LevelContext(LevelSource.Cybergrind, trimmed, "cybergrind", isCustom);
        }

        string levelId = ResolveCampaignLikeLevelId(trimmed);
        return new LevelContext(
            isCustom ? LevelSource.Custom : LevelSource.Campaign,
            trimmed,
            levelId,
            isCustom
        );
    }

    private static string ResolveCampaignLikeLevelId(string scene)
    {
        scene = (scene ?? "").Trim();

        if (scene.StartsWith("Level ", StringComparison.OrdinalIgnoreCase))
            scene = scene.Substring("Level ".Length).Trim();

        if (string.Equals(scene, "Bootstrap", StringComparison.OrdinalIgnoreCase))
            return "0-1";

        Match m = LevelRegex.Match(scene);
        if (m.Success)
            return m.Groups["id"].Value;

        return SanitizeFileName(scene.Length > 0 ? scene : "unknown");
    }

    public static readonly object WriteLock = new();

    public static long NextIdForFile(string jsonlPath)
    {
        lock (IdLock)
        {
            string idPath = jsonlPath + ".id";
            Directory.CreateDirectory(Path.GetDirectoryName(idPath)!);

            long cur = 0;
            if (File.Exists(idPath))
                long.TryParse(File.ReadAllText(idPath).Trim(), out cur);

            cur++;
            File.WriteAllText(idPath, cur.ToString());
            return cur;
        }
    }

    public static void AppendJsonLine<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string json;
        lock (SerializerLock)
        {
            using var ms = new MemoryStream();
            GetSerializer(typeof(T)).WriteObject(ms, value);
            json = Encoding.UTF8.GetString(ms.ToArray());
        }

        lock (FileLock)
        {
            using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            sw.WriteLine(json);
        }
    }

    private static DataContractJsonSerializer GetSerializer(Type type)
    {
        if (SerializerCache.TryGetValue(type, out var serializer))
            return serializer;

        serializer = new DataContractJsonSerializer(type);
        SerializerCache[type] = serializer;
        return serializer;
    }

    public static string BoolText(bool value) => value ? "true" : "false";

    public static string FormatWaveHundredths(int waveHundredths) =>
        (waveHundredths / 100f).ToString("0.00", CultureInfo.InvariantCulture);

    public static bool TryGetCurrentCustomLogContext(out CustomLevelLogContext ctx)
    {
        ctx = default;

        LevelContext baseCtx = GetCurrentLevelContext();
        if (baseCtx.Source != LevelSource.Custom || !baseCtx.IsCustom)
        {
            BepInExLogs_US.Debug("Custom ctx failed: not currently in a custom level.");
            return false;
        }

        Type? angrySceneManagerType =
            AccessTools.TypeByName("AngryLevelLoader.Managers.AngrySceneManager");
        if (angrySceneManagerType == null)
        {
            BepInExLogs_US.Debug("Custom ctx failed: AngrySceneManager type not found.");
            return false;
        }

        object? currentLevelData = TryGetStaticMember(angrySceneManagerType, "currentLevelData");
        object? currentBundleContainer = TryGetStaticMember(angrySceneManagerType, "currentBundleContainer");

        BepInExLogs_US.Debug(() => $"currentLevelData null = {currentLevelData == null}, currentBundleContainer null = {currentBundleContainer == null}");

        string levelId = ReadStringMember(currentLevelData, "uniqueIdentifier");
        string levelName = ReadStringMember(currentLevelData, "levelName");
        string scenePath = ReadStringMember(currentLevelData, "scenePath");

        object? bundleData = null;
        if (currentBundleContainer != null)
            UltraStatsReflection.TryGetMember(currentBundleContainer, "bundleData", out bundleData);

        string packFolderKey = ReadStringMember(bundleData, "bundleDataPath");

        if (string.IsNullOrWhiteSpace(packFolderKey))
        {
            string tempFolderPath = ReadStringMember(currentBundleContainer, "pathToTempFolder");
            string tempLeaf = Path.GetFileName((tempFolderPath ?? "").TrimEnd('\\', '/'));

            if (!string.IsNullOrWhiteSpace(tempLeaf))
                packFolderKey = tempLeaf;
        }

        if (string.IsNullOrWhiteSpace(packFolderKey))
            packFolderKey = ReadStringMember(bundleData, "bundleGuid");

        BepInExLogs_US.Debug(() =>
            $"Custom ctx values: levelId='{levelId}', levelName='{levelName}', scenePath='{scenePath}', packFolderKey='{packFolderKey}'");

        if (string.IsNullOrWhiteSpace(levelId))
            levelId = baseCtx.LevelId;

        string fileStem = BuildCustomLevelFileStem(levelId, levelName, scenePath);
        ctx = new CustomLevelLogContext(packFolderKey, fileStem, levelId);
        return true;
    }

    public static string BuildCustomLevelFileStem(string levelId, string levelName, string scenePath = "")
    {
        string id = NormalizeFileStemPart(levelId);
        string title = NormalizeFileStemPart(levelName);

        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(scenePath))
            title = NormalizeFileStemPart(Path.GetFileNameWithoutExtension(scenePath));

        // Prefer title-only for weird machine ids like se.tstaI
        if (!string.IsNullOrWhiteSpace(title))
        {
            if (!string.IsNullOrWhiteSpace(id) && HumanLevelCodeRegex.IsMatch(id))
            {
                if (string.Equals(id, title, StringComparison.OrdinalIgnoreCase) ||
                    title.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase) ||
                    title.StartsWith(id + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return SanitizeFileName(title);
                }

                return SanitizeFileName($"{id}_{title}");
            }

            return SanitizeFileName(title);
        }

        if (!string.IsNullOrWhiteSpace(id))
            return SanitizeFileName(id);

        return "unknown";
    }

    private static string NormalizeFileStemPart(string s)
    {
        s = StripTags(s ?? "");
        s = s.Replace('\r', ' ')
             .Replace('\n', ' ')
             .Replace('\t', ' ');

        s = WhitespaceRegex.Replace(s, " ").Trim();
        return s;
    }

    private static object? TryGetStaticMember(Type type, params string[] names)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < names.Length; i++)
        {
            PropertyInfo? prop = type.GetProperty(names[i], flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                object? value = prop.GetValue(null, null);
                if (value != null)
                    return value;
            }

            FieldInfo? field = type.GetField(names[i], flags);
            if (field != null)
            {
                object? value = field.GetValue(null);
                if (value != null)
                    return value;
            }
        }

        return null;
    }

    private static string ReadStringMember(object? obj, params string[] names)
    {
        if (obj == null)
            return "";

        if (obj is string s)
            return (s ?? "").Trim();

        for (int i = 0; i < names.Length; i++)
        {
            if (!UltraStatsReflection.TryGetMember(obj, names[i], out object? value) || value == null)
                continue;

            try
            {
                return Convert.ToString(value)?.Trim() ?? "";
            }
            catch
            {
            }
        }

        return "";
    }
}

internal static class UltraStatsReflection
{
    public static bool TryGetMember(object obj, string name, out object? value)
    {
        value = null;
        try
        {
            Type t = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo? field = t.GetField(name, flags);
            if (field != null)
            {
                value = field.GetValue(obj);
                return true;
            }

            PropertyInfo? prop = t.GetProperty(name, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                value = prop.GetValue(obj, null);
                return true;
            }
        }
        catch { }

        return false;
    }

    public static int TryGetInt(object obj, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (!TryGetMember(obj, names[i], out object? v) || v == null)
                continue;

            try
            {
                if (v is int ii) return ii;
                if (v is float ff) return Mathf.RoundToInt(ff);
                if (v is double dd) return (int)Math.Round(dd);
            }
            catch { }
        }

        return -1;
    }

    public static float TryGetFloat(object obj, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (!TryGetMember(obj, names[i], out object? v) || v == null)
                continue;

            try
            {
                if (v is float ff) return ff;
                if (v is int ii) return ii;
                if (v is double dd) return (float)dd;
            }
            catch { }
        }

        return -1f;
    }

    public static bool TryGetBoolExact(object obj, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (!TryGetMember(obj, names[i], out object? v) || v == null)
                continue;

            try
            {
                if (v is bool bb) return bb;
                if (v is int ii) return ii != 0;
            }
            catch { }
        }

        return false;
    }

    public static bool TryGetBoolLoose(object obj, params string[] tokens)
    {
        try
        {
            Type t = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo f in t.GetFields(flags))
            {
                if (f.FieldType != typeof(bool) && f.FieldType != typeof(int))
                    continue;

                bool match = true;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (f.Name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                    continue;

                object? v = null;
                try { v = f.GetValue(obj); } catch { }
                if (v is bool b && b) return true;
                if (v is int ii && ii != 0) return true;
            }

            foreach (PropertyInfo p in t.GetProperties(flags))
            {
                if (p.GetIndexParameters().Length != 0)
                    continue;

                if (p.PropertyType != typeof(bool) && p.PropertyType != typeof(int))
                    continue;

                bool match = true;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (p.Name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                    continue;

                object? v = null;
                try { v = p.GetValue(obj, null); } catch { }
                if (v is bool b && b) return true;
                if (v is int ii && ii != 0) return true;
            }
        }
        catch { }

        return false;
    }

    public static bool TryInvokeBoolGetter(object obj, params string[] keys)
    {
        try
        {
            Type t = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            MethodInfo? getBool2 = t.GetMethod("GetBool", flags, null, new[] { typeof(string), typeof(bool) }, null);
            MethodInfo? getBool1 = t.GetMethod("GetBool", flags, null, new[] { typeof(string) }, null);
            MethodInfo? getInt2 = t.GetMethod("GetInt", flags, null, new[] { typeof(string), typeof(int) }, null);
            MethodInfo? getInt1 = t.GetMethod("GetInt", flags, null, new[] { typeof(string) }, null);

            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];

                if (getBool2 != null)
                {
                    object? v = getBool2.Invoke(obj, new object[] { key, false });
                    if (v is bool b && b) return true;
                }

                if (getBool1 != null)
                {
                    object? v = getBool1.Invoke(obj, new object[] { key });
                    if (v is bool b && b) return true;
                }

                if (getInt2 != null)
                {
                    object? v = getInt2.Invoke(obj, new object[] { key, 0 });
                    if (v is int ii && ii != 0) return true;
                }

                if (getInt1 != null)
                {
                    object? v = getInt1.Invoke(obj, new object[] { key });
                    if (v is int ii && ii != 0) return true;
                }
            }
        }
        catch { }

        return false;
    }

    public static string TryGetTMPText(object inst, string fieldName)
    {
        if (!TryGetMember(inst, fieldName, out object? tmpObj) || tmpObj == null)
            return "";

        try
        {
            Type t = tmpObj.GetType();

            MethodInfo? getParsed = t.GetMethod(
                "GetParsedText",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (getParsed != null)
                return getParsed.Invoke(tmpObj, null) as string ?? "";

            PropertyInfo? prop = t.GetProperty(
                "text",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            return prop?.GetValue(tmpObj, null) as string ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static object? TryGetSingletonInstance(string typeName)
    {
        try
        {
            Type? found = null;
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < asms.Length && found == null; i++)
            {
                try
                {
                    found = asms[i].GetType(typeName, false);
                    if (found != null)
                        break;

                    Type[] types = asms[i].GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        if (types[j].Name == typeName)
                        {
                            found = types[j];
                            break;
                        }
                    }
                }
                catch { }
            }

            if (found == null)
                return null;

            const BindingFlags sflags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            string[] memberNames = { "Instance", "instance", "Current", "current", "Inst", "inst" };

            for (int i = 0; i < memberNames.Length; i++)
            {
                PropertyInfo? p = found.GetProperty(memberNames[i], sflags);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    object? v = p.GetValue(null, null);
                    if (v != null) return v;
                }

                FieldInfo? f = found.GetField(memberNames[i], sflags);
                if (f != null)
                {
                    object? v = f.GetValue(null);
                    if (v != null) return v;
                }
            }
        }
        catch { }

        return null;
    }
}

internal static class PendingRunManager
{
    internal interface IRunCaptureSnapshot { }

    internal readonly struct CampaignRunCapture : IRunCaptureSnapshot
    {
        public CampaignRunCapture(
            int difficulty,
            int saveSlot,
            string levelId,
            int timeMs,
            int kills,
            int style,
            int restarts,
            int points,
            string timeRankRaw,
            string killsRankRaw,
            string styleRankRaw,
            string totalRankRaw,
            bool tookDamage,
            bool challengeComplete,
            bool majorAssistsUsed,
            bool cheatsUsed)
        {
            Difficulty = difficulty;
            SaveSlot = saveSlot;
            LevelId = levelId ?? "unknown";
            TimeMs = timeMs;
            Kills = kills;
            Style = style;
            Restarts = restarts;
            Points = points;
            TimeRankRaw = timeRankRaw ?? "";
            KillsRankRaw = killsRankRaw ?? "";
            StyleRankRaw = styleRankRaw ?? "";
            TotalRankRaw = totalRankRaw ?? "";
            TookDamage = tookDamage;
            ChallengeComplete = challengeComplete;
            MajorAssistsUsed = majorAssistsUsed;
            CheatsUsed = cheatsUsed;
        }

        public int Difficulty { get; }
        public int SaveSlot { get; }
        public string LevelId { get; }
        public int TimeMs { get; }
        public int Kills { get; }
        public int Style { get; }
        public int Restarts { get; }
        public int Points { get; }
        public string TimeRankRaw { get; }
        public string KillsRankRaw { get; }
        public string StyleRankRaw { get; }
        public string TotalRankRaw { get; }
        public bool TookDamage { get; }
        public bool ChallengeComplete { get; }
        public bool MajorAssistsUsed { get; }
        public bool CheatsUsed { get; }
    }

    internal readonly struct CustomRunCapture : IRunCaptureSnapshot
    {
        public CustomRunCapture(
            int difficulty,
            int saveSlot,
            string packFolderKey,
            string levelFileStem,
            string levelId,
            int timeMs,
            int kills,
            int style,
            int restarts,
            int points,
            string timeRankRaw,
            string killsRankRaw,
            string styleRankRaw,
            string totalRankRaw,
            bool tookDamage,
            bool challengeComplete,
            bool majorAssistsUsed,
            bool cheatsUsed)
        {
            Difficulty = difficulty;
            SaveSlot = saveSlot;
            PackFolderKey = packFolderKey ?? "unknown_bundle";
            LevelFileStem = levelFileStem ?? "unknown";
            LevelId = levelId ?? "unknown";
            TimeMs = timeMs;
            Kills = kills;
            Style = style;
            Restarts = restarts;
            Points = points;
            TimeRankRaw = timeRankRaw ?? "";
            KillsRankRaw = killsRankRaw ?? "";
            StyleRankRaw = styleRankRaw ?? "";
            TotalRankRaw = totalRankRaw ?? "";
            TookDamage = tookDamage;
            ChallengeComplete = challengeComplete;
            MajorAssistsUsed = majorAssistsUsed;
            CheatsUsed = cheatsUsed;
        }

        public int Difficulty { get; }
        public int SaveSlot { get; }
        public string PackFolderKey { get; }
        public string LevelFileStem { get; }
        public string LevelId { get; }
        public int TimeMs { get; }
        public int Kills { get; }
        public int Style { get; }
        public int Restarts { get; }
        public int Points { get; }
        public string TimeRankRaw { get; }
        public string KillsRankRaw { get; }
        public string StyleRankRaw { get; }
        public string TotalRankRaw { get; }
        public bool TookDamage { get; }
        public bool ChallengeComplete { get; }
        public bool MajorAssistsUsed { get; }
        public bool CheatsUsed { get; }
    }

    internal readonly struct CybergrindRunCapture : IRunCaptureSnapshot
    {
        public CybergrindRunCapture(
            int difficulty,
            int saveSlot,
            int timeMs,
            int kills,
            int style,
            int waveHundredths,
            int flags)
        {
            Difficulty = difficulty;
            SaveSlot = saveSlot;
            TimeMs = timeMs;
            Kills = kills;
            Style = style;
            WaveHundredths = waveHundredths;
            Flags = flags;
        }

        public int Difficulty { get; }
        public int SaveSlot { get; }
        public int TimeMs { get; }
        public int Kills { get; }
        public int Style { get; }
        public int WaveHundredths { get; }
        public int Flags { get; }
    }

    private enum PendingBatchState
    {
        Pending = 0,
        Discarded = 1,
        Saved = 2
    }

    private sealed class PendingBatch
    {
        public Guid BatchToken { get; set; }
        public string OutputPath { get; set; } = "";
        public object RunRecord { get; set; } = null!;
        public Type RunRecordType { get; set; } = null!;
        public List<object> DataLogRecords { get; } = new();
        public string[] QueueLines { get; set; } = Array.Empty<string>();
        public PendingBatchState State { get; set; } = PendingBatchState.Pending;
    }

    private static readonly object WorkerLock = new();
    private static readonly ConcurrentQueue<Action> WorkerQueue = new();
    private static readonly AutoResetEvent WorkerSignal = new(false);
    private static readonly object PendingStateLock = new();

    private static CancellationTokenSource? _workerCts;
    private static Thread? _workerThread;
    private static PendingBatch? _pendingBatch;
    private static bool _hasPending;

    public static bool HasPending
    {
        get
        {
            lock (PendingStateLock)
                return _hasPending;
        }
    }

    public static void Initialize()
    {
        lock (WorkerLock)
        {
            if (_workerThread != null)
                return;

            _workerCts = new CancellationTokenSource();
            _workerThread = new Thread(() => WorkerLoop(_workerCts.Token))
            {
                Name = "ULTRASTATS.Worker",
                IsBackground = true
            };
            _workerThread.Start();

            BepInExLogs_US.Debug("ULTRASTATS worker thread started.");
        }
    }

    public static void EnqueueCapture(IRunCaptureSnapshot snapshot)
    {
        Initialize();
        WorkerQueue.Enqueue(() => HandleCapturedRun(snapshot));
        WorkerSignal.Set();
    }

    public static bool TryDiscardPending()
    {
        if (!HasPending)
            return false;

        Initialize();
        WorkerQueue.Enqueue(DiscardPendingCore);
        WorkerSignal.Set();
        return true;
    }

    public static void FlushPendingIfAny(string reason)
    {
        Initialize();
        WorkerQueue.Enqueue(() => FlushPendingCore(reason));
        WorkerSignal.Set();
    }

    public static void AttachDiscardWatcher(Component? owner)
    {
        if (owner == null)
            return;

        if (owner.GetComponent<PendingRunDiscardWatcher>() == null)
            owner.gameObject.AddComponent<PendingRunDiscardWatcher>();
    }

    private static void SetHasPending(bool value)
    {
        lock (PendingStateLock)
            _hasPending = value;
    }

    private static void HandleCapturedRun(IRunCaptureSnapshot snapshot)
    {
        PendingBatch? newBatch = BuildPendingBatch(snapshot);
        if (newBatch == null)
            return;

        if (_pendingBatch != null && _pendingBatch.State == PendingBatchState.Pending)
            SaveBatch(_pendingBatch, "superseded");

        _pendingBatch = newBatch;
        SetHasPending(true);

        for (int i = 0; i < newBatch.QueueLines.Length; i++)
            BepInExLogs_US.Queue(newBatch.QueueLines[i]);

        BepInExLogs_US.Debug(() => $"Queued batch {newBatch.BatchToken} for {newBatch.OutputPath}");

        bool keepPendingForDiscard = Plugin.EndscreenDiscardEnabled;
        if (!keepPendingForDiscard)
        {
            SaveBatch(_pendingBatch, "immediate-save");
            _pendingBatch = null;
            SetHasPending(false);
        }
    }

    private static PendingBatch? BuildPendingBatch(IRunCaptureSnapshot snapshot)
    {
        if (snapshot is CampaignRunCapture campaign)
            return BuildCampaignBatch(campaign);
        if (snapshot is CustomRunCapture custom)
            return BuildCustomBatch(custom);
        if (snapshot is CybergrindRunCapture cyber)
            return BuildCybergrindBatch(cyber);

        BepInExLogs_US.Debug(() => $"Unknown snapshot type ignored: {snapshot.GetType().FullName}");
        return null;
    }

    private static PendingBatch? BuildCampaignBatch(CampaignRunCapture c)
    {
        string tr = NormalizeRankLetter(LoggerShared.StripTags(c.TimeRankRaw));
        string kr = NormalizeRankLetter(LoggerShared.StripTags(c.KillsRankRaw));
        string sr = NormalizeRankLetter(LoggerShared.StripTags(c.StyleRankRaw));
        string rt = NormalizeRankLetter(LoggerShared.StripTags(c.TotalRankRaw));

        CampLine line = new()
        {
            S = Math.Max(0, c.SaveSlot),
            T = LoggerShared.UnixSecondsNow(),
            t = Math.Max(0, c.TimeMs),
            k = Math.Max(0, c.Kills),
            s = Math.Max(0, c.Style),
            r = Math.Max(0, c.Restarts),
            p = Math.Max(0, c.Points),
            rs = $"{tr}{kr}{sr}",
            rt = rt,
            td = c.TookDamage,
            F = LoggerShared.BuildFlags(c.MajorAssistsUsed, c.CheatsUsed),
            c = c.ChallengeComplete
        };

        if (!HasCompleteEndscreenData(line))
            return null;

        string path = LoggerShared.CampaignOutputPath(c.Difficulty, c.LevelId);
        string[] queueLines =
        {
            $"| CAMPAIGN | Difficulty = {c.Difficulty} | Level = {c.LevelId} | Savefile = {line.S} | Time of Death = {line.T} |",
            $"| TimeElapsed = {line.t}ms | Kills = {line.k} | Style = {line.s} | Restarts = {line.r} | P Earned = {line.p} |",
            $"| Individual Ranks = {line.rs} | Overall Rank = {line.rt} | Took Damage = {LoggerShared.BoolText(line.td)} | Challenge = {LoggerShared.BoolText(line.c)} |"
        };

        return new PendingBatch
        {
            BatchToken = Guid.NewGuid(),
            OutputPath = path,
            RunRecord = line,
            RunRecordType = typeof(CampLine),
            QueueLines = queueLines
        };
    }

    private static PendingBatch? BuildCustomBatch(CustomRunCapture c)
    {
        string tr = NormalizeRankLetter(LoggerShared.StripTags(c.TimeRankRaw));
        string kr = NormalizeRankLetter(LoggerShared.StripTags(c.KillsRankRaw));
        string sr = NormalizeRankLetter(LoggerShared.StripTags(c.StyleRankRaw));
        string rt = NormalizeRankLetter(LoggerShared.StripTags(c.TotalRankRaw));

        CampLine line = new()
        {
            S = Math.Max(0, c.SaveSlot),
            T = LoggerShared.UnixSecondsNow(),
            t = Math.Max(0, c.TimeMs),
            k = Math.Max(0, c.Kills),
            s = Math.Max(0, c.Style),
            r = Math.Max(0, c.Restarts),
            p = Math.Max(0, c.Points),
            rs = $"{tr}{kr}{sr}",
            rt = rt,
            td = c.TookDamage,
            F = LoggerShared.BuildFlags(c.MajorAssistsUsed, c.CheatsUsed),
            c = c.ChallengeComplete
        };

        if (!HasCompleteCustomEndscreenData(line))
        {
            BepInExLogs_US.Debug(() =>
                $"Custom batch ignored because endscreen data stayed incomplete: time={line.t}, points={line.p}, rs='{line.rs}', rt='{line.rt}'");
            return null;
        }

        string path = LoggerShared.CustomOutputPath(c.Difficulty, c.PackFolderKey, c.LevelFileStem);
        string[] queueLines =
        {
            $"| CUSTOM | Difficulty = {c.Difficulty} | Pack = {c.PackFolderKey} | Level = {c.LevelId} | Savefile = {line.S} |",
            $"| Time of Death = {line.T} | TimeElapsed = {line.t}ms | Kills = {line.k} | Style = {line.s} | Restarts = {line.r} | P Earned = {line.p} |",
            $"| Individual Ranks = {line.rs} | Overall Rank = {line.rt} | Took Damage = {LoggerShared.BoolText(line.td)} | Challenge = {LoggerShared.BoolText(line.c)} |"
        };

        return new PendingBatch
        {
            BatchToken = Guid.NewGuid(),
            OutputPath = path,
            RunRecord = line,
            RunRecordType = typeof(CampLine),
            QueueLines = queueLines
        };
    }

    private static PendingBatch? BuildCybergrindBatch(CybergrindRunCapture c)
    {
        CgLine line = new()
        {
            S = Math.Max(0, c.SaveSlot),
            T = LoggerShared.UnixSecondsNow(),
            t = Math.Max(0, c.TimeMs),
            k = Math.Max(0, c.Kills),
            s = Math.Max(0, c.Style),
            w = Math.Max(0, c.WaveHundredths),
            F = c.Flags
        };

        if (line.t <= 0 && line.w <= 0)
            return null;

        string path = LoggerShared.CybergrindOutputPath(c.Difficulty);
        string[] queueLines =
        {
            $"| CYBERGRIND | Difficulty = {c.Difficulty} | Savefile = {line.S} | Time of Death = {line.T} Unix Time |",
            $"| Wave = {LoggerShared.FormatWaveHundredths(line.w)} | TimeElapsed = {line.t}ms | Kills = {line.k} | Style = {line.s} |"
        };

        return new PendingBatch
        {
            BatchToken = Guid.NewGuid(),
            OutputPath = path,
            RunRecord = line,
            RunRecordType = typeof(CgLine),
            QueueLines = queueLines
        };
    }

    private static void DiscardPendingCore()
    {
        if (_pendingBatch == null || _pendingBatch.State != PendingBatchState.Pending)
            return;

        _pendingBatch.State = PendingBatchState.Discarded;
        _pendingBatch = null;
        SetHasPending(false);
        BepInExLogs_US.QueueCleared();
        BepInExLogs_US.Debug("Pending batch discarded.");
    }

    private static void FlushPendingCore(string reason)
    {
        if (_pendingBatch == null)
            return;

        SaveBatch(_pendingBatch, reason);
        _pendingBatch = null;
        SetHasPending(false);
    }

    private static void SaveBatch(PendingBatch batch, string reason)
    {
        if (batch.State != PendingBatchState.Pending)
            return;

        try
        {
            if (batch.RunRecord is CampLine camp)
            {
                camp.ID = LoggerShared.NextIdForFile(batch.OutputPath);
                LoggerShared.AppendJsonLine(batch.OutputPath, camp);
                BepInExLogs_US.QueueAppended(camp.ID, batch.OutputPath);
            }
            else if (batch.RunRecord is CgLine cg)
            {
                cg.ID = LoggerShared.NextIdForFile(batch.OutputPath);
                LoggerShared.AppendJsonLine(batch.OutputPath, cg);
                BepInExLogs_US.QueueAppended(cg.ID, batch.OutputPath);
            }

            batch.State = PendingBatchState.Saved;
            BepInExLogs_US.Debug(() => $"Pending batch committed. reason = {reason}");
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Error($"Failed to append run to {batch.OutputPath}", ex);
        }
        finally
        {
            BepInExLogs_US.QueueCleared();
        }
    }

    private static bool HasCompleteEndscreenData(CampLine line)
    {
        if (line.t <= 0 || line.p <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(line.rt) || line.rt.Length != 1)
            return false;

        if (string.IsNullOrWhiteSpace(line.rs) || line.rs.Length != 3)
            return false;

        return true;
    }

    private static bool HasCompleteCustomEndscreenData(CampLine line)
    {
        if (line.t <= 0 || line.p <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(line.rt) || line.rt.Length != 1)
            return false;

        if (string.IsNullOrWhiteSpace(line.rs) || line.rs.Length != 3)
            return false;

        return true;
    }

    private static string NormalizeRankLetter(string raw)
    {
        raw = (raw ?? "").Trim().ToUpperInvariant();
        return raw.Length == 1 ? raw : "";
    }

    private static void WorkerLoop(CancellationToken token)
    {
        while (true)
        {
            while (WorkerQueue.TryDequeue(out Action? work))
            {
                try { work(); }
                catch (Exception ex)
                {
                    BepInExLogs_US.Error("Worker step failed.", ex);
                }
            }

            if (token.IsCancellationRequested)
                break;

            WorkerSignal.WaitOne(250);
        }

        while (WorkerQueue.TryDequeue(out Action? remaining))
        {
            try { remaining(); }
            catch (Exception ex)
            {
                BepInExLogs_US.Error("Worker drain step failed.", ex);
            }
        }

        if (_pendingBatch != null)
        {
            SaveBatch(_pendingBatch, "shutdown-drain");
            _pendingBatch = null;
            SetHasPending(false);
        }
    }

    public static void Shutdown()
    {
        CancellationTokenSource? cts;
        Thread? worker;

        lock (WorkerLock)
        {
            cts = _workerCts;
            worker = _workerThread;

            _workerCts = null;
            _workerThread = null;
        }

        if (cts == null)
            return;

        try
        {
            cts.Cancel();
            WorkerSignal.Set();
            worker?.Join(1000);
        }
        catch (Exception ex)
        {
            BepInExLogs_US.Debug(() => $"Worker shutdown wait failed: {ex.Message}");
        }
        finally
        {
            cts.Dispose();
        }
    }

    // Legacy entry point intentionally removed from use by loggers.
    public static void QueuePending(string outputPath, string[] queueLines, Action commitAction)
    {
        throw new NotSupportedException("QueuePending is replaced by capture snapshots and worker-owned PendingBatch.");
    }
}

internal sealed class PendingRunDiscardWatcher : MonoBehaviour
{
    private void Update()
    {
        if (!Plugin.EndscreenDiscardEnabled)
            return;

        if (!PendingRunManager.HasPending)
        {
            Destroy(this);
            return;
        }

        if (!gameObject.activeInHierarchy)
            return;

        KeyCode key = Plugin.EndscreenDiscardKey;
        if (key == KeyCode.None)
            return;

        if (UnityEngine.Input.GetKeyDown(key))
        {
            if (PendingRunManager.TryDiscardPending())
                Destroy(this);
        }
    }
}

[HarmonyPatch(typeof(SceneHelper))]
internal static class SceneHelper_LoadScene_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("LoadScene")]
    private static void Prefix()
    {
        PendingRunManager.FlushPendingIfAny("LoadScene");
        CybergrindDeathStatsLogger.ResetSceneState();
    }
}

[HarmonyPatch(typeof(SceneHelper))]
internal static class SceneHelper_RestartScene_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("RestartScene")]
    private static void Prefix()
    {
        PendingRunManager.FlushPendingIfAny("RestartScene");
        CybergrindDeathStatsLogger.ResetSceneState();
    }
}