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
    
    public Task<bool> Save<T>(int userId, string dirName, string fileName, T data)
    {
        var request = new SaveRequest(userId, dirName, fileName, data);
        _queueManager.Enqueue(request);
        return request.Completion.Task;
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
            if (File.Exists(filePath))
                File.Delete(filePath);

            File.Move(tempPath, filePath);
        }
    }
    
    private string GetFilePath(int userId, string dirName, string fileName)
    {
        return Path.Combine(_rootDir, userId.ToString(), dirName, fileName + Serializer.FileExtension);
    }

    private void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
    
    public void Flush()
    {
        _queueManager.Flush();
    }
}