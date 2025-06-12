using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltraSaveSystem
{
    [Serializable]
    public class UltraSaveFile
    {
        public string version;
        public int slot;
        public bool isAutoSave;
        public string playerName;
        public string sceneName;
        public DateTime saveTime;
        public bool isEncrypted;
        public List<SavedObjectData> objects;
    }
    
    [Serializable]
    public class SavedObjectData
    {
        public string saveKey;
        public bool hasTransform;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Dictionary<string, object> fieldData;
        public byte[] customData;
    }
}