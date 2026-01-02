using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public abstract class FileSaveSystemBase : ISaveSystem
{
    private readonly string _rootDir = Application.persistentDataPath;
    protected abstract IDataSerializer Serializer { get; }

    private readonly SaveQueueManager _queueManager;

    protected FileSaveSystemBase()
    {
        _queueManager = new SaveQueueManager(ProcessSaveRequestAsync);
    }

    public async Task<T> Load<T>(int userId, string dirName, string fileName) where T : new()
    {
        string filePath = GetFilePath(userId, dirName, fileName);

        if (!File.Exists(filePath))
            return new T();

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            return Serializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Load failed: {ex}");
            return new T();
        }
    }

    public Task Save<T>(int userId, string dirName, string fileName, T data)
    {
        _queueManager.Enqueue(new SaveRequest(userId, dirName, fileName, data));
        return Task.CompletedTask;
    }

    private Task ProcessSaveRequestAsync(SaveRequest req)
    {
        return SaveAtomicAsync(req);
    }

    private async Task SaveAtomicAsync(SaveRequest req)
    {
        string filePath = GetFilePath(req.UserId, req.DirName, req.FileName);
        string tempPath = filePath + ".tmp";

        EnsureDirectoryExists(filePath);

        try
        {
            byte[] bytes = Serializer.Serialize(req.Data);
            await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);

            try
            {
                if (File.Exists(filePath))
                    File.Replace(tempPath, filePath, null);
                else
                    File.Move(tempPath, filePath);
            }
            catch
            {
                // Fallback: try to remove the old file and move the temp in place
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tempPath, filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Atomic save failed: {ex}");
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private string GetFilePath(int userId, string dirName, string fileName)
    {
        return Path.Combine(_rootDir, dirName, userId.ToString(), fileName + Serializer.FileExtension);
    }

    private void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Drain the queue synchronously on the caller thread. Blocks until queue is empty.
    /// Typically called on application quit to guarantee all saves complete.
    /// </summary>
    public void Flush()
    {
        _queueManager.Flush();
    }
}