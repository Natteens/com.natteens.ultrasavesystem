using System;
using System.Collections.Generic;

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
        public bool isCompressed;
        public List<SavedObjectData> objects = new();
    }
}