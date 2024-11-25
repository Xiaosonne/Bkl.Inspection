using Bkl.Models;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;


public class StitchLeftData
{
    public int x1 { get; set; }
    public int y1 { get; set; }
    public int w1 { get; set; }
    public int h1 { get; set; }
    public int[][] kp1 { get; set; }
}
public class StitchRightData
{
    public int x2 { get; set; }
    public int y2 { get; set; }
    public int w2 { get; set; }
    public int h2 { get; set; }
    public int[][] kp2 { get; set; }
}

public class StitchToFuse
{
    public string path { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public int w { get; set; }
    public int h { get; set; }
    public int[][] kp { get; set; }
}
public class FuseRequest
{
    public string Rotate90 { get; set; }
    public string Name { get; set; }
    public StitchToFuse[] Pics { get; set; }
}
public class TaskProgressItem
{
    public int total { get; set; }
    public int proceed { get; set; }
    public string percent { get; set; }

    public string info { get; set; }

    public object data { get; set; }
    public TaskProgressItem(DetectTaskInfo detect)
    {

        total = detect.Total; proceed = detect.Procced;
        if (detect.Total != 0)
            percent = (detect.Procced * 100.0 / detect.Total).ToString("F2");
        else
            percent = "0";
    }
    public TaskProgressItem(SegTaskInfo detect)
    {
        total = detect.Total; proceed = detect.Procced;

        if (detect.Total != 0)
            percent = (detect.Procced * 100.0 / detect.Total).ToString("F2");
        else
            percent = "0";

    }
    public TaskProgressItem(List<FuseImagesResponse> detect)
    {
        total = 12; proceed = detect.Count; percent = (detect.Count * 100.0 / 12).ToString("F2");

    }
}
public class FuseImagesResponse
{
    public string save_path { get; set; }
    public string state { get; set; }
    public StitchToFuse[] images { get; set; }
}
public static class DetectHelper
{
    public static string DetectEndPoint = "192.168.31.113:8972";
    public static string FuseEndPoint = "192.168.31.108:8191";
    public static string SegEndPoint = "192.168.31.108:8190";

    static DetectHelper()
    {
        var d1 = Environment.GetEnvironmentVariable("BKL_DETECT_ENDPOINT");
        var d2 = Environment.GetEnvironmentVariable("BKL_FUSE_ENDPOINT");
        var d3 = Environment.GetEnvironmentVariable("BKL_SEG_ENDPOINT");
        DetectEndPoint = string.IsNullOrEmpty(d1) ? "192.168.31.108:9900" : d1;
        FuseEndPoint = string.IsNullOrEmpty(d2) ? "192.168.31.108:8191" : d2;
        SegEndPoint = string.IsNullOrEmpty(d3) ? "192.168.31.108:8190" : d3;
    }

