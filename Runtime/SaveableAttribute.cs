using System;

namespace UltraSaveSystem
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class SaveableAttribute : Attribute
    {
        public SaveableAttribute(string saveKey = null, bool saveTransform = false,
            bool autoSave = false, SavePriority priority = SavePriority.Normal)
        {
            SaveKey = saveKey;
            SaveTransform = saveTransform;
            AutoSave = autoSave;
            Priority = priority;
        }

        public string SaveKey { get; }
        public bool SaveTransform { get; }
        public bool AutoSave { get; }
        public SavePriority Priority { get; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SaveFieldAttribute : Attribute
    {
        public SaveFieldAttribute(string customKey = null, bool encrypted = false)
        {
            CustomKey = customKey;
            Encrypted = encrypted;
        }

        public string CustomKey { get; }
        public bool Encrypted { get; }
    }

    public enum SavePriority : byte
    {
        Critical = 0,
        High = 1,
        Normal = 2,
        Low = 3
    }
}