using BeyondTools.VFS.Crypto;
using BeyondTools.VFS.Extensions;
using ConsoleAppFramework;
using System.IO.Hashing;
using System.Text;

namespace BeyondTools.VFS;
internal class VFSDump
{
    static readonly Dictionary<EVFSBlockType, string> blockTypeMap = new()
    {
        { EVFSBlockType.Audio, "MainAudio" },
        { EVFSBlockType.Bundle, "MainBundles" },
        { EVFSBlockType.BundleManifest, "BundleManifest" },
        { EVFSBlockType.IFixPatch, "IFixPatchOut" },
        { EVFSBlockType.InitialAudio, "InitAudio" },
        { EVFSBlockType.InitialBundle, "InitBundles" },
        { EVFSBlockType.IV, "IV" },
        { EVFSBlockType.LowShader, "LowShader" },
        // { EVFSBlockType.Raw, "" }, Not present
        { EVFSBlockType.Streaming, "Streaming" },
        { EVFSBlockType.TextAsset, "TextAsset" },
        { EVFSBlockType.Video, "Video" },
    };

    static void Main(string[] args)
    {
        ConsoleApp.Run(args, (
            [Argument] string streamingAssetsPath,
            [Argument] EVFSBlockType dumpAssetType = EVFSBlockType.All,
            [Argument] string? outputDir = null) =>
        {
            streamingAssetsPath = Path.Combine(streamingAssetsPath, VFSDefine.VFS_DIR);
            outputDir ??= Path.Combine(AppContext.BaseDirectory, "Assets");
            if (dumpAssetType == EVFSBlockType.All)
            {
                foreach (var type in blockTypeMap.Keys)
                {
                    DumpAssetByType(streamingAssetsPath, type, outputDir);
                }
            }
            else
            {
                DumpAssetByType(streamingAssetsPath, dumpAssetType, outputDir);
            }
        });
    }

    private static void DumpAssetByType(string streamingAssetsPath, EVFSBlockType dumpAssetType, string outputDir)
    {
        Console.WriteLine("Dumping {0} files...", dumpAssetType.ToString());

        // TODO: This is a temporary solution that makes this thing only worked on some EVFSBlockType, i haven't been able to figure out Crc32Utils.UnityCRC64
        var blockDir = Directory.EnumerateDirectories(streamingAssetsPath).First(x => x.Split('/', '\\').Last().StartsWith(Convert.ToHexString(Crc32.Hash(Encoding.UTF8.GetBytes(blockTypeMap[dumpAssetType])))));
        var blockFilePath = Path.Combine(blockDir, blockDir.Split('/', '\\').Last() + ".blc");

        var blockFile = File.ReadAllBytes(blockFilePath);
        byte[] nonce = GC.AllocateUninitializedArray<byte>(VFSDefine.BLOCK_HEAD_LEN);
        Buffer.BlockCopy(blockFile, 0, nonce, 0, nonce.Length);

        var chacha = new CSChaCha20(Convert.FromBase64String(VFSDefine.CHACHA_KEY), nonce, 1);
        var decryptedBytes = chacha.DecryptBytes(blockFile[VFSDefine.BLOCK_HEAD_LEN..]);
        Buffer.BlockCopy(decryptedBytes, 0, blockFile, VFSDefine.BLOCK_HEAD_LEN, decryptedBytes.Length);

        var vfBlockMainInfo = new VFBlockMainInfo(blockFile);
        foreach (var chunk in vfBlockMainInfo.allChunks)
        {
            var chunkMd5Name = Convert.ToHexString(BitConverter.GetBytes(chunk.md5Name)) + FVFBlockChunkInfo.FILE_EXTENSION;
            var chunkFs = File.OpenRead(Path.Join(blockDir, chunkMd5Name));
            foreach (var file in chunk.files)
            {
                var filePath = Path.Combine(outputDir, file.fileName);
                if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? throw new InvalidDataException($"Cannot get directory name of {filePath}"));

                if (file.bUseEncrypt)
                {
                    byte[] fileNonce = GC.AllocateUninitializedArray<byte>(VFSDefine.BLOCK_HEAD_LEN);
                    Buffer.BlockCopy(BitConverter.GetBytes(vfBlockMainInfo.version), 0, fileNonce, 0, sizeof(int));
                    Buffer.BlockCopy(BitConverter.GetBytes(file.ivSeed), 0, fileNonce, sizeof(int), sizeof(long));

                    var fileChacha = new CSChaCha20(Convert.FromBase64String(VFSDefine.CHACHA_KEY), fileNonce, 1);
                    var encryptedMs = new MemoryStream();
                    chunkFs.CopyBytes(encryptedMs, file.len);

                    // TODO: Should've used stream decryptor for better perf, but idk how to get it working
                    File.WriteAllBytes(filePath, fileChacha.DecryptBytes(encryptedMs.ToArray()));
                }
                else
                {
                    var fileFs = File.OpenWrite(filePath);
                    chunkFs.Seek(file.offset, SeekOrigin.Begin);
                    chunkFs.CopyBytes(fileFs, file.len);
                    fileFs.Dispose();
                }

            }

            Console.WriteLine("Dumped {0} file(s) from chunk {1}", chunk.files.Length, chunkMd5Name);
            chunkFs.Dispose();
        }
    }
}