using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public sealed class SaveRequest
{
    public int UserId { get; }
    public string DirName { get; }
    public string FileName { get; }
    public object Data { get; }

    public string Key { get; }

    internal TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<bool> Task => Completion.Task;

    public SaveRequest(int userId, string dirName, string fileName, object data)
    {
        if (string.IsNullOrWhiteSpace(dirName))
            throw new ArgumentException("Directory name cannot be null or empty.", nameof(dirName));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

        UserId = userId;
        DirName = dirName;
        FileName = fileName;
        Data = data ?? throw new ArgumentNullException(nameof(data));

        Key = $"{DirName}/{UserId}/{FileName}";
    }
}

public sealed class SaveQueueManager
{
    private readonly object _gate = new();

    private readonly Queue<SaveRequest> _queue = new();
    private readonly Dictionary<string, SaveRequest> _pendingByKey = new();

    private readonly Func<SaveRequest, Task> _processor;

    private bool _isProcessing;

    public SaveQueueManager(Func<SaveRequest, Task> processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public Task<bool> Enqueue(SaveRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        SaveRequest supersededRequest = null;
        var startProcessor = false;

        lock (_gate)
        {
            if (_pendingByKey.TryGetValue(request.Key, out supersededRequest))
            {
                Debug.Log($"[SaveQueue] Replace → {request.Key}");
            }

            _pendingByKey[request.Key] = request;
            _queue.Enqueue(request);

            if (!_isProcessing)
            {
                _isProcessing = true;
                startProcessor = true;
            }
        }

        // This request's exact data will not be saved.
        supersededRequest?.Completion.TrySetCanceled();

        if (startProcessor)
        {
            _ = ProcessAsync();
        }

        return request.Task;
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            SaveRequest request;

            lock (_gate)
            {
                request = GetNextValidRequestLocked();

                if (request == null)
                {
                    _isProcessing = false;
                    return;
                }

                /*
                 * Remove before processing.
                 *
                 * If another request with the same key is enqueued while
                 * this one is being written, it becomes a new pending save
                 * and will run after the current save.
                 */
                _pendingByKey.Remove(request.Key);
            }

            try
            {
                await _processor(request);

                request.Completion.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                request.Completion.TrySetCanceled();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[SaveQueue] ERROR {request.Key}\n{exception}");

                request.Completion.TrySetException(exception);
            }
        }
    }

    private SaveRequest GetNextValidRequestLocked()
    {
        while (_queue.Count > 0)
        {
            var request = _queue.Dequeue();

            if (_pendingByKey.TryGetValue(
                    request.Key,
                    out var latest) &&
                ReferenceEquals(latest, request))
            {
                return request;
            }

            // This request was replaced by a newer request.
        }

        return null;
    }
}