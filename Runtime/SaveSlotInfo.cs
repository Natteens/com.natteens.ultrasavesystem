using System;

namespace UltraSaveSystem
{
    [Serializable]
    public class SaveSlotInfo
    {
        public int slot;
        public bool exists;
        public DateTime lastModified;
        public long sizeBytes;
    }
}