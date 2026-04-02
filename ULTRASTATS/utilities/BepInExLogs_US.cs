using System;
using System.Runtime.CompilerServices;

namespace ULTRASTATS;

internal static class BepInExLogs_US
{
    private static bool DebugEnabled => Plugin.DebugLoggingEnabled;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ModLoaded() =>
        Plugin.Log.LogInfo("[ULTRASTATS] : Mod loaded");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Queue(string message) =>
        Plugin.Log.LogInfo("[Queue] : " + message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QueueCleared() =>
        Queue("Queue has been cleared!");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void QueueAppended(long id, string path) =>
        Queue($"Run {id} has been appended to {path}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(string message)
    {
        if (!DebugEnabled)
            return;

        Plugin.Log.LogInfo("[Debug] : " + message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(Func<string> messageFactory)
    {
        if (!DebugEnabled)
            return;

        Plugin.Log.LogInfo("[Debug] : " + messageFactory());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warn(string message) =>
        Plugin.Log.LogWarning("[ULTRASTATS] : " + message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(string message) =>
        Plugin.Log.LogError("[ULTRASTATS] : " + message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(string context, Exception ex) =>
        Plugin.Log.LogError($"[ULTRASTATS] : {context}: {ex}");
}