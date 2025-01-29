namespace BeyondTools.VFS
{
    public struct FVFBlockFileInfo
    {
        public string fileName;
        public long fileNameHash;
        public UInt128 fileChunkMD5Name;
        public UInt128 fileDataMD5;
        public long offset;
        public long len;
        public EVFSBlockType blockType;
        public bool bUseEncrypt;
        public long ivSeed;
        public bool bIsDirect;
        public EVFSLoaderPosType loaderPosType;
    }
}
