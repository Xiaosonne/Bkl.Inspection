using Bkl.Models.MongoEntity;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bkl.Models.RequestResponse
{
    public class CreateStatisticRequest
    {
        public class GetDeviceFilter
        {
            public long PlanId { get; set; }
            public int RunStatus { get; set; }
            public string PlanType { get; set; }
            public string PlanName { get; set; }
            public long FactoryId { get; set; }
            public string FactoryName { get; set; }
            public long FacilityId { get; set; }
            public string FacilityName { get; set; }
            public long DeviceId { get; set; }
            public string DeviceName { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public int _Page = 0;
            public int Page
            {
                get
                {
                    return _Page;
                }
                set
                {
                    _Page = value;
                }
            }
            public int _PageSize = 10;
            public int PageSize
            {
                get
                {
                    return _PageSize;
                }
                set
                {
                    _PageSize = value;
                }
            }

            public string AlgorithmName { set; get; }

        }


        public class DailyCountResult
        {

            [BsonId]
            public ObjectId _id { get; set; }
            public PlanResult planResult { get; set; }
            public FacilityResult facilityResult { get; set; }
            public DeviceResult deviceResult { get; set; }

            public Dictionary<string, List<AigoriRunTime>> _aigoriRunTimes = new Dictionary<string, List<AigoriRunTime>>();
            /// <summary>
            /// 算法运行总时长和最长时间
            /// </summary>
            public Dictionary<string, List<AigoriRunTime>> aigoriRunTimes
            {
                get
                {
                    return _aigoriRunTimes;
                }
                set
                {
                    _aigoriRunTimes = value;
                }
            }

            public GetDeviceFilter _BaseInformation = new GetDeviceFilter();
            public GetDeviceFilter BaseInformation
            {
                set
                {
                    _BaseInformation = value;
                }
                get
                {
                    return _BaseInformation;
                }
            }
        }

        public class DailyCountMongo
        {

        }

        public class PlanResult
        {
            /// <summary>
            /// 巡检任务次数
            /// </summary>
            public long RunTimes { get; set; }
            /// <summary>
            /// 巡检任务失败次数
            /// </summary>
            public long ErrorTimes { get; set; }
            /// <summary>
            /// 任务运行时间
            /// </summary>
            public string RunTime { get; set; }
            /// <summary>
            /// 任务运行进度（天、小时）
            /// </summary>
            public double PlanProcess { get; set; }
            /// <summary>
            /// 运行状态
            /// </summary>
            public int RunStatus { get; set; }
            /// <summary>
            /// 运行周期
            /// </summary>
            public double PlanInterval { get; set; }
            /// <summary>
            /// 任务完成度（实际运行次数/计算运行次数）单位时间
            /// </summary>
            public double Completeness { get; set; }
            public ErrorResultCount ErrorResultCount { get; set; }
            /// <summary>
            /// 总检验图片
            /// </summary>
            public int TotalPictureCount { get; set; }

        }
        public class FacilityResult
        {
            /// <summary>
            /// 总检测设备
            /// </summary>
            public int TotalDevice { get; set; }
            public List<ResultList> _ErrorDevice = new List<ResultList>();
            /// <summary>
            /// 异常设备
            /// </summary>
            public List<ResultList> ErrorDevice
            {
                get
                {
                    return _ErrorDevice;
                }
                set
                {
                    _ErrorDevice = value;
                }
            }
        }
        public class DeviceResult
        {
            /// <summary>
            /// 检测次数
            /// </summary>
            public int DetectTimes { get; set; }
            public ErrorResultCount ErrorResultCount { get; set; }
        }

        public class ErrorResultCount
        {
            public List<ResultList> _DefectLevel = new List<ResultList>();
            /// <summary>
            /// 缺陷类型(级别)统计
            /// </summary>
            public List<ResultList> DefectLevel
            {
                get
                {
                    return _DefectLevel;
                }
                set
                {
                    _DefectLevel = value;
                }
            }

            public List<ResultList> _AlgorithmName = new List<ResultList>();
            /// <summary>
            /// 缺陷类型(级别)统计
            /// </summary>
            public List<ResultList> AlgorithmName
            {
                get
                {
                    return _AlgorithmName;
                }
                set
                {
                    _AlgorithmName = value;
                }
            }

            /// <summary>
            /// 抓拍图片
            /// </summary>
            public int PictureCount { get; set; }
            /// <summary>
            /// 抓拍音频
            /// </summary>
            public int VideoCount { get; set; }

            public List<ResultList> _DetectClassName = new List<ResultList>();
            /// <summary>
            /// 检测分类统计
            /// </summary>
            public List<ResultList> DetectClassName
            {
                get
                {
                    return _DetectClassName;
                }
                set
                {
                    _DetectClassName = value;
                }
            }
        }

        public class ReturnDeviceModel
        {
            //public string PointName { get; set; }
            public string DeviceName { get; set; }
            public long Count { get; set; }
        }

        public class PlanByDevice
        {
            public long id { get; set; }
            public string DeviceName { get; set; }
            public DateTime BeginTime { get; set; }
            public DateTime EndTime { get; set; }
            public string ResultCount { get; set; }
            public List<string> PlanResult { get; set; }
        }

        public class ResultList
        {
            public string Name { set; get; }
            public int Count { set; get; }
        }

        public class FacilityModel
        {
            public string Name { set; get; }
            public long Id { set; get; }
        }
        public class AigoriRunTime
        {
            public string Name { set; get; }
            public long TotalTime { set; get; }
            public long MaxTime { set; get; }
        }
        public class FacilityMaxModel
        {
            public string Name { set; get; }
            public string Type { set; get; }
            public long Count { set; get; }
        }

        public class FacilityCountData
        {
            public long Id { set; get; }
            public string Name { set; get; }
            public Dictionary<string, long> _ErrorCount = new Dictionary<string, long>();
            public Dictionary<string, long> ErrorCount
            {
                set
                {
                    _ErrorCount = value;
                }
                get
                {
                    return _ErrorCount;
                }
            }
            public List<Dictionary<string, string>> _ErrorPointCount = new List<Dictionary<string, string>>();
            public List<Dictionary<string, string>> ErrorPointCount
            {
                set
                {
                    _ErrorPointCount = value;
                }
                get
                {
                    return _ErrorPointCount;
                }
            }
            public List<Dictionary<string, string>> _PointErrorCount = new List<Dictionary<string, string>>();
            public List<Dictionary<string, string>> PointErrorCount
            {
                set
                {
                    _PointErrorCount = value;
                }
                get
                {
                    return _PointErrorCount;
                }
            }

            public List<ReportData> _ErrorPointData = new List<ReportData>();
            public List<ReportData> ErrorPointData
            {
                set
                {
                    _ErrorPointData = value;
                }
                get { return _ErrorPointData; }
            }
            List<ReportData> _PointErrorData = new List<ReportData>();
            public List<ReportData> PointErrorData
            {
                set
                {
                    _PointErrorData = value;
                }
                get { return _PointErrorData; }
            }

            public Dictionary<string, Dictionary<string, List<Dictionary<string, string>>>> _TempData = new Dictionary<string, Dictionary<string, List<Dictionary<string, string>>>>();
            public Dictionary<string, Dictionary<string, List<Dictionary<string, string>>>> TempData
            {

                get
                {
                    return _TempData;
                }
                set
                {
                    _TempData = value;
                }
            }

            public List<Dictionary<string, string>> _BladData = new List<Dictionary<string, string>>();
            public List<Dictionary<string, string>> BladData
            {
                set
                {
                    _BladData = value;
                }
                get
                {
                    return _BladData;
                }
            }

            public List<AigoriRunTime> _AigoriRuns = new List<AigoriRunTime>();
            public List<AigoriRunTime> AigoriRuns
            {
                set { _AigoriRuns = value; }
                get { return _AigoriRuns; }

            }
            public Dictionary<string, Dictionary<string, string[]>> ErrorPointPicture { set; get; }
            public Dictionary<string, Dictionary<string, string[]>> PointErrorPicture { set; get; }
        }
        public class FacilityDatas
        {
            public long Id { set; get; }
            public string Name { set; get; }
            public Dictionary<string, long> ErrorCount { set; get; }
            public List<Dictionary<string, string>> ErrorPointCount { set; get; }
            public List<Dictionary<string, string>> PointErrorCount { set; get; }

            public List<ReportData> ErrorPointData { set; get; }
            public List<ReportData> PointErrorData { set; get; }
            public Dictionary<string, Dictionary<string, List<Dictionary<string, string>>>> TempData { get; set; }

            public List<Dictionary<string, string>> BladData { set; get; }

            public List<AigoriRunTime> AigoriRuns { set; get; }

            public Dictionary<string, Dictionary<string, string[]>> ErrorPointPicture { set; get; }
            public Dictionary<string, Dictionary<string, string[]>> PointErrorPicture { set; get; }
        }



    }
}
