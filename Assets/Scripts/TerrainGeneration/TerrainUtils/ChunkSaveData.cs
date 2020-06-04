using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WorldGeneration {

[Serializable]
public class ChunkSaveData {

    private static BinaryFormatter binaryFormatter;
    private static string dataPath = Path.Combine(Application.persistentDataPath, "Saves", "First", "ChunkData");

    public string key;
    public float3 position; 
    public float[] noiseMap;

    public static string GetFilePath (string key, float3 position) {
        return Path.Combine(dataPath, $"{key}-{(int3) position}.bin");
    }

    public static void DeleteAll () {
        var directory = new DirectoryInfo(dataPath);

        foreach (var file in directory.GetFiles()) {
            file.Delete();
        }
    }

    public static ChunkSaveData Load (string key, float3 position) {
        binaryFormatter ??= new BinaryFormatter();

        var filePath = GetFilePath(key, position);
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

        var filePath = GetFilePath(data.key, data.position);
        
        using var filestream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write);
        
        binaryFormatter.Serialize(filestream, data);
    }

}

}