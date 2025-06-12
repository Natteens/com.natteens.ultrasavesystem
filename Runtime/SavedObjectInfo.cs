using System;
using UnityEngine;

namespace UltraSaveSystem
{
    [Serializable]
    public class SavedObjectInfo
    {
        public string saveKey;
        public string typeName;
        public string sceneName;
        public Vector3 position;
        public bool hasTransform;
        public DateTime lastSaved;
        public int fieldCount;
        public long dataSize;
        
        public SavedObjectInfo(string key, string type, string scene)
        {
            saveKey = key;
            typeName = type;
            sceneName = scene;
            lastSaved = DateTime.Now;
        }
    }
}