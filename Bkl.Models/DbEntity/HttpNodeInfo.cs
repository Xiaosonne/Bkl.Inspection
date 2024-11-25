namespace Bkl.Models
{
    public class HttpNodeInfo
    {
        public long Id { get; set; }
        public long DeviceId { get; set; }

        public string ProtocolName { get; set; }
        public ModbusDataType DataType { get; set; }

        public string RequestUrl { get; set; }
        public string RequestMethod { get; set; }
        public string HttpHeaders { get; set; }
        public string PostBody { get; set; }
        public string Scale { get; set; }
        public string StatusName { get; set; }
        public string StatusNameCN { get; set; }
        public string Unit { get; set; }
        public string UnitCN { get; set; }

    }
}
