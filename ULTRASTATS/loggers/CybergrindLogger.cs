using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace ULTRASTATS;

[DataContract]
public sealed class CgLine
{
    [DataMember(Name = "S", Order = 0)] public int S;
    [DataMember(Name = "ID", Order = 1)] public long ID;
    [DataMember(Name = "T", Order = 2)] public long T;
    [DataMember(Name = "t", Order = 3)] public int t;
    [DataMember(Name = "k", Order = 4)] public int k;
    [DataMember(Name = "s", Order = 5)] public int s;
    [DataMember(Name = "w", Order = 6)] public int w;
    [DataMember(Name = "F", Order = 7, EmitDefaultValue = false)] public int F;
}

internal static class CybergrindDeathStatsLogger
{
    private static bool _captureInProgress;
    private static bool _cachedFlagsValid;
    private static int _cachedFlags;
    private static readonly HashSet<int> QueuedRankIds = new();

    private static readonly string[] WaveNames =
    {
        "savedWaves", "SavedWaves", "preciseWaves", "PreciseWaves"
    };

    private static readonly string[] TimeNames =
    {
        "savedTime", "SavedTime", "time", "Time"
    };

    private static readonly string[] KillNames =
    {
        "savedKills", "SavedKills", "kills", "Kills", "killCount", "KillCount", "totalKills", "TotalKills"
    };

    private static readonly string[] StyleNames =
    {
        "savedStyle", "SavedStyle", "style", "Style", "stylePoints", "StylePoints", "savedStylePoints", "SavedStylePoints"
    };

    private static readonly string[] SingletonTypeNames =
    {
        "AssistController", "AssistManager",
        "CheatController", "CheatsController",
        "CheatManager", "CheatsManager",
        "OptionsManager", "PrefsManager",
        "StatsManager", "GameProgressSaver"
    };

    private static readonly string[] MajorFlagNames =
    {
        "majorAssistsUsed", "majorUsed", "majorAssists", "majorAssist", "usedMajorAssists"
    };

    private static readonly string[] CheatFlagNames =
    {
        "cheatsUsed", "cheats", "usedCheats", "cheatUsed", "cheatMode", "cheatsEnabled"
    };

    private static readonly string[] MajorPrefKeys =
    {
        "major_assists", "majorAssist", "major assists", "majorAssists"
    };

    private static readonly string[] CheatPrefKeys =
    {
        "cheats", "cheat", "usedCheats", "cheatsEnabled", "keepCheatsEnabled"
    };

    public static void ResetSceneState()
    {
        _captureInProgress = false;
        _cachedFlagsValid = false;
        _cachedFlags = 0;
        QueuedRankIds.Clear();
    }

    public static void ScheduleCapture(FinalCyberRank rank)
    {
        if (!Plugin.CybergrindLoggingEnabled || Plugin.Instance == null || rank == null)
            return;

        int rankId = rank.GetInstanceID();

        if (QueuedRankIds.Contains(rankId) || _captureInProgress)
            return;

        _captureInProgress = true;
        Plugin.Instance.StartCoroutine(CaptureRoutine(rank, rankId));
    }

    private static IEnumerator CaptureRoutine(FinalCyberRank rank, int rankId)
    {
        const float timeout = 1.0f;
        const float step = 0.05f;

        float waited = 0f;
        PendingRunManager.CybergrindRunCapture? best = null;

        while (waited < timeout && rank != null)
        {
            PendingRunManager.CybergrindRunCapture rec = CaptureCoreNoFlags(rank);

            if (!best.HasValue || ScoreCore(rec) > ScoreCore(best.Value))
                best = rec;

            yield return new WaitForSecondsRealtime(step);
            waited += step;
        }

        if (best.HasValue)
        {
            int flags = rank != null ? ResolveCyberFlagsCached(rank) : 0;
            PendingRunManager.CybergrindRunCapture line = new(
                LoggerShared.Difficulty(),
                LoggerShared.GetSaveSlotId(),
                best.Value.TimeMs,
                best.Value.Kills,
                best.Value.Style,
                best.Value.WaveHundredths,
                flags
            );

            if (QueuedRankIds.Contains(rankId))
            {
                _captureInProgress = false;
                yield break;
            }

            QueuedRankIds.Add(rankId);

            PendingRunManager.EnqueueCapture(line);

            PendingRunManager.AttachDiscardWatcher(rank);

            BepInExLogs_US.Debug(() =>
                $"Cybergrind snapshot enqueued: wave={line.WaveHundredths}, time={line.TimeMs}, kills={line.Kills}, style={line.Style}, flags={line.Flags}");
        }

        _captureInProgress = false;
    }

