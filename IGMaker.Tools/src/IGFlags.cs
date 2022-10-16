namespace IGMaker.Tools
{
    [System.Flags]
    public enum IGFlags : ushort
    {
        None = 0x0,

        File = 0x1,
        Stream = 0x2,

        GroupByType = 0x4,

        AllTypes = File | Stream
    }
}