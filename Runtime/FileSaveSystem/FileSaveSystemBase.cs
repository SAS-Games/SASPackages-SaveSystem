using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public abstract class FileSaveSystemBase : ISaveSystem
{
    private string _rootDir;
    private string RootDir => _rootDir;
    protected abstract IDataSerializer Serializer { get; }

    private readonly SaveQueueManager _queueManager;

    protected FileSaveSystemBase(string rootDirPath)
    {
        _rootDir = rootDirPath;
        _queueManager = new SaveQueueManager(ProcessSaveRequestAsync);
    }

    public async Task<T> Load<T>(int userId, string dirName, string fileName) where T : new()
    {
        string filePath =  PathExtensions.GetFilePath(userId, RootDir, dirName, fileName, Serializer.FileExtension);

        if (!File.Exists(filePath))
            return new T();

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
            return Serializer.Deserialize<T>(bytes, userId, dirName, fileName);
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

    public Task DeleteFile(int userId, string dir, string fileName)
    {
        string filePath = PathExtensions.GetFilePath(userId, RootDir, dir, fileName, Serializer.FileExtension);
        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[SaveSystem] File not found: {filePath}");
            return Task.CompletedTask;
        }
        File.Delete(filePath);
        return Task.CompletedTask;
    }

    public Task DeleteDirectory(int userId, string dirName)
    {
        string dirPath = PathExtensions.GetDirectoryPath(userId, RootDir, dirName);
        if (!Directory.Exists(dirPath))
        {
            Debug.LogWarning($"[SaveSystem] Directory not found: {dirPath}");
            return Task.CompletedTask;
        }
        Directory.Delete(dirPath, true);
        return Task.CompletedTask;
    }

    private Task ProcessSaveRequestAsync(SaveRequest req)
    {
        return SaveAtomicAsync(req);
    }

    private async Task SaveAtomicAsync(SaveRequest req)
    {
        string filePath = PathExtensions.GetFilePath(req.UserId, RootDir, req.DirName, req.FileName, Serializer.FileExtension);
        string tempPath = filePath + ".tmp";

        PathExtensions.EnsureDirectoryExists(filePath);

        byte[] bytes = Serializer.Serialize(req.Data, req.UserId, req.DirName, req.FileName);

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
}