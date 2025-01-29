using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace BeyondTools.VFS
{
    public class VFBlockMainInfo
    {
        public VFBlockMainInfo(byte[] bytes, int offset = 0)
        {
            version = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
            offset += sizeof(int);
            // TODO: CRC and stuff idk
            offset += 12;

            ushort groupCfgNameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
            offset += sizeof(ushort);
            groupCfgName = Encoding.UTF8.GetString(bytes.AsSpan(offset, groupCfgNameLength));
            offset += groupCfgNameLength;

            groupCfgHashName = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
            offset += sizeof(long);

            groupFileInfoNum = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
            offset += sizeof(int);

            groupChunksLength = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
            offset += sizeof(long);

            blockType = (EVFSBlockType)bytes[offset++];

            var chunkCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
            allChunks = GC.AllocateUninitializedArray<FVFBlockChunkInfo>(chunkCount);
            offset += sizeof(int);

            foreach (ref var chunk in allChunks.AsSpan())
            {
                chunk.md5Name = BinaryPrimitives.ReadUInt128LittleEndian(bytes.AsSpan(offset));
                offset += Marshal.SizeOf<UInt128>();

                chunk.contentMD5 = BinaryPrimitives.ReadUInt128LittleEndian(bytes.AsSpan(offset));
                offset += Marshal.SizeOf<UInt128>();

                chunk.length = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
                offset += sizeof(long);

                chunk.blockType = (EVFSBlockType)bytes[offset++];

                var fileCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
                chunk.files = GC.AllocateUninitializedArray<FVFBlockFileInfo>(fileCount);
                offset += sizeof(int);

                foreach (ref var file in chunk.files.AsSpan())
                {
                    ushort fileNameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
                    offset += sizeof(ushort);
                    file.fileName = Encoding.UTF8.GetString(bytes.AsSpan(offset, fileNameLength));
                    offset += fileNameLength;

                    file.fileNameHash = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
                    offset += sizeof(long);

                    file.fileChunkMD5Name = BinaryPrimitives.ReadUInt128LittleEndian(bytes.AsSpan(offset));
                    offset += Marshal.SizeOf<UInt128>();

                    file.fileDataMD5 = BinaryPrimitives.ReadUInt128LittleEndian(bytes.AsSpan(offset));
                    offset += Marshal.SizeOf<UInt128>();

                    file.offset = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
                    offset += sizeof(long);

                    file.len = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
                    offset += sizeof(long);

                    file.blockType = (EVFSBlockType)bytes[offset++];
                    file.bUseEncrypt = Convert.ToBoolean(bytes[offset++]);
                    if (file.bUseEncrypt)
                    {
                        file.ivSeed = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset));
                        offset += sizeof(long);
                    }
                }
            }

        }

        public int version;
        public string groupCfgName;
        public long groupCfgHashName;
        public int groupFileInfoNum;
        public long groupChunksLength;
        public EVFSBlockType blockType;
        public FVFBlockChunkInfo[] allChunks;
    }
}