    static HttpClient client1 = new HttpClient(new HttpClientHandler
    {
    })
    {
        Timeout = TimeSpan.FromMinutes(30)
    };
    public static async Task<YoloResult[]> PathDetect(BklConfig config, long taskId, long facilityId, string path)
    {
        try
        {
            var resp = await client1.GetAsync($"http://{DetectEndPoint}/file-predict?w=400&h=300&path={path}");
            Console.WriteLine(DetectEndPoint + " " + path);
            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
                return null;
            //var resp = await client.PostAsync("http://192.168.31.17:8972/yolo_predict", new StringContent("files=" + JsonSerializer.Serialize(new { image = str64 }),Encoding.UTF8, "application/x-www-form-urlencoded"));
            var strRet = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<YoloResult[]>(strRet);
            foreach (var item in result)
            {
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


    //public static string FuseEndPoint = "fuse.bacaraenergy.com:7001";
    //public static string SegEndPoint = "seg.bacaraenergy.com:7001";


    public static async Task<string> Fuse(BklConfig config, long taskId, long facilityId, FuseRequest fuse)
    {
        HttpContent content = new StringContent(JsonSerializer.Serialize(fuse), Encoding.UTF8, "application/json");
        var resp = await client1.PostAsync($"http://{FuseEndPoint}/file-fuse", content);
        if (resp.StatusCode == System.Net.HttpStatusCode.OK)
            return null;
        return await resp.Content.ReadAsStringAsync();
    }
    public static async Task<StitchToFuse[]> Stitch(BklConfig config, long taskId, long facilityId, string path1, string path2, string rotate90)
    {
        var resp = await client1.GetAsync($"http://{FuseEndPoint}/file-stitch?path1={path1}&path2={path2}&rotate90={rotate90}");
        if (resp.StatusCode != System.Net.HttpStatusCode.OK)
            return null;
        var content = await resp.Content.ReadAsStringAsync();
        var left = JsonSerializer.Deserialize<StitchLeftData>(content);
        var right = JsonSerializer.Deserialize<StitchRightData>(content);
        return new StitchToFuse[]{
                new StitchToFuse{w=left.w1,h=left.h1,x=left.x1,y=left.y1,kp=left.kp1,path=path1},
                new StitchToFuse{w=right.w2,h=right.h2,x=right.x2,y=right.y2,kp=right.kp2,path=path2},
            };
    }
    public static async Task<int[][]> Seg(BklConfig config, long taskId, long facilityId, string path)
    {
        var resp = await client1.GetAsync($"http://{SegEndPoint}/file-seg?image_path={path}");
        var content = await resp.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<int[][][][]>(content);
        var dataArr = data[0].Select(da => da[0]).ToArray();
        return dataArr;
    }
    static Dictionary<string, string> shortMap = new Dictionary<string, string>
    {
            {"叶片A/前缘/迎风面","aqy.jpg"},
            {"叶片A/后缘/迎风面","ahy.jpg"},
            {"叶片A/前缘/背风面","aqb.jpg"},
            {"叶片A/后缘/背风面","ahb.jpg"},
            {"叶片B/前缘/迎风面","bqy.jpg"},
            {"叶片B/后缘/迎风面","bhy.jpg"},
            {"叶片B/前缘/背风面","bqb.jpg"},
            {"叶片B/后缘/背风面","bhb.jpg"},
            {"叶片C/前缘/迎风面","cqy.jpg"},
            {"叶片C/后缘/迎风面","chy.jpg"},
            {"叶片C/前缘/背风面","cqb.jpg"},
            {"叶片C/后缘/背风面","chb.jpg"},
    };
    public static async Task<FuseImagesResponse> FuseImages(long taskId, long facilityId, string genname, string[] pics)
    {
        var resp = await client1.PostAsync($"http://{FuseEndPoint}/file-stitch-imgs", new StringContent(JsonSerializer.Serialize(new
        {
            rotate90 = "0",
            name = $"task_{taskId}/" + genname,
            pics = pics
        }), Encoding.UTF8, "application/json"));


        var json = await resp.Content.ReadAsStringAsync();
        try
        {
            var respJson = JsonSerializer.Deserialize<FuseImagesResponse>(json);

            return respJson;
        }
        catch (Exception ex)
        {
            Console.WriteLine(json);
            Console.WriteLine(ex.ToString());
            return new FuseImagesResponse() { state = ex.ToString() };
        }
    }
    public static async Task<FuseImagesResponse> FuseImages(long taskId, long facilityId, string[] pics, string yp)
    {
        var resp = await client1.PostAsync($"http://{FuseEndPoint}/file-stitch-imgs", new StringContent(JsonSerializer.Serialize(new
        {
            rotate90 = "0",
            name = $"task_{taskId}/{taskId}_{facilityId}_{shortMap[yp]}",
            pics = pics
        }), Encoding.UTF8, "application/json"));


        var str = await resp.Content.ReadAsStringAsync();
        try
        {
            return JsonSerializer.Deserialize<FuseImagesResponse>(str);
        }
        catch (Exception ex)
        {
            return new FuseImagesResponse
            {
                state = str + " decode error:" + ex.ToString(),
            };
        }
    }
    //public static async Task<YoloResult[]> Detect(BklConfig config, long taskId, long facilityId, string path)
    //{
    //    try
    //    {
    //        using (MemoryStream ms = new MemoryStream())
    //        {
    //            var mat = Cv2.ImRead(Path.Combine(config.MinioDataPath, path));

    //            using (FileStream fs = new FileStream(Path.Combine(config.MinioDataPath, path), FileMode.Open))
    //            {
    //                byte[] buf = new byte[1024 * 1024];
    //                int byteread = fs.Read(buf, 0, buf.Length);
    //                while (byteread > 0)
    //                {
    //                    ms.Write(buf, 0, byteread);
    //                    byteread = fs.Read(buf, 0, buf.Length);
    //                }
    //            }
    //            //var str64 = Convert.ToBase64String(ms.ToArray());
    //            var client = new HttpClient(new HttpClientHandler { });
    //            var content = new MultipartFormDataContent();
    //            content.Add(new ByteArrayContent(ms.ToArray()), "image", "image");
    //            var resp = await client.PostAsync("http://192.168.31.17:8972/yolo_predict", content);
    //            if (resp.StatusCode != System.Net.HttpStatusCode.OK)
    //                return null;
    //            //var resp = await client.PostAsync("http://192.168.31.17:8972/yolo_predict", new StringContent("files=" + JsonSerializer.Serialize(new { image = str64 }),Encoding.UTF8, "application/x-www-form-urlencoded"));
    //            var strRet = await resp.Content.ReadAsStringAsync();
    //            var result = JsonSerializer.Deserialize<YoloResult[]>(strRet);

    //            return result;
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine("error detect http request " + ex);
    //        return null;
    //    }
    //}
}

