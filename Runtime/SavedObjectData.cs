using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltraSaveSystem
{
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