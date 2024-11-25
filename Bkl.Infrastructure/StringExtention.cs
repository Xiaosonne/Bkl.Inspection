using System.Linq;
using System.Text;

namespace System
{
    public static class StringExtention
    {
        public static DateTime StrExtDetectFullPathToDateTime(this string str)
        {
            return DateTime.ParseExact(str.Split('/').Last().Split('.')[0], "yyyyMMddHHmmssfff", null);
        }
        public static string StrExtDetectFullPathDateFormat(this string str, string format)
        {
            return DateTime.ParseExact(str.Split('/').Last().Split('.')[0], "yyyyMMddHHmmssfff", null).ToString(format);
        }
        public static float[] StrExtCXCyToXY(this string str)
        {
            var arr = str.Split(',').Select(q => float.Parse(q)).ToArray();
            var x = arr[0] - arr[2] / 2;
            var y = arr[1] - arr[3] / 2;
            return new float[] { x, y, arr[2], arr[3] };
        }
    }
}
