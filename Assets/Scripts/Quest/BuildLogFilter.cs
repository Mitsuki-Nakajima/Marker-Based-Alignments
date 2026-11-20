using UnityEngine;

/// <summary>
/// Several prototype scripts (FakeMarkerProvider, SharedPoseDebug) call Debug.Log
/// every frame. On Quest that logcat traffic costs frame time, so in player builds
/// this drops plain logs and keeps warnings/errors. The Editor is left unchanged.
/// Runs before scene load, so it needs no GameObject in the scene.
/// </summary>
public static class BuildLogFilter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (Application.isEditor || Debug.isDebugBuild)
            return;

        Debug.unityLogger.filterLogType = LogType.Warning;
    }
}
