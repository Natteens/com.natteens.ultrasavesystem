using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace UltraSaveSystem
{
    /// <summary>
    ///     Dados de um objeto saveable - COMPATÍVEL COM UNITY JSON
    /// </summary>
    [Serializable]
    public class SaveableData
    {
        public string SaveKey;
        public SavePriority Priority;
        public float SaveTime;
        public bool HasTransform;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        // MUDANÇA: Usar listas em vez de Dictionary
        public List<string> FieldKeys = new();
        public List<string> FieldValues = new(); // Tudo como string para JSON
        public byte[] CustomData;

        // Métodos helper para trabalhar com os dados
        public void SetFieldData(string key, object value)
        {
            var index = FieldKeys.IndexOf(key);
            var stringValue = value?.ToString() ?? "";

            if (index >= 0)
            {
                FieldValues[index] = stringValue;
            }
            else
            {
                FieldKeys.Add(key);
                FieldValues.Add(stringValue);
            }
        }

        public string GetFieldData(string key)
        {
            var index = FieldKeys.IndexOf(key);
            return index >= 0 ? FieldValues[index] : null;
        }

        public bool HasFieldData(string key)
        {
            return FieldKeys.Contains(key);
        }
    }

    /// <summary>
    ///     Arquivo de save completo - COMPATÍVEL COM UNITY JSON
    /// </summary>
    [Serializable]
    public class UltraSaveFile
    {
        public string Version;
        public string SaveTime;
        public long SaveTimeUnix;
        public int ObjectCount;
        public bool IsEncrypted;
        public string PlayerName;
        public string DeviceInfo;

        // MUDANÇA: Lista em vez de Dictionary
        public List<SaveableData> Objects = new();
    }

    public struct SaveableMetadata
    {
        public string SaveKey;
        public bool SaveTransform;
        public bool AutoSave;
        public SavePriority Priority;
        public FieldInfo[] SaveableFields;
    }

    public struct SavedObjectData
    {
        public object Instance;
        public SaveableMetadata Metadata;
        public GameObject GameObject;
        public float LastSaveTime;
    }

    public struct TransformJobData
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
    }
}