using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class LevelJsonData
{
    public int schemaVersion = 1;
    public string id = "level-01";
    public string displayName = "Level 1";
    public int startingCoins = 75;
    public float timelineDuration = 60f;
    public bool showCardSelectionOnStart = true;
    public bool waitForCardSelectionBeforeLoadingCards = true;
    public LevelCardRulesJson cardRules = new();
    public List<LevelEnemySpawnJson> enemySpawns = new();
}

[Serializable]
public sealed class LevelCardRulesJson
{
    public bool restrictAvailableCards;
    public List<string> allowedCards = new();
    public List<string> bannedCards = new();
}

[Serializable]
public sealed class LevelEnemySpawnJson
{
    public float time;
    public string enemy = "Goblin";
    public int lane = 3;
    public float spawnX = 8.5f;
    public LevelVector3Json offset = new();
}

[Serializable]
public sealed class LevelVector3Json
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

public static class LevelJsonUtility
{
    public const int CurrentSchemaVersion = 1;

    public static bool TryParse(string json, out LevelJsonData data, out string error)
    {
        data = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Level JSON is empty.";
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<LevelJsonData>(json);
        }
        catch (Exception exception)
        {
            error = $"Invalid level JSON: {exception.Message}";
            return false;
        }

        if (data == null)
        {
            error = "Level JSON could not be parsed.";
            return false;
        }

        Normalize(data);
        return Validate(data, out error);
    }

    public static bool TryReadExternal(string location, out string json, out string error)
    {
        json = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(location))
        {
            error = "External JSON location is empty.";
            return false;
        }

        string path = ResolveExternalPath(location);
        if (!File.Exists(path))
        {
            error = $"Level JSON was not found: {path}";
            return false;
        }

        try
        {
            json = File.ReadAllText(path);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not read level JSON: {exception.Message}";
            return false;
        }
    }

    public static string ResolveExternalPath(string location)
    {
        if (Path.IsPathRooted(location))
        {
            return location;
        }

        string persistentPath = Path.Combine(Application.persistentDataPath, location);
        if (File.Exists(persistentPath))
        {
            return persistentPath;
        }

        return Path.Combine(Application.streamingAssetsPath, location);
    }

    private static void Normalize(LevelJsonData data)
    {
        data.schemaVersion = data.schemaVersion <= 0 ? CurrentSchemaVersion : data.schemaVersion;
        data.id = string.IsNullOrWhiteSpace(data.id) ? "custom-level" : data.id.Trim();
        data.displayName = string.IsNullOrWhiteSpace(data.displayName) ? data.id : data.displayName.Trim();
        data.startingCoins = Mathf.Max(0, data.startingCoins);
        data.timelineDuration = Mathf.Max(5f, data.timelineDuration <= 0f ? 60f : data.timelineDuration);
        data.cardRules ??= new LevelCardRulesJson();
        data.cardRules.allowedCards ??= new List<string>();
        data.cardRules.bannedCards ??= new List<string>();
        data.enemySpawns ??= new List<LevelEnemySpawnJson>();

        NormalizeNames(data.cardRules.allowedCards);
        NormalizeNames(data.cardRules.bannedCards);
        for (int i = 0; i < data.enemySpawns.Count; i++)
        {
            LevelEnemySpawnJson spawn = data.enemySpawns[i];
            if (spawn == null)
            {
                continue;
            }

            spawn.time = Mathf.Max(0f, spawn.time);
            spawn.enemy = spawn.enemy?.Trim() ?? string.Empty;
            spawn.lane = Mathf.Clamp(spawn.lane, 1, 5);
            spawn.offset ??= new LevelVector3Json();
        }
    }

    private static void NormalizeNames(List<string> names)
    {
        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
        for (int i = names.Count - 1; i >= 0; i--)
        {
            string value = names[i]?.Trim();
            if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
            {
                names.RemoveAt(i);
            }
            else
            {
                names[i] = value;
            }
        }
    }

    private static bool Validate(LevelJsonData data, out string error)
    {
        if (data.schemaVersion > CurrentSchemaVersion)
        {
            error = $"Unsupported level schema version {data.schemaVersion}.";
            return false;
        }

        for (int i = 0; i < data.enemySpawns.Count; i++)
        {
            LevelEnemySpawnJson spawn = data.enemySpawns[i];
            if (spawn == null)
            {
                error = $"Enemy spawn {i + 1} is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(spawn.enemy))
            {
                error = $"Enemy spawn {i + 1} has no enemy id.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}

public static class LevelLoadRequest
{
    public static string Json { get; private set; }
    public static string Source { get; private set; }
    public static int LevelNumber { get; private set; }
    public static bool HasJson => !string.IsNullOrWhiteSpace(Json);

    public static void Set(string json, string source, int levelNumber)
    {
        Json = json;
        Source = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
        LevelNumber = Mathf.Max(1, levelNumber);
    }

    public static void Clear()
    {
        Json = null;
        Source = null;
        LevelNumber = 0;
    }
}
