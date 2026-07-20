using System.IO;

public static class PathExtensions
{
    public static string GetDirectoryPath(int userId, string rootDir, string dirName)
    {
        return Path.Combine(rootDir, userId.ToString(), dirName);
    }
    
    public static string GetFilePath(int userId, string rootDir, string dirName, string fileName, string extension)
    {
        return Path.Combine(rootDir, userId.ToString(), dirName, fileName + extension);
    }

    public static void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}