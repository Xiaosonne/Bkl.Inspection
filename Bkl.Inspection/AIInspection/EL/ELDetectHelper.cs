using Bkl.Models;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System;

public class ELDetectHelper
{
    public static string DetectEndPoint = "192.168.31.108:7766"; 
    public static string ExtractEndPoint = "192.168.31.108:7767";

    static ELDetectHelper()
    {
        var d1 = Environment.GetEnvironmentVariable("BKL_EL_DETECT_ENDPOINT");
        var d2 = Environment.GetEnvironmentVariable("BKL_EL_EXTRACT_ENDPOINT");
        DetectEndPoint = string.IsNullOrEmpty(d1) ? "192.168.31.108:7766" : d1;
        ExtractEndPoint = string.IsNullOrEmpty(d1) ? "192.168.31.108:7767" : d2;
    }
    static HttpClient client1 = new HttpClient(new HttpClientHandler
    {
    })
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public  async Task<AlignmentResult> Extract(BklConfig config, string path)
    {
        try
        {
            var arr = path.Split('/');
            var bucket = arr[0];
            var name = arr[1];
            var resp = await client1.GetAsync($"http://{ExtractEndPoint}/alignment/{bucket}/{name}");
            var strRet = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                try
                {
                    var result = JsonSerializer.Deserialize<AlignmentResult>(strRet);
                    return result;
                }
                catch(Exception ex1)
                {
                    Console.WriteLine($"ErrorAlignmentDeserialize {path} {ExtractEndPoint} {ex1} ");
                }
                return new AlignmentResult { error = "ErrorAlignmentDeserialize" };
            }
            else
            {
                return new AlignmentResult { error= "ErrorAlignmentHttpProcess" };
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"ErrorAlignment {path} {ExtractEndPoint} {ex} ");
            return null;
        }
       
    }
    public  async Task<YoloResult[]> PathDetect(BklConfig config,   string path)
    {
        try
        {
            var resp = await client1.GetAsync($"http://{DetectEndPoint}/file-predict?w=400&h=300&path={path}");
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                return null;
            //var resp = await client.PostAsync("http://192.168.31.17:8972/yolo_predict", new StringContent("files=" + JsonSerializer.Serialize(new { image = str64 }),Encoding.UTF8, "application/x-www-form-urlencoded"));
            var strRet = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<YoloResult[]>(strRet);
            foreach (var item in result)
            {
                item.id = SnowId.NextId();
                item.xmax = item.xmax * 10;
                item.xmin = item.xmin * 10;
                item.ymax = item.ymax * 10;
                item.ymin = item.ymin * 10;
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine("error detect http request " + ex);
            return null;
        }
    }
}
