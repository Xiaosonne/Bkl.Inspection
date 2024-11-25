using System;

namespace Bkl.Models
{
    public class CreateDeviceRequest
    {
        public string BrandName { get; set; }
        /// <summary>
        /// WindPowerGenerator HeatPowerGenerator Transformer
        /// </summary>
        public string FacilityType { get; set; }
        public string FacilityName { get; set; }
        public string Position { get; set; }
        public string ProbeName { get; set; }
        /// <summary>
        /// BandageSensor ThermalCamera DGA PTDetector
        /// </summary>
        public string DeviceType { get; set; }
        public string IPaddress { get; set; }

        public int Port { get; set; }
        public int StreamPort { get; set; } = 554;

        public string UserName { get; set; }
        public string Password { get; set; }

        public byte BusId { get; set; }
        public string TransferType { get; set; }
        public int NodeIndex { get; set; }
        public string ReadType { get; set; } 
        public string ProtocolName { get; set; }

        public long FactoryId { get; set; } 
    }
    public class NVRFileInfo
    {
        public string fileName { get; set; }
        public uint fileIndex { get; set; }
        public uint fileSize { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
    }
}
