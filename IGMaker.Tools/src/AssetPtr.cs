namespace IGMaker.Tools
{
    public struct AssetPtr
    {
        public ushort pakIdex;
        public BufferType bufferType;
        public AssetType assetType;
        public long pos;
        public int size;

        public AssetPtr(int pakIdex, int buffer, AssetType type, long pos, int size)
        {
            this.pakIdex = (ushort)pakIdex;
            this.bufferType = (BufferType)buffer;
            this.assetType = type;
            this.pos = pos;
            this.size = size;
        }
    }
}