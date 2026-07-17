public enum LevelSceneMode
{
    Normal,
    LevelEditor,
    Sandbox,
    Endless
}

public static class LevelSceneModeRequest
{
    public static LevelSceneMode Mode { get; private set; } = LevelSceneMode.Normal;

    public static LevelSceneMode ActiveMode { get; private set; } = LevelSceneMode.Normal;

    public static bool HasRequest { get; private set; }

    public static bool IsSandbox => ActiveMode == LevelSceneMode.Sandbox;

    public static bool IsEndless => ActiveMode == LevelSceneMode.Endless;

    public static void Set(LevelSceneMode mode)
    {
        Mode = mode;
        ActiveMode = mode;
        HasRequest = true;
    }

    public static void Clear()
    {
        Mode = LevelSceneMode.Normal;
        ActiveMode = LevelSceneMode.Normal;
        HasRequest = false;
    }

    public static LevelSceneMode ConsumeRequestOrDefault()
    {
        LevelSceneMode mode = HasRequest ? Mode : LevelSceneMode.Normal;
        ActiveMode = mode;
        HasRequest = false;
        Mode = LevelSceneMode.Normal;
        return mode;
    }
}
