using System;

namespace UltraSaveSystem
{
    /// <summary>
    ///     Atributo para marcar classes como saveáveis
    /// </summary>
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

    /// <summary>
    ///     Atributo para marcar campos como saveáveis
    /// </summary>
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

    /// <summary>
    ///     Prioridade de salvamento
    /// </summary>
    public enum SavePriority : byte
    {
        Critical = 0, // Salva primeiro
        High = 1,
        Normal = 2,
        Low = 3 // Salva por último
    }

    /// <summary>
    ///     Interface para objetos com salvamento customizado
    /// </summary>
    public interface ICustomSaveable
    {
        void OnBeforeSave();
        void OnAfterLoad();
        byte[] SerializeCustomData();
        void DeserializeCustomData(byte[] data);
    }
}