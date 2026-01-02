using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

public struct SaveRequest
{
    public int UserId;
    public string DirName;
    public string FileName;
    public object Data;


    public SaveRequest(int userId, string dirName, string fileName, object data)
    {
        UserId = userId;
        DirName = dirName;
        FileName = fileName;
        Data = data;
    }
}

public class SaveQueueManager
{
    private readonly ConcurrentQueue<SaveRequest> _queue = new ConcurrentQueue<SaveRequest>();
    private readonly Func<SaveRequest, Task> _processor;

    private Task _backgroundTask;
    private readonly object _workerLock = new object();
    private volatile bool _isRunning = false;
    private volatile bool _isFlushing = false;

    public SaveQueueManager(Func<SaveRequest, Task> processor)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    }

    public void Enqueue(SaveRequest req)
    {
        _queue.Enqueue(req);

        if (_isFlushing)
            return;

        EnsureWorkerRunning();
    }

    private void EnsureWorkerRunning()
    {
        lock (_workerLock)
        {
            if (_isFlushing) return;        // Prevent race with Flush
            if (_isRunning) return;

            _isRunning = true;
            _backgroundTask = Task.Run(ProcessQueueAsync);
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (_queue.TryDequeue(out var req))
            {
                try
                {
                    await _processor(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveQueue] Error processing save: {ex}");
                }
            }
        }
        finally
        {
            _isRunning = false;

            // If more items were added during a race window, restart worker
            if (!_isFlushing && !_queue.IsEmpty)
                EnsureWorkerRunning();
        }
    }

    /// <summary>
    /// Flushes all save requests. Blocks calling thread until complete.
    /// Safe to call from OnApplicationQuit.
    /// </summary>
    public void Flush()
    {
        lock (_workerLock)
        {
            _isFlushing = true;

            // Drain queue synchronously
            while (_queue.TryDequeue(out var req))
            {
                try
                {
                    var task = _processor(req);
                    task.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveQueue] Flush error: {ex}");
                }
            }

            // Ensure background worker finishes
            try
            {
                _backgroundTask?.Wait();
            }
            catch { }

            _isRunning = false;
            _isFlushing = false;
        }
    }
}