namespace UltraSaveSystem
{
    public interface ICustomSaveable
    {
        void OnBeforeSave();
        void OnAfterLoad();
        byte[] SerializeCustomData();
        void DeserializeCustomData(byte[] data);
    }
}