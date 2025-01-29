namespace BeyondTools.VFS
{
    public enum EVFSLoaderPosType : byte
    {
        None,
        PersistAsset,
        StreamAsset,
        VFS = 10,
        VFS_PersistAsset,
        VFS_StreamAsset,
        VFS_Build
    }
}
