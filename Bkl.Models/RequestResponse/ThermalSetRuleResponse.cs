using Bkl.Infrastructure;
using System.Collections.Generic;

namespace Bkl.Models
{
    public class GeneralResponse
    {
        public string msg { get; set; }
        public int error { get; set; }
        public bool success { get; set; }
        public GeneralResponse()
        {
            success = true;
            error = 0;
            msg = "";
        }
    }
    public class DataResponse<T> : GeneralResponse
    {
        public T data { get; set; }
        public DataResponse() { }
        public DataResponse(T data)
        {
            this.data = data;
        }

    }

    public class PagedDataResponse<T> : DataResponse<T>
    {
        public int page { get; set; }
        public int pageSize { get; set; }
        /// <summary>
        /// 当前页查出来数量
        /// </summary>
        public int pageCount { get; set; }
        public int totalPage { get; set; }
        public int totalCount { get; set; }
    }
    public class ThermalSetRuleResponse : GeneralResponse
    {
        public string outStatus { get; set; }
        public int ruleId { get; set; }
        public List<ThermalMeasureRule> rules { get; set; }
    }
}
