using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace ULTRASTATS;

internal static class CustomLevelStatsLogger
{
    private static readonly ConditionalWeakTable<object, Scratch> ScratchTable = new();

    private sealed class Scratch
    {
        public bool Logged;
        public bool CaptureScheduled;
        public int Restarts;
        public bool TookDamage = true;
        public bool MajorAssistsUsed;
        public bool CheatsUsed;
    }

    private static Scratch GetScratch(object inst) => ScratchTable.GetOrCreateValue(inst);

    public static void NoteInfo(object inst, int restarts, bool damage, bool majorUsed, bool cheatsUsed)
    {
        Scratch scratch = GetScratch(inst);
        scratch.Restarts = restarts;
        scratch.TookDamage = damage;
        scratch.MajorAssistsUsed = majorUsed;
        scratch.CheatsUsed = cheatsUsed;

        BepInExLogs_US.Debug(() =>
            $"Custom NoteInfo: restarts={restarts}, damage={damage}, major={majorUsed}, cheats={cheatsUsed}");
    }

    public static void ScheduleQueue(object inst)
    {
        if (!Plugin.CustomLevelLoggingEnabled || Plugin.Instance == null)
            return;

        Scratch scratch = GetScratch(inst);
        if (scratch.Logged || scratch.CaptureScheduled)
            return;

        LevelContext ctx = LoggerShared.GetCurrentLevelContext();
        if (ctx.Source != LevelSource.Custom || !ctx.IsCustom)
            return;

        scratch.CaptureScheduled = true;
        Plugin.Instance.StartCoroutine(QueueWhenReadyRoutine(inst));
    }

    private static IEnumerator QueueWhenReadyRoutine(object inst)
    {
        const float timeout = 3.0f;
        const float step = 0.05f;

        float waited = 0f;
        PendingRunManager.CustomRunCapture? completeCapture = null;
        string levelId = "";
        string packKey = "";
        string bestFailureReason = "custom endscreen data never became complete";

        while (waited < timeout)
        {
            if (TryBuildCapture(
                inst,
                out PendingRunManager.CustomRunCapture capture,
                out string resolvedLevelId,
                out string resolvedPackKey,
                out string incompleteReason))
            {
                if (HasCompleteEndscreenData(capture))
                {
                    completeCapture = capture;
                    levelId = resolvedLevelId;
                    packKey = resolvedPackKey;
                    break;
                }

                bestFailureReason = incompleteReason;
            }

            yield return new WaitForSecondsRealtime(step);
            waited += step;
        }

        Scratch scratch = GetScratch(inst);
        scratch.CaptureScheduled = false;

        if (scratch.Logged)
        {
            BepInExLogs_US.Debug("Custom queue routine finished without queueing a run because it was already logged.");
            yield break;
        }

        if (completeCapture == null)
        {
            BepInExLogs_US.Debug(() =>
                $"Custom queue routine timed out. Skipping save because endscreen data stayed incomplete: {bestFailureReason}");
            yield break;
        }

        PendingRunManager.CustomRunCapture finalCapture = completeCapture.Value;
        scratch.Logged = true;
        PendingRunManager.EnqueueCapture(finalCapture);

        if (inst is Component component)
            PendingRunManager.AttachDiscardWatcher(component);

        BepInExLogs_US.Debug(() =>
            $"Custom snapshot enqueued: pack={packKey}, level={levelId}, time={finalCapture.TimeMs}, points={finalCapture.Points}");
    }

