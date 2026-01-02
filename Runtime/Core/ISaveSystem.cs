using SAS.Core.TagSystem;
using System.Threading.Tasks;

/// <summary>
/// Interface for a save system that handles loading and saving data asynchronously,
/// supporting binding through the IBindable interface.
/// </summary>
public interface ISaveSystem : IBindable
{
    /// <summary>
    /// Asynchronously loads data of the specified type from the specified directory and file name.
    /// Returns a new instance of the type if no data is found.
    /// </summary>
    /// <typeparam name="T">The type of data to load.</typeparam>
    /// <param name="userId">The user ID associated with the data.</param>
    /// <param name="dirName">The name of the directory containing the file.</param>
    /// <param name="fileName">The name of the file to load data from.</param>
    /// <returns>A Task containing the loaded data or a new instance if not found.</returns>
    Task<T> Load<T>(int userId, string dirName, string fileName) where T : new();

    /// <summary>
    /// Asynchronously saves the specified data to the specified directory and file name.
    /// Overwrites the file if it already exists.
    /// </summary>
    /// <typeparam name="T">The type of data to save.</typeparam>
    /// <param name="userId">The user ID associated with the data.</param>
    /// <param name="dirName">The name of the directory to save the file in.</param>
    /// <param name="fileName">The name of the file to save data to.</param>
    /// <param name="data">The data to be saved.</param>
    /// <returns>A Task representing the asynchronous save operation.</returns>
    Task Save<T>(int userId, string dirName, string fileName, T data);
}
