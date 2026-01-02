using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
///  FlexPrefs:
/// - Lock-free during gameplay (main-thread-only API)
/// - Snapshot-based saves (safe background writes)
/// - Version counter to detect concurrent changes
/// - Works with queued ISaveSystem implementations (fire-and-forget) or awaitable Save Tasks
/// </summary>
public static class FlexPrefs
{
    private static ISaveSystem _saveSystem;
    private static int _activeUserId;

    // Per-cache entry holds the live dictionary, version counter and flags.
    private class CacheEntry
    {
        public Dictionary<string, object> Data = new Dictionary<string, object>();
        public int Version = 0; // increments on every mutation
        public bool IsDirty = false; // set when mutated, cleared when snapshot is taken
        public bool IsSaving = false; // true while awaiting save completion (if awaitable)
    }

    private static readonly Dictionary<(int userId, string file), CacheEntry> _entries = new();

    private const string DefaultFile = "FlexPrefsData";
    private const string DirName = "FlexPrefsDataDir";

    public static void Initialize(ISaveSystem saveSystem, int defaultUserId = 0)
    {
        _saveSystem = saveSystem ?? throw new ArgumentNullException(nameof(saveSystem));
        _activeUserId = defaultUserId;
    }

    public static void SetActiveUser(int userId) => _activeUserId = userId;

    /// <summary>
    /// Preloads the file for a user. Must be called before using Get/Set for that user+file.
    /// </summary>
    public static async Task PreloadUserAsync(int userId, string fileName = DefaultFile)
    {
        EnsureInitialized();
        var key = (userId, fileName);
        if (_entries.ContainsKey(key)) return;

        try
        {
            var data = await _saveSystem.Load<Dictionary<string, object>>(userId, DirName, fileName)
                           .ConfigureAwait(false)
                       ?? new Dictionary<string, object>();

            var entry = new CacheEntry
            {
                Data = new Dictionary<string, object>(data),
                Version = 0,
                IsDirty = false,
                IsSaving = false
            };

            _entries[key] = entry;
            Debug.Log($"[FlexPrefs] Preloaded data for user {userId} ({fileName}).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FlexPrefs] Failed to preload data for user {userId}/{fileName}: {ex}");
            _entries[key] = new CacheEntry();
        }
    }

    public static Task PreloadActiveUserAsync(string fileName = DefaultFile)
        => PreloadUserAsync(_activeUserId, fileName);

    public static T Get<T>(int userId, string key, T defaultValue = default, string fileName = DefaultFile)
    {
        var cacheKey = (userId, fileName);
        if (!_entries.TryGetValue(cacheKey, out var entry))
        {
            Debug.LogWarning($"[FlexPrefs] Cache not loaded for user {userId}. Returning default for key '{key}'.");
            return defaultValue;
        }

        if (entry.Data.TryGetValue(key, out var boxed) && boxed is T typed) return typed;
        return defaultValue;
    }

    public static void Set<T>(int userId, string key, T value, string fileName = DefaultFile)
    {
        var cacheKey = (userId, fileName);
        if (!_entries.TryGetValue(cacheKey, out var entry))
        {
            Debug.LogWarning($"[FlexPrefs] Cache not loaded for user {userId}. Skipping Set('{key}').");
            return;
        }

        entry.Data[key] = value;
        entry.Version++; // mutation
        entry.IsDirty = true; // mark dirty so next save will persist
    }

    // convenience for active user
    public static T Get<T>(string key, T defaultValue = default, string fileName = DefaultFile)
        => Get(_activeUserId, key, defaultValue, fileName);

    public static void Set<T>(string key, T value, string fileName = DefaultFile)
        => Set(_activeUserId, key, value, fileName);

    public static void DeleteKey(int userId, string key, string fileName = DefaultFile)
    {
        var cacheKey = (userId, fileName);
        if (!_entries.TryGetValue(cacheKey, out var entry)) return;

        if (entry.Data.Remove(key))
        {
            entry.Version++;
            entry.IsDirty = true;
        }
    }

    public static void DeleteKey(string key, string fileName = DefaultFile)
        => DeleteKey(_activeUserId, key, fileName);

