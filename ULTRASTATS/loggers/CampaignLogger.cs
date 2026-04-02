using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;

namespace ULTRASTATS;

[DataContract]
public sealed class CampLine
{
    [DataMember(Name = "S", Order = 0)] public int S;
    [DataMember(Name = "ID", Order = 1)] public long ID;
    [DataMember(Name = "T", Order = 2)] public long T;
    [DataMember(Name = "t", Order = 3)] public int t;
    [DataMember(Name = "k", Order = 4)] public int k;
    [DataMember(Name = "s", Order = 5)] public int s;
    [DataMember(Name = "r", Order = 6)] public int r;
    [DataMember(Name = "p", Order = 7)] public int p;
    [DataMember(Name = "rs", Order = 8)] public string rs = "";
    [DataMember(Name = "rt", Order = 9)] public string rt = "";
    [DataMember(Name = "td", Order = 10)] public bool td;
    [DataMember(Name = "F", Order = 11, EmitDefaultValue = false)] public int F;
    [DataMember(Name = "c", Order = 12)] public bool c;
}

internal static class CampaignLevelStatsLogger
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
            $"Campaign NoteInfo: restarts={restarts}, damage={damage}, major={majorUsed}, cheats={cheatsUsed}");
    }

    public static void ScheduleQueue(object inst)
    {
        if (!Plugin.CampaignLoggingEnabled || Plugin.Instance == null)
            return;

        Scratch scratch = GetScratch(inst);
        if (scratch.Logged || scratch.CaptureScheduled)
            return;

        LevelContext ctx = LoggerShared.GetCurrentLevelContext();
        if (ctx.Source != LevelSource.Campaign || ctx.IsCustom)
            return;

        scratch.CaptureScheduled = true;
        Plugin.Instance.StartCoroutine(QueueWhenReadyRoutine(inst));
    }

    private static IEnumerator QueueWhenReadyRoutine(object inst)
    {
        const float timeout = 3.0f;
        const float step = 0.05f;

        float waited = 0f;
        PendingRunManager.CampaignRunCapture? completeCapture = null;
        string bestFailureReason = "campaign endscreen data never became complete";

        while (waited < timeout)
        {
            if (TryBuildCapture(inst, out PendingRunManager.CampaignRunCapture capture, out string incompleteReason))
            {
                if (HasCompleteEndscreenData(capture))
                {
                    completeCapture = capture;
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
            BepInExLogs_US.Debug("Campaign queue routine finished without queueing a run because it was already logged.");
            yield break;
        }

        if (completeCapture == null)
        {
            BepInExLogs_US.Debug(() =>
                $"Campaign queue routine timed out. Skipping save because endscreen data stayed incomplete: {bestFailureReason}");
            yield break;
        }

        PendingRunManager.CampaignRunCapture finalCapture = completeCapture.Value;
        scratch.Logged = true;
        PendingRunManager.EnqueueCapture(finalCapture);

        if (inst is Component component)
            PendingRunManager.AttachDiscardWatcher(component);

        BepInExLogs_US.Debug(() =>
            $"Campaign snapshot enqueued: level={finalCapture.LevelId}, time={finalCapture.TimeMs}, points={finalCapture.Points}");
    }

    private static bool TryBuildCapture(object inst, out PendingRunManager.CampaignRunCapture capture, out string incompleteReason)
    {
        incompleteReason = "unknown";
        capture = default;

        LevelContext ctx = LoggerShared.GetCurrentLevelContext();
        if (ctx.Source != LevelSource.Campaign || ctx.IsCustom)
        {
            incompleteReason = "not in a normal campaign level";
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

        capture = new PendingRunManager.CampaignRunCapture(
            LoggerShared.Difficulty(),
            LoggerShared.GetSaveSlotId(),
            ctx.LevelId,
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

    private static bool HasCompleteEndscreenData(PendingRunManager.CampaignRunCapture capture)
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

    private static string DescribeIncompleteData(PendingRunManager.CampaignRunCapture capture)
    {
        if (capture.TimeMs <= 0)
            return "time not populated yet";

        if (capture.Points <= 0)
            return "total points not populated yet";

        if (string.IsNullOrWhiteSpace(capture.TotalRankRaw))
            return "overall rank is empty";

        if (capture.TotalRankRaw.Length != 1)
            return $"overall rank length was {capture.TotalRankRaw.Length} instead of 1";

        return "unknown incomplete state";
    }
}

[HarmonyPatch]
internal static class FinalRank_SetInfo_Patch
{
    private static MethodBase TargetMethod()
    {
        Type t = AccessTools.TypeByName("FinalRank");
        return AccessTools.Method(t, "SetInfo", new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) });
    }

    private static void Prefix(object __instance, int restarts, bool damage, bool majorUsed, bool cheatsUsed)
    {
        if (!Plugin.CampaignLoggingEnabled)
            return;

        CampaignLevelStatsLogger.NoteInfo(__instance, restarts, damage, majorUsed, cheatsUsed);
    }

    private static void Postfix(object __instance)
    {
        if (!Plugin.CampaignLoggingEnabled)
            return;

        CampaignLevelStatsLogger.ScheduleQueue(__instance);
    }
}