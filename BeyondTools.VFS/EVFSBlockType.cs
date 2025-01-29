namespace BeyondTools.VFS
{
    public enum EVFSBlockType : byte
    {
        All,

        InitialAudio = 1,
        InitialBundle,
        BundleManifest,
        LowShader,
        Audio = 11,
        Bundle,
        TextAsset = 14,
        Video,
        IV,
        Streaming,
        IFixPatch = 21,
        Raw = 31
    }
}