    private static bool TryBuildCapture(
        object inst,
        out PendingRunManager.CustomRunCapture capture,
        out string levelId,
        out string packKey,
        out string incompleteReason)
    {
        levelId = "";
        packKey = "";
        incompleteReason = "unknown";
        capture = default;

        if (!LoggerShared.TryGetCurrentCustomLogContext(out CustomLevelLogContext customCtx))
        {
            incompleteReason = "custom Angry metadata unavailable";
            return false;
        }

        Scratch scratch = GetScratch(inst);

        float savedTime = UltraStatsReflection.TryGetFloat(inst, "savedTime");
        int savedKills = UltraStatsReflection.TryGetInt(inst, "savedKills");
        int savedStyle = UltraStatsReflection.TryGetInt(inst, "savedStyle");
        int totalPoints = UltraStatsReflection.TryGetInt(inst, "totalPoints");

        ChallengeManager? challengeManager = GetChallengeManager();
        bool challengeComplete = challengeManager != null && challengeManager.challengeDone;

        int restarts = scratch.Restarts;
        bool tookDamage = scratch.TookDamage;
        bool majorUsed = scratch.MajorAssistsUsed;

        StatsManager? statsManager = GetStatsManager();
        if (statsManager != null)
        {
            restarts = statsManager.restarts;
            tookDamage = statsManager.tookDamage;
            majorUsed = statsManager.majorUsed;
        }

        bool cheatsUsed = scratch.CheatsUsed;

        string timeRank = NormalizeRankLetter(LoggerShared.StripTags(UltraStatsReflection.TryGetTMPText(inst, "timeRank")));
        string killsRank = NormalizeRankLetter(LoggerShared.StripTags(UltraStatsReflection.TryGetTMPText(inst, "killsRank")));
        string styleRank = NormalizeRankLetter(LoggerShared.StripTags(UltraStatsReflection.TryGetTMPText(inst, "styleRank")));
        string totalRank = NormalizeRankLetter(LoggerShared.StripTags(UltraStatsReflection.TryGetTMPText(inst, "totalRank")));

        levelId = customCtx.LevelId;
        packKey = customCtx.PackFolderKey;

        capture = new PendingRunManager.CustomRunCapture(
            LoggerShared.Difficulty(),
            LoggerShared.GetSaveSlotId(),
            customCtx.PackFolderKey,
            customCtx.LevelFileStem,
            customCtx.LevelId,
            savedTime >= 0f ? Mathf.RoundToInt(savedTime * 1000f) : 0,
            Math.Max(0, savedKills),
            Math.Max(0, savedStyle),
            Math.Max(0, restarts),
            Math.Max(0, totalPoints),
            timeRank,
            killsRank,
            styleRank,
            totalRank,
            tookDamage,
            challengeComplete,
            majorUsed,
            cheatsUsed
        );

        incompleteReason = DescribeIncompleteData(capture);
        return true;
    }

    private static StatsManager? GetStatsManager()
    {
        try { return MonoSingleton<StatsManager>.Instance; }
        catch { return null; }
    }

    private static ChallengeManager? GetChallengeManager()
    {
        try { return MonoSingleton<ChallengeManager>.Instance; }
        catch { return null; }
    }

    private static bool HasCompleteEndscreenData(PendingRunManager.CustomRunCapture capture)
    {
        if (capture.TimeMs <= 0)
            return false;

        if (capture.Points <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(capture.TotalRankRaw) || capture.TotalRankRaw.Length != 1)
            return false;

        if (string.IsNullOrWhiteSpace(capture.TimeRankRaw) ||
            string.IsNullOrWhiteSpace(capture.KillsRankRaw) ||
            string.IsNullOrWhiteSpace(capture.StyleRankRaw))
            return false;

        return true;
    }

    private static string NormalizeRankLetter(string raw)
    {
        raw = (raw ?? "").Trim().ToUpperInvariant();
        return raw.Length == 1 ? raw : "";
    }

    private static string DescribeIncompleteData(PendingRunManager.CustomRunCapture capture)
    {
        if (capture.TimeMs <= 0)
            return "time not populated yet";

        if (capture.Points <= 0)
            return "total points not populated yet";

        if (string.IsNullOrWhiteSpace(capture.TotalRankRaw))
            return "overall rank is empty";

        if (capture.TotalRankRaw.Length != 1)
            return $"overall rank length was {capture.TotalRankRaw.Length} instead of 1";

        if (string.IsNullOrWhiteSpace(capture.TimeRankRaw))
            return "time rank is empty";

        if (string.IsNullOrWhiteSpace(capture.KillsRankRaw))
            return "kills rank is empty";

        if (string.IsNullOrWhiteSpace(capture.StyleRankRaw))
            return "style rank is empty";

        return "custom snapshot is complete";
    }
}

[HarmonyPatch]
internal static class FinalRank_SetInfo_Custom_Patch
{
    private static MethodBase TargetMethod()
    {
        Type t = AccessTools.TypeByName("FinalRank");
        return AccessTools.Method(t, "SetInfo", new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) });
    }

    private static void Prefix(object __instance, int restarts, bool damage, bool majorUsed, bool cheatsUsed)
    {
        if (!Plugin.CustomLevelLoggingEnabled)
            return;

        CustomLevelStatsLogger.NoteInfo(__instance, restarts, damage, majorUsed, cheatsUsed);
    }

    private static void Postfix(object __instance)
    {
        if (!Plugin.CustomLevelLoggingEnabled)
            return;

        CustomLevelStatsLogger.ScheduleQueue(__instance);
    }
}