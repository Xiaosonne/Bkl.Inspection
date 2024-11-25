﻿using System;
using System.Collections.Generic;
using System.Drawing;
using Bkl.Infrastructure;
namespace Bkl.Models
{
    public class FacilityGPS
    {
        public long id { get; set; }
        public string name { get; set; }
        public double[] gps { get; set; }
        public string defaultPic { get; set; }
    }
    public class HttpPushPollConfig
    {
        public string RemoteType { get; set; }

        public string Url { get; set; }
        //pull push
        public string Action { get; set; }
        public string Method { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public int DeviceId { get; set; }
        public int FacilityId { get; set; }
        public int FactoryId { get; set; }

    }
    public class BklConfig
    {
        public static BklConfig Instance = null;
        public class Snow
        {
            public ushort WorkerId { get; set; } = 0;
            public byte WorkerIdBitLength { get; set; } = 6;
            public uint DataCenterId { get; set; } = 0;
            public byte DataCenterIdBitLength { get; set; } = 0;
            public byte SeqBitLength { get; set; } = 6;

        }
        public class Database
        {
            public string host { get; set; }
            public string eusername { get; set; }
            public string epassword { get; set; }

            public string database { get; set; }

            public static string DB_AES_KEY = "1bkldatabasebkl2";
            private string _conStr = String.Empty;
            public string GetConnectionString()
            {
                return $"Server={host};SslMode=none;Uid={SecurityHelper.AESDecrypt(eusername, DB_AES_KEY)};Pwd={SecurityHelper.AESDecrypt(epassword, DB_AES_KEY)};Database={database};Convert Zero Datetime=True"; ;
            }

            public static string AESEN(string str)
            {
                return str.AESEncrypt(DB_AES_KEY);
            }
            public static Database GenInitConfig()
            {
                return new Database
                {
                    host = "localhost",
                    eusername = "test".AESEncrypt(DB_AES_KEY),
                    epassword = "bkl123...".AESEncrypt(DB_AES_KEY),
                    database = "bacara"
                };
            }
            public static Database GenTestInitConfig()
            {
                return new Database
                {
                    host = "localhost",
                    eusername = "root".AESEncrypt(DB_AES_KEY),
                    epassword = "123456".AESEncrypt(DB_AES_KEY),
                    database = "bacaratest"
                };
            }
        }
        public class Auth
        {
            public string Secret { get; set; }
            public string Issuer { get; set; }
            public string Audience { get; set; }
        }
         

        public class Redis
        {
            public string RedisHost { get; set; }
            public int RedisPort { get; set; }
            public string Auth { get; set; }
            public int DefaultDb { get; set; }

            public int SiloDb { get; set; }

            public string SiloClusterRedis { get; set; } = "127.0.0.1:6379,password=Etor0070x01";

            public string SiloReminderRedis { get; set; } = "127.0.0.1:6379,password=Etor0070x01";

            public string SiloStorageRedis { get; set; } = "127.0.0.1:6379,password=Etor0070x01";

            public string SiloStreamRedis { get; set; } = "127.0.0.1:6379,password=Etor0070x01";

            public string GetRedisUrl()
            {
                return $"{RedisHost}:{RedisPort},password={Auth}";
            }
        }
        public class Kafka
        {

            public string[] WebsocketConsumedTopic { get; set; }
            public string BootstrapServers { get; set; }
            public string ThermalStatusTopic { get; set; }
            public string BandageSensorStatusTopic { get; set; }
            public string DGAStatusTopic { get; set; }
            public string FDTStatusTopic { get; set; }
            public string DefaultGroupId { get; set; }

            public string AlarmLogTopic { get; set; }

            public string AlarmVideoRecordGroup { get; set; }

            public bool AutoOffsetCommit { get; set; }

            public Dictionary<string, string> PushStateOffset { get; set; }
            public string PTStatusTopic { get; set; }
        }

        public class Minio
        {
            public string EndPoint { get; set; }
            public string Key { get; set; }
            public string Secret { get; set; }
            public string Region { get; set; }
            public string PublicEndPoint { get; set; }
        }

        public Minio MinioConfig { get; set; }

        public Database DatabaseConfig { get; set; } 

        public Redis RedisConfig { get; set; }

        public Kafka KafkaConfig { get; set; }

        public Auth AuthConfig { get; set; } 

        public Snow SnowConfig { get; set; } 

        public string MySqlString { get => DatabaseConfig?.GetConnectionString(); } 

        public string Biz { get; set; }
        /// <summary>
        /// 临时文件目录
        /// </summary>
        public string FileBasePath { get; set; }

        public string MinioDataPath { get; set; }

        public string RtspServer { get; set; }

        public int ModbusSlaveServicePort { get; set; } = 8234;
        public string RtspDir { get; set; }

        public int ModbusStatusSaveInterval { get; set; } = 600;

        public int ThermalStatusSaveInterval { get; set; } = 600;



        public string DJIThermalTooPath { get; set; }
        public string DJIThermalTooExe { get; set; } = "dji_irp.exe";
        public string PowerDetectService { get; set; }
        public bool UseLocalFile { get;  set; }
        public bool ReportImageInMemory { get;  set; }
    }
    public class BklDGAConfig
    {
        public string CubicMeters { get; set; } = "20";

        /// <summary>
        /// 变压器油数据生产间隔 秒数
        /// </summary>
        public int GasProductionInterval { get; set; } = (int)TimeSpan.FromMinutes(1).TotalSeconds;

        /// <summary>
        ///线程休息间隔 秒数
        /// </summary>
        public int AbsoluteRateReminderInterval { get; set; } = (int)TimeSpan.FromMinutes(1).TotalSeconds;
        /// <summary>
        /// 变压器油计算气体产气率间隔秒数
        /// </summary>
        public int AbsoluteRateCalculateInterval { get; set; } = (int)TimeSpan.FromMinutes(1).TotalSeconds;


        /// <summary>
        ///线程休息间隔  秒数
        /// </summary>
        public int RelativeRateReminderInterval { get; set; } = (int)TimeSpan.FromMinutes(1).TotalSeconds;
        /// <summary>
        /// 变压器油计算气体产气率间隔 秒数
        /// </summary>
        public int RelativeRateCalculateInterval { get; set; } = (int)TimeSpan.FromMinutes(1).TotalSeconds;

    }
}