    private static int ScoreCore(PendingRunManager.CybergrindRunCapture record)
    {
        int score = 0;
        if (record.WaveHundredths > 0) score += 3;
        if (record.TimeMs > 0) score += 2;
        if (record.Kills > 0) score += 2;
        else if (record.Kills == 0) score += 1;
        if (record.Style > 0) score += 2;
        else if (record.Style == 0) score += 1;
        return score;
    }

    private static PendingRunManager.CybergrindRunCapture CaptureCoreNoFlags(FinalCyberRank rank)
    {
        int waveHundredths = 0;
        int timeMs = 0;

        float wavesPrecise = UltraStatsReflection.TryGetFloat(rank, WaveNames);
        if (wavesPrecise >= 0f)
            waveHundredths = Mathf.RoundToInt(wavesPrecise * 100f);

        float timeSeconds = UltraStatsReflection.TryGetFloat(rank, TimeNames);
        if (timeSeconds >= 0f)
            timeMs = Mathf.RoundToInt(timeSeconds * 1000f);

        return new PendingRunManager.CybergrindRunCapture(
            0,
            0,
            timeMs,
            Math.Max(0, UltraStatsReflection.TryGetInt(rank, KillNames)),
            Math.Max(0, UltraStatsReflection.TryGetInt(rank, StyleNames)),
            waveHundredths,
            0
        );
    }

    private static int ResolveCyberFlagsCached(FinalCyberRank rank)
    {
        if (_cachedFlagsValid)
            return _cachedFlags;

        _cachedFlags = ResolveCyberFlags(rank);
        _cachedFlagsValid = true;
        return _cachedFlags;
    }

    private static int ResolveCyberFlags(FinalCyberRank rank)
    {
        bool major = false;
        bool cheats = false;

        TryResolveFlagsFromObject(rank, ref major, ref cheats);

        for (int i = 0; i < SingletonTypeNames.Length && !(major && cheats); i++)
        {
            object? obj = UltraStatsReflection.TryGetSingletonInstance(SingletonTypeNames[i]);
            if (obj != null)
                TryResolveFlagsFromObject(obj, ref major, ref cheats);
        }

        return LoggerShared.BuildFlags(major, cheats);
    }

    private static void TryResolveFlagsFromObject(object obj, ref bool major, ref bool cheats)
    {
        if (obj == null)
            return;

        if (!major)
        {
            major =
                UltraStatsReflection.TryGetBoolExact(obj, MajorFlagNames) ||
                UltraStatsReflection.TryGetBoolLoose(obj, "major", "assist");
        }

        if (!cheats)
        {
            cheats =
                UltraStatsReflection.TryGetBoolExact(obj, CheatFlagNames) ||
                UltraStatsReflection.TryGetBoolLoose(obj, "cheat");
        }

        string typeName = obj.GetType().Name;
        if (typeName.IndexOf("pref", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("option", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (!major)
                major = UltraStatsReflection.TryInvokeBoolGetter(obj, MajorPrefKeys);

            if (!cheats)
                cheats = UltraStatsReflection.TryInvokeBoolGetter(obj, CheatPrefKeys);
        }
    }
}

[HarmonyPatch(typeof(FinalCyberRank))]
internal static class FinalCyberRank_GameOver_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch("GameOver")]
    private static void Postfix(FinalCyberRank __instance)
    {
        if (SceneHelper.CurrentScene != "Endless")
            return;

        CybergrindDeathStatsLogger.ScheduleCapture(__instance);
    }
}