    public static void Clear(int userId, string fileName = DefaultFile)
    {
        var cacheKey = (userId, fileName);
        if (!_entries.TryGetValue(cacheKey, out var entry)) return;

        if (entry.Data.Count > 0)
        {
            entry.Data.Clear();
            entry.Version++;
            entry.IsDirty = true;
        }
    }

    public static void Clear(string fileName = DefaultFile)
        => Clear(_activeUserId, fileName);

    /// <summary>
    /// Creates a snapshot of the current cache and asks the save system to persist it.
    /// This method is very fast (snapshot is a shallow copy) and non-blocking.
    /// If the underlying ISaveSystem returns a Task that completes on disk write, FlexPrefs will await it
    /// to update IsSaving; otherwise Save behaves as fire-and-forget (still safe).
    /// </summary>
    public static Task Save(int userId, string fileName = DefaultFile)
    {
        EnsureInitialized();
        var cacheKey = (userId, fileName);
        if (!_entries.TryGetValue(cacheKey, out var entry))
            return Task.CompletedTask;

        // If nothing changed, skip
        if (!entry.IsDirty)
            return Task.CompletedTask;

        // Snapshot and capture version — shallow copy on main thread (cheap)
        var snapshot = new Dictionary<string, object>(entry.Data);
        var snapshotVersion = entry.Version;

        // Mark not-dirty for now (any future Set will set IsDirty=true and bump version)
        entry.IsDirty = false;

        // If underlying save returns an awaitable Task that completes on write, track IsSaving.
        var saveTask = _saveSystem.Save(userId, DirName, fileName, snapshot);

        // If saveTask is already completed (likely in our save queue design), we don't await.
        if (saveTask.IsCompleted)
        {
            // Fire-and-forget path (common for queued save systems).
            // We still return the task (already completed).
            return Task.CompletedTask;
        }

        // If the save returns an awaitable Task (completes when disk write finishes),
        // we track IsSaving and update flags after completion.
        entry.IsSaving = true;
        return AwaitSaveCompletion(entry, snapshotVersion, saveTask);
    }

    // Helper to await the save task and update IsSaving / IsDirty properly.
    private static async Task AwaitSaveCompletion(CacheEntry entry, int snapshotVersion, Task saveTask)
    {
        try
        {
            await saveTask.ConfigureAwait(false);
            // If no newer changes happened while saving (version unchanged), keep IsDirty=false.
            // If new changes happened, leave IsDirty=true so future Save will pick them up.
            if (entry.Version == snapshotVersion)
                entry.IsDirty = false;
            else
                entry.IsDirty = true;
        }
        catch (Exception ex)
        {
            // On failure, mark dirty so next Save will retry.
            entry.IsDirty = true;
            Debug.LogError($"[FlexPrefs] Save task failed: {ex}");
        }
        finally
        {
            entry.IsSaving = false;
        }
    }

    public static Task Save(string fileName = DefaultFile)
        => Save(_activeUserId, fileName);

    public static async Task SaveAll()
    {
        EnsureInitialized();
        // Snapshot keys to avoid mutation during iteration
        var keys = _entries.Keys.ToList();
        var tasks = new List<Task>(keys.Count);
        foreach (var key in keys)
            tasks.Add(Save(key.userId, key.file));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public static bool IsUserDataLoaded(int userId, string fileName = DefaultFile)
        => _entries.ContainsKey((userId, fileName));

    public static bool IsActiveUserDataLoaded(string fileName = DefaultFile)
        => IsUserDataLoaded(_activeUserId, fileName);

    public static bool HasKey(int userId, string key, string fileName = DefaultFile)
    {
        if (!_entries.TryGetValue((userId, fileName), out var entry)) return false;
        return entry.Data.ContainsKey(key);
    }

    public static bool HasKey(string key, string fileName = DefaultFile)
        => HasKey(_activeUserId, key, fileName);

    public static void UnloadUser(int userId, string fileName = DefaultFile)
    {
        _entries.Remove((userId, fileName));
    }

    public static void UnloadActiveUser(string fileName = DefaultFile)
        => UnloadUser(_activeUserId, fileName);

    private static void EnsureInitialized()
    {
        if (_saveSystem == null)
            throw new InvalidOperationException(
                "FlexPrefs not initialized. Call FlexPrefs.Initialize(saveSystem) first.");
    }
}