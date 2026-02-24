using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

public sealed class SaveRequest
{
    public readonly int UserId;
    public readonly string DirName;
    public readonly string FileName;
    public readonly object Data;

    public readonly string Key;
    public readonly TaskCompletionSource<bool> Completion;

    public SaveRequest(int userId, string dirName, string fileName, object data)
    {
        UserId = userId;
        DirName = dirName;
        FileName = fileName;
        Data = data;

        Key = $"{dirName}/{userId}/{fileName}";

        Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
public class SaveQueueManager
{
    private readonly ConcurrentQueue<SaveRequest> _queue = new();
    private readonly ConcurrentDictionary<string, SaveRequest> _pendingByKey = new();

    private readonly Func<SaveRequest, Task> _processor;

    private readonly object _workerLock = new();

    private Task _workerTask;
    private volatile bool _isRunning;
    private volatile bool _isFlushing;

    public SaveQueueManager(Func<SaveRequest, Task> processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public void Enqueue(SaveRequest req)
    {
        // Merge saves (latest replaces previous)
        if (_pendingByKey.TryGetValue(req.Key, out var old))
            old.Completion.TrySetResult(false);

        _pendingByKey[req.Key] = req;
        _queue.Enqueue(req);

        if (_isFlushing)
            return;

        EnsureWorkerRunning();
    }

    private void EnsureWorkerRunning()
    {
        lock (_workerLock)
        {
            if (_isRunning || _isFlushing)
                return;

            _isRunning = true;
            _workerTask = Task.Run(ProcessQueueAsync);
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (_queue.TryDequeue(out var req))
            {
                // Skip outdated merged saves
                if (!_pendingByKey.TryRemove(req.Key, out var latest) || latest != req)
                    continue;

                try
                {
                    await _processor(req).ConfigureAwait(false);
                    req.Completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveQueue] Save failed: {ex}");
                    req.Completion.TrySetResult(false);
                }
            }
        }
        finally
        {
            _isRunning = false;

            if (!_isFlushing && !_queue.IsEmpty)
                EnsureWorkerRunning();
        }
    }

    /// <summary>
    /// Blocks caller until all saves finish.
    /// Call during OnApplicationQuit.
    /// </summary>
    public void Flush()
    {
        lock (_workerLock)
        {
            _isFlushing = true;

            while (_queue.TryDequeue(out var req))
            {
                if (!_pendingByKey.TryRemove(req.Key, out _))
                    continue;

                try
                {
                    _processor(req).GetAwaiter().GetResult();
                    req.Completion.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveQueue] Flush error: {ex}");
                    req.Completion.TrySetResult(false);
                }
            }

            try { _workerTask?.Wait(); }
            catch { }

            _isRunning = false;
            _isFlushing = false;
        }
    }
}