using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using WorldGeneration;

namespace TerrainGeneration.TerrainUtils {

[Serializable]
public class ChunkSaveData {
    
    private static BinaryFormatter binaryFormatter;
    private static string dataPath = Path.Combine(Application.persistentDataPath, "Saves", "First", "ChunkData");
    private static int formatVersion = 1;

    public float[] densityMap;
    public string fileKey;
    public ChunkKey chunkKey;

    public static string GetFilePath (string fileKey, ChunkKey chunkKey) {
        return Path.Combine(dataPath, $"{formatVersion}-{fileKey}-{chunkKey.origin}.bin");
    }

    public static void DeleteAll () {
        var directory = new DirectoryInfo(dataPath);

        foreach (var file in directory.GetFiles()) {
            file.Delete();
        }
    }

    public static ChunkSaveData Load (string fileKey, ChunkKey chunkKey) {
        binaryFormatter ??= new BinaryFormatter();

        var filePath = GetFilePath(fileKey, chunkKey);
        if (!File.Exists(filePath)) {
            return null;
        }

        using var filestream = File.Open(filePath, FileMode.Open, FileAccess.Read);

        try {
            return binaryFormatter.Deserialize(filestream) as ChunkSaveData;
        }
        catch (Exception) {
            return null;
        }
    }

    public static void Save (ChunkSaveData data) {
        binaryFormatter ??= new BinaryFormatter();

        var filePath = GetFilePath(data.fileKey, data.chunkKey);
        var directoryPath = Path.GetDirectoryName(filePath);

        if (directoryPath == null) {
            throw new Exception($"Unable to get directory path from '{filePath}'");
        }

        Directory.CreateDirectory(directoryPath);
        
        using var filestream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write);
        
        binaryFormatter.Serialize(filestream, data);
    }

}

}