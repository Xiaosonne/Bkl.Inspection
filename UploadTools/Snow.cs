namespace UploadTools
{
    public class Snow
    {
        public ushort WorkerId { get; set; } = 0;
        public byte WorkerIdBitLength { get; set; } = 6;
        public uint DataCenterId { get; set; } = 0;
        public byte DataCenterIdBitLength { get; set; } = 0;
        public byte SeqBitLength { get; set; } = 6;

    }
}