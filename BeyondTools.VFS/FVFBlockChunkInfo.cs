using System.Text.Json.Serialization;

namespace BeyondTools.VFS
{
    public struct FVFBlockChunkInfo
    {
        public const string FILE_EXTENSION = ".chk";

        public UInt128 md5Name;
        public UInt128 contentMD5;
        public long length;
        public EVFSBlockType blockType;
        [JsonIgnore]
        public FVFBlockFileInfo[] files;
    }
}
