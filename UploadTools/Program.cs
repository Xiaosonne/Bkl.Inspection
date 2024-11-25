using CommandLine;
using ImageMagick;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minio.DataModel;
using Org.BouncyCastle.Asn1.Pkcs;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using SixLabors.ImageSharp;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;

namespace UploadTools
{

    public class LabelStudioLabel
    {
        public Data data { get; set; }
        public Prediction[] predictions { get; set; }
    }

    public class Data
    {
        public string image { get; set; }
    }

    public class Prediction
    {
        public string model_version { get; set; }
        public float score { get; set; }
        public Result[] result { get; set; }
    }

    public class Result
    {
        public int original_width { get; set; }
        public int original_height { get; set; }
        public int image_rotation { get; set; }
        public XYRect value { get; set; }
        public string from_name { get; set; }
        public string to_name { get; set; }
        public string type { get; set; }
        public string origin { get; set; }
    }

    public class XYRect
    {
        public float x { get; set; }
        public float y { get; set; }
        public float width { get; set; }
        public float height { get; set; }
        public int rotation { get; set; }
        public string[] rectanglelabels { get; set; }
    }

    public class CommandLineOption
    {
        [Option('t')]
        public string type { get; set; }

    }

    internal class Program
    {
        static Dictionary<string, string> classCnMap = new Dictionary<string, string>
        {
            {"fc","蜂巢"},
{"nc","鸟巢"},
{"gt_yw","杆塔异物"},
{"gt_dyjj","杆塔多余金具"},
{"gt_qls","杆塔缺螺栓"},
{"gt_bgbhctl","杆塔抱箍保护层脱落"},
{"gt_ttsh","杆塔塔头损坏"},
{"gt_xs","杆塔锈蚀"},
{"gt_tcbx","杆塔塔材变形"},
{"fhjyz_dhss","复合绝缘子电弧烧伤"},
{"tcjyz_dhss","陶瓷绝缘子电弧烧伤"},
{"tcjyz_sqps","陶瓷绝缘子伞裙破损"},
{"fhjyz_sqps","复合绝缘子伞裙破损"},
{"fhjyz_sqbx","复合绝缘子伞裙变形"},
{"jyz_sqzb","绝缘子伞裙自爆"},
{"jyz_jjxs","绝缘子金具锈蚀"},
{"bljyz_sqzw","玻璃绝缘子伞裙脏污"},
{"tcjyz_sqzw","陶瓷绝缘子伞裙脏污"},
{"jyh_qx","均压环倾斜"},
{"jyh_qs","均压环缺失"},
{"jyh_sh","均压环损坏"},
{"jyh_zs","均压环灼伤"},
{"jyh_tl","均压环脱落"},
{"jyh_azbgf","均压环安装不规范"},
{"fhjyz_sqfh","复合绝缘子伞裙粉化"},
{"fdjx_cbtl","放电间隙磁棒脱落"},
{"fdjx_jxjx","放电间隙间隙较小"},
{"fdjx_jxjd","放电间隙间隙较大"},
{"xcxj_xs","悬垂线夹锈蚀"},
{"nzxj_xs","耐张线夹锈蚀"},
{"uxgh_xs","U型挂环锈蚀"},
{"zjgb_xs","直角挂板锈蚀"},
{"qtgh_xs","球头挂环锈蚀"},
{"wtgb_xs","碗头挂板锈蚀"},
{"lb_xs","连板锈蚀"},
{"uxls_xs","U型螺栓锈蚀"},
{"tzb_xs","调整板锈蚀"},
{"zc_xs","重锤锈蚀"},
{"fzc_xs","防振锤锈蚀"},
{"xjqx","线夹倾斜"},
{"fzc_hycp","防震锤滑移触碰"},
{"fzc_sh","防震锤损坏"},
{"fzc_px","防振锤偏斜"},
{"xzazbgf","销子安装不规范"},
{"xztc","销子脱出"},
{"lsqxz","螺栓缺销子"},
{"lsqlm","螺栓缺螺母"},
{"lsqbm","螺栓缺备母"},
{"lslmqk","螺栓螺母欠扣"},
{"jjqls","金具缺螺栓"},
{"xjjxs","小金具锈蚀"},
{"xcxj_lslybz","悬垂线夹露牙不足"},
{"xcxj_dpbp","悬垂线夹垫片不平"},
{"lsqdp","螺栓缺垫片"},
{"wtxztc","碗头销子脱出"},
{"ddx_sg","导地线散股"},
{"ddx_dg","导地线断股"},
{"ddx_ss","导地线损伤"},
{"ddx_yw","导地线异物"},
{"ddx_xs","导地线锈蚀"},
{"ddx_jc","导地线接触"},
{"yjs_sg","预绞丝散股"},
{"fnc_sh","防鸟刺损坏"},
{"fnc_wdk","防鸟刺未打开"},
{"qnq_sh","驱鸟器损坏"},
{"bsp_sh","标识牌损坏"},
{"bsp_twbq","标识牌图文不清"},
{"td_wjj","通道挖掘机"},
{"td_dc","通道吊车"},
{"td_ttc","通道推土车"},
{"tjzw","塔基杂物"},
{"tjbm","塔基被埋"},
{"tjjs","塔基积水"},
{"tjsh","塔基损坏"},
{"tjxx","塔基下陷"},
{"tjwl","塔基外漏"},
{"jdzzqx","接地装置缺陷"},
{"jd_yxxdk","接地引下线断开"},
        };
        public static void RepaireImage(string repiredDir, string sourceDir)
        {
            var files = Directory.GetFiles(repiredDir, "*_V.JPG", new EnumerationOptions { MaxRecursionDepth = 10, RecurseSubdirectories = true });
            var filesDst = Directory.GetFiles(sourceDir, "*_V.JPG", new EnumerationOptions { MaxRecursionDepth = 10, RecurseSubdirectories = true });
            files.AsParallel().ForAll(srcFile =>
            {
                var finfo = new FileInfo(srcFile);
                var dstFile = filesDst.FirstOrDefault(s => s.EndsWith(finfo.Name));
                var dfinfo = new FileInfo(dstFile);
                if (0 == finfo.Length)
                {
                    File.Copy(dstFile, srcFile, true);
                    Console.WriteLine(srcFile);
                }
            });

        }
        public static void CompressImages(string[] files)
        {

            int total = files.Length;
            int cur = 0;
            files.AsParallel().ForAll(file =>
            {
                var finfo = new FileInfo(file);
                if (finfo.Length > 2 * 1024 * 1024)
                {
                    using (Process process = new Process())
                    {
                        var proc1 = new ProcessStartInfo();
                        proc1.UseShellExecute = false;
                        proc1.WorkingDirectory = @"C:\Program Files\ImageMagick-7.1.1-Q16-HDRI";
                        proc1.FileName = @"C:\Program Files\ImageMagick-7.1.1-Q16-HDRI\magick.exe";
                        proc1.Verb = " convert ";

                        proc1.Arguments = $" -quality 75 {file} {file}";
                        proc1.WindowStyle = ProcessWindowStyle.Hidden;
                        Interlocked.Add(ref cur, 1);
                        var proc = Process.Start(proc1);
                        proc.WaitForExit();
                        Console.WriteLine($"Total {total} Now {cur} Percent {(cur * 1.0 / total)} ");
                    };
                }
               
            });
        }
        public static void CompressImage(string image_path)
        {
            var files = Directory.GetFiles(image_path, "*_V.JPG", new EnumerationOptions { MaxRecursionDepth = 10, RecurseSubdirectories = true });

            int total = files.Length;
            int cur = 0;
            int running = 0;
            files.AsParallel().ForAll(file =>
            {
                var finfo = new FileInfo(file);
                if(finfo.Length > 2 * 1024 * 1024)
                {
                    Interlocked.Add(ref running, 1);
                    using (Process process = new Process())
                    {
                        var proc1 = new ProcessStartInfo();
                        proc1.UseShellExecute = false;
                        proc1.WorkingDirectory = @"C:\Program Files\ImageMagick-7.1.1-Q16-HDRI";
                        proc1.FileName = @"C:\Program Files\ImageMagick-7.1.1-Q16-HDRI\magick.exe";
                        proc1.Verb = " convert ";

                        proc1.Arguments = $" -quality 75 {file} {file}";
                        proc1.WindowStyle = ProcessWindowStyle.Hidden;
                        Interlocked.Add(ref cur, 1);
                        var proc = Process.Start(proc1);
                        proc.WaitForExit();
                        Console.WriteLine($"Total {total} Now {cur} Percent {(cur * 1.0 / total)} {DateTime.Now} ");
                    };
                    Interlocked.Add(ref running, -1);
                }
            });
        }
        public static void Convert2LabelStudioAnnotation(string savedir, string sourcedir)
        {
            var label = JsonSerializer.Deserialize<List<XmlLabel>>(File.ReadAllText("data.json"));
            List<LabelStudioLabel> labels = new List<LabelStudioLabel>();
            object lockobn = new object();
            label.GroupBy(s => s.file).AsParallel().ForAll(items =>
            {
                var item = items.First();
                Prediction pre = new Prediction
                {
                    model_version = "userconvert",
                    score = 0.9f,
                    result = new Result[items.Count()]
                };
                LabelStudioLabel label1 = new LabelStudioLabel
                {
                    data = new Data
                    {
                        image = $"s3://yolo-power-dataset/images/{Path.GetFileNameWithoutExtension(item.file)}.JPG"
                    }
                    ,
                    predictions = new Prediction[]  {
                        pre
                    }
                };

                //var meta = Metafile.FromFile(Path.Combine(sourcedir, Path.GetFileNameWithoutExtension(item.file) + ".JPG"));
                var w = 4000;// meta.Width;
                var h = 3000;// meta.Height;
                var i = 0;
                foreach (var s in items)
                {
                    float x1 = 100.0f * (int.Parse(s.xmin)) / w;
                    float y1 = 100.0f * (int.Parse(s.ymin)) / h;
                    float w1 = 100.0f * (int.Parse(s.xmax) - int.Parse(s.xmin)) / w;
                    float h1 = 100.0f * (int.Parse(s.ymax) - int.Parse(s.ymin)) / h;
                    pre.result[i] = new Result
                    {
                        original_width = w,
                        original_height = h,
                        image_rotation = 0,
                        value = new XYRect
                        {
                            x = x1,
                            y = y1,
                            width = w1,
                            height = h1,
                            rotation = 0,
                            rectanglelabels = new string[] { s.name }
                        },
                        from_name = "label",
                        to_name = "image",
                        type = "rectanglelabels",
                        origin = "manual"
                    };
                    i++;
                }
                lock (lockobn)
                {
                    labels.Add(label1);
                }
            });

            File.WriteAllText(Path.Combine(savedir, "labelstudio.json"), JsonSerializer.Serialize(labels));
        }
        public static void Convert2YoloAnnotation(string file, string sourcedir, string jsondata = "data-updated-20240118.json")
        {
            var dir = Path.GetFileNameWithoutExtension(file);

            var label = JsonSerializer.Deserialize<List<XmlLabel>>(File.ReadAllText(jsondata));
            var lines = File.ReadAllLines(file);

            Directory.CreateDirectory($"d:/{dir}");
            Directory.CreateDirectory($"d:/{dir}/images");
            Directory.CreateDirectory($"d:/{dir}/labels");

            var txtdir = $"d:/{dir}/labels";
            var repodir = $"d:/{dir}";
            var imagesdir = $"d:/{dir}/images";

            var clsMap = lines.Select(t => t.Split('\t')).Where(s => s.Length == 5)
                .ToDictionary(s => s[0], s => s[3]);
            Dictionary<string, int> nameclassmap = new Dictionary<string, int>();
            Dictionary<string, int> nameclasscount = new Dictionary<string, int>();
            int classId = 0;
            object lockobn = new object();
            label.GroupBy(s => s.file).AsParallel().ForAll(items =>
            {
                var its = items.Where(q => clsMap.ContainsKey(q.name)).ToArray();
                if (its.Length == 0)
                {
                    Console.WriteLine(items.Key + "  not class");
                    return;
                }
                var item = its.First();

                //var meta = Metafile.FromFile(Path.Combine(sourcedir, Path.GetFileNameWithoutExtension(item.file) + ".JPG"));

                //var w = meta.Width;
                //var h = meta.Height;
                var w = 4000; //meta.Width;
                var h = 3000;// meta.Height;
                lock (lockobn)
                {
                    foreach (var s in its)
                    {
                        var yoloclass = clsMap[s.name];
                        if (!nameclassmap.ContainsKey(yoloclass))
                        {
                            nameclassmap.Add(yoloclass, classId++);
                        }
                    }
                }

                StringBuilder sb = new StringBuilder();
                int count = 0;
                foreach (var s in its)
                {
                    var yoloclass = clsMap[s.name];
                    var clsId = nameclassmap[yoloclass];
                    float cx = (int.Parse(s.xmin) + int.Parse(s.xmax)) / (2 * w * 1.0f);
                    float cy = (int.Parse(s.ymin) + int.Parse(s.ymax)) / (2 * h * 1.0f);
                    float w1 = (int.Parse(s.xmax) - int.Parse(s.xmin)) / (w * 1.0f);
                    float h1 = (int.Parse(s.ymax) - int.Parse(s.ymin)) / (h * 1.0f);
                    sb.AppendLine($"{clsId} {cx} {cy} {w1} {h1}");
                    count++;
                    lock (lockobn)
                    {
                        if (nameclasscount.ContainsKey(yoloclass))
                        {
                            nameclasscount[yoloclass]++;
                        }
                        else
                        {
                            nameclasscount.Add(yoloclass, 1);
                        }
                    }

                }
                if (count > 0)
                {
                    //if (!File.Exists(Path.Combine(imagesdir, Path.GetFileNameWithoutExtension(item.file) + ".JPG")))
                    //    File.Copy(Path.Combine(sourcedir, Path.GetFileNameWithoutExtension(item.file) + ".JPG"),
                    //        Path.Combine(imagesdir, Path.GetFileNameWithoutExtension(item.file) + ".JPG"));
                    if (!File.Exists(Path.Combine(txtdir, Path.GetFileNameWithoutExtension(item.file) + ".txt")))
                        File.WriteAllText(Path.Combine(txtdir, Path.GetFileNameWithoutExtension(item.file) + ".txt"), sb.ToString());
                }
            });
            nameclassmap = nameclassmap.OrderBy(s => s.Value).ToDictionary(s => s.Key, s => s.Value);
            File.WriteAllText(Path.Combine(repodir, "nameclasscount.txt"), JsonSerializer.Serialize(nameclasscount.ToArray()));
            File.WriteAllText(Path.Combine(repodir, "nameclassmap.txt"), JsonSerializer.Serialize(nameclassmap.ToArray()));
            foreach (var s in nameclasscount)
            {
                Console.WriteLine($"{s.Key}\t\t\t{s.Value}");
            }
            Console.WriteLine("=====");
            foreach (var s in nameclassmap)
            {
                Console.WriteLine($"{s.Key}\t\t\t{s.Value}");
            }

        }
        public static void ProcessXml(string dir)
        {
            var xmls = Directory.GetFiles(dir, "*.xml");
            List<XmlLabel> list = new List<XmlLabel>();
            //HashSet<string> set = new HashSet<string>();    
            Dictionary<string, int> set = new Dictionary<string, int>();
            foreach (var xml in xmls)
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(File.ReadAllText(xml));
                var objects = xmlDocument.SelectNodes("/annotation/object");
                foreach (XmlElement obj in objects)
                {
                    var name = obj.SelectSingleNode("name").InnerText;
                    var xmin = obj.SelectSingleNode("bndbox/xmin").InnerText;
                    var xmax = obj.SelectSingleNode("bndbox/xmax").InnerText;
                    var ymin = obj.SelectSingleNode("bndbox/ymin").InnerText;
                    var ymax = obj.SelectSingleNode("bndbox/ymax").InnerText;
                    if (set.ContainsKey(name))
                    {
                        set[name] += 1;
                    }
                    else
                    {
                        set.Add(name, 1);
                    }

                    list.Add(new XmlLabel
                    {
                        file = Path.GetFileName(xml),
                        name = name,
                        xmin = xmin,
                        xmax = xmax,
                        ymin = ymin,
                        ymax = ymax
                    });
                }
            }
            File.WriteAllText("data-updated.json", JsonSerializer.Serialize(list));

            var sb = set.OrderByDescending(p => p.Value).ToList().Aggregate(new StringBuilder(), (pre, cur) =>
              {
                  pre.AppendLine($"{cur.Key}\t{cur.Value}\t{classCnMap[cur.Key]}\t{cur.Key}\t{classCnMap[cur.Key]}");
                  return pre;
              });

            var sb2 = set.OrderByDescending(p => p.Key).ToList().Aggregate(new StringBuilder(), (pre, cur) =>
            {
                pre.AppendLine($"{cur.Key}\t{cur.Value}\t{classCnMap[cur.Key]}\t{cur.Key}\t{classCnMap[cur.Key]}");
                return pre;
            });
            File.WriteAllText("class_count.txt", sb.ToString());
            File.WriteAllText("class_names.txt", sb2.ToString());
            Console.WriteLine(sb.ToString());
            Console.WriteLine(sb2.ToString());
        }
        public static void ProcessAnnotation(string dir, string basedir = "F:\\无人机项目\\线路巡检其它", int off = 2)
        {

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            StreamReader sr = new StreamReader(File.OpenRead(System.IO.Path.Combine(dir, "image_list.txt")), Encoding.GetEncoding("GBK"));
            List<string> lis = new List<string>();
            while (!sr.EndOfStream)
            {
                lis.Add(sr.ReadLine());
            }
            //var filelen = File.ReadAllLines(System.IO.Path.Combine(dir, "image_list.txt"),Encoding.ASCII);
            var imgFiles = lis.Select(s => basedir == null ? s : $"{basedir}{s.Substring(off)}").ToArray();

            var annadir = Path.Combine(dir, "bak");
            var alldirs = Directory.GetDirectories(annadir);
            List<string> allxmlfiles = new List<string>();
            foreach (var dd in alldirs)
            {
                var ddd1 = Path.Combine(annadir, dd, "AllXyfDoit");
                var files = Directory.GetFiles(ddd1);
                allxmlfiles.AddRange(files);
            }
            var match = (from imgfile in imgFiles
                         join xmlfile in allxmlfiles on Path.GetFileName(imgfile).Split('.')[0] equals Path.GetFileName(xmlfile).Split('.')[0]
                         select new { imgfile, xmlfile }).ToArray();
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "dataset"));
            var datasetdir = Path.Combine(Directory.GetCurrentDirectory(), "dataset");
            foreach (var imgxml in match)
            {
                //var src = Path.Combine(Directory.GetCurrentDirectory(), "dataset", Path.GetFileName(imgxml.imgfile));
                var dst1 = Path.Combine(Directory.GetCurrentDirectory(), "dataset", Path.GetFileName(imgxml.imgfile));
                var dst2 = Path.Combine(Directory.GetCurrentDirectory(), "dataset", Path.GetFileName(imgxml.xmlfile));
                File.Copy(imgxml.imgfile, dst1, true);
                File.Copy(imgxml.xmlfile, dst2, true);
            }
        }
        private static void ProcessFanImages(string[] args)
        {
            var arg = CommandLine.Parser.Default.ParseArguments<CommandLineOption>(args);
            Console.WriteLine("Hello, World!");
            var host = new HostBuilder()
                  .ConfigureServices(services =>
                  {
                      services.AddSingleton<CommandLineOption>(arg.Value);
                      services.AddHostedService<HostService>();
                  })
                  .ConfigureAppConfiguration(app =>
                  {
                      app.AddJsonFile("appsettings.json", optional: true);
                      app.AddJsonFile("appsettings.Development.json", optional: true);
                  })
                  .Build();
            host.Run();
        }
        public static void CreateKmlFiles(string basedir)
        {
            // This will be used for the placemark


            // This is the root element of the file

             
            var dirs = Directory.GetDirectories(basedir);

            foreach (var dir in dirs)
            {
                Kml kml = new Kml();

                kml.AddNamespacePrefix(KmlNamespaces.GX22Prefix, KmlNamespaces.GX22Namespace);

                Folder folder = new Folder();
                folder.Name = Path.GetDirectoryName(basedir);
                kml.Feature = folder; 
                var dirinfo = new DirectoryInfo(dir);
                SharpKml.Dom.Point point = null;

                var files = Directory.GetFiles(Path.Combine(basedir, dir), "*.JPG");
                StringBuilder stringBuilder = new StringBuilder();
                foreach(var file in files ){
                    using var ms = File.Open(Path.Combine(basedir, dir, file), FileMode.Open);
                    using (var img = new MagickImage(ms))
                    {
                        var profile = img.GetExifProfile();

                        var lat = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLatitude);
                        var lon = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLongitude);
                        var alt = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSAltitude);
                        var tslat = lat.GetValue() as ImageMagick.Rational[];
                        var tslon = lon.GetValue() as ImageMagick.Rational[];
                        var dalt = ((ImageMagick.Rational)alt.GetValue()).ToDouble();
                        var dlat = tslat[0].ToDouble() + tslat[1].ToDouble() / 60 + tslat[2].ToDouble() / 3600;
                        var dlon = tslon[0].ToDouble() + tslon[1].ToDouble() / 60 + tslon[2].ToDouble() / 3600;
                    
                        point = new SharpKml.Dom.Point
                        {
                            Coordinate = new Vector(dlat, dlon)
                        };
                        Console.WriteLine(dir + " " + dlon+" "+dlat);

                        stringBuilder.AppendLine($"{dlat}\t{dlon}\t{dalt}");
                    } 

                }  
               
                using (var stream = System.IO.File.OpenWrite($"out{dirinfo.Name}.txt"))
                {
                    stream.Write(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
                }
            } 
          
            Console.WriteLine("ok");
        }
        public static void CreateKml()
        {
            // This will be used for the placemark


            // This is the root element of the file


            var basedir = Directory.GetCurrentDirectory();
            var dirs = Directory.GetDirectories(basedir);
            Kml kml = new Kml();

            kml.AddNamespacePrefix(KmlNamespaces.GX22Prefix, KmlNamespaces.GX22Namespace);

            Folder folder = new Folder();
            folder.Name = Path.GetDirectoryName(basedir);
            kml.Feature = folder;
            foreach (var dir in dirs)
            {
                SharpKml.Dom.Point point = null;

                var file = Directory.GetFiles(Path.Combine(basedir, dir), "*.JPG").FirstOrDefault();


                using var ms = File.Open(Path.Combine(basedir, dir, file), FileMode.Open);
                using (var img = new MagickImage(ms))
                {
                    var profile = img.GetExifProfile();

                    var lat = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLatitude);
                    var lon = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLongitude);
                    var alt = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSAltitude);
                    var tslat = lat.GetValue() as ImageMagick.Rational[];
                    var tslon = lon.GetValue() as ImageMagick.Rational[];
                    var dalt = ((ImageMagick.Rational)alt.GetValue()).ToDouble();
                    var dlat = tslat[0].ToDouble() + tslat[1].ToDouble() / 60 + tslat[2].ToDouble() / 3600;
                    var dlon = tslon[0].ToDouble() + tslon[1].ToDouble() / 60 + tslon[2].ToDouble() / 3600;
                   
                    point = new SharpKml.Dom.Point
                    {
                        Coordinate = new Vector(dlat, dlon)
                    };
                    Console.WriteLine(dir + " " + dlon+" "+dlat);
                }
             
                var placemark = new Placemark
                {
                    Name = new DirectoryInfo(dir).Name,
                    Geometry = point
                };

                folder.AddFeature(placemark);
           

            }

            KmlFile kmlfile = KmlFile.Create(kml, false);
            using (var stream = System.IO.File.OpenWrite("out.kml"))
            {
                kmlfile.Save(stream);
            }
            Console.WriteLine("ok");
        }
        static void Main(string[] args)
        {


            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平二回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平二回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平三回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平三回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平一回\\东平一回1-21");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平一回\\东平一回1-21");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平一回\\东平一回22-50");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\东平历史数据\\东平巡检照片-可见光\\东平一回\\东平一回22-50");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\平阴照片\\二三回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\平阴照片\\二三回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\平阴照片\\六回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\平阴照片\\六回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\平阴照片\\四回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\平阴照片\\四回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\平阴照片\\一回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\平阴照片\\一回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\孝里一二回巡检照片");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\孝里一二回巡检照片");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\邹城巡检图片-可见光\\邹城二回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\邹城巡检图片-可见光\\邹城二回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\邹城巡检图片-可见光\\邹城三回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\邹城巡检图片-可见光\\邹城三回");
            //ProcessAnnotation("F:\\无人机项目\\线路巡检其它\\邹城巡检图片-可见光\\邹城一回");
            //Console.WriteLine("F:\\无人机项目\\线路巡检其它\\邹城巡检图片-可见光\\邹城一回");

            //ProcessAnnotation("F:\\无人机项目\\烟台无人机风电线路巡检项目\\文登风电场", "f", 1);
            //ProcessAnnotation("F:\\无人机项目\\烟台无人机风电线路巡检项目\\郓城玉皇风电场\\精细化照片", "f", 1);

            //F:\无人机项目\线路巡检其它\东平历史数据\东平巡检照片-可见光\东平二回
            //F:\无人机项目\线路巡检其它\东平历史数据\东平巡检照片-可见光\东平三回
            //F:\无人机项目\线路巡检其它\东平历史数据\东平巡检照片-可见光\东平一回\东平一回1-21
            //F:\无人机项目\线路巡检其它\东平历史数据\东平巡检照片-可见光\东平一回\东平一回22-50
            //F:\无人机项目\线路巡检其它\平阴照片\二三回
            //F:\无人机项目\线路巡检其它\平阴照片\六回
            //F:\无人机项目\线路巡检其它\平阴照片\四回
            //F:\无人机项目\线路巡检其它\平阴照片\一回
            //F:\无人机项目\线路巡检其它\孝里一二回巡检照片
            //F:\无人机项目\线路巡检其它\邹城巡检图片-可见光\邹城二回
            //F:\无人机项目\线路巡检其它\邹城巡检图片-可见光\邹城三回
            //F:\无人机项目\线路巡检其它\邹城巡检图片-可见光\邹城一回 

            //ProcessXml("D:\\powerlinedataset-xml");

            //Convert2YoloAnnotation("classmap.json", "D:\\power-dataset\\yolo-power-dataset\\images");
            //Convert2YoloAnnotation("power-small-class.txt", "D:\\power-dataset\\yolo-power-dataset\\images");
            //Convert2YoloAnnotation("power-top-8.txt", "D:\\power-dataset\\yolo-power-dataset\\images");
            //Convert2YoloAnnotation("power-top-13.txt", "D:\\power-dataset\\yolo-power-dataset\\images");
            //ProcessFanImages(args);


            //Convert2LabelStudioAnnotation("d:", "D:\\power-dataset\\yolo-power-dataset\\images");

            //CompressImage("E:\\data\\power-wendeng\\精细化照片");

            //CompressImages(files);
            //RepaireImage("E:\\data\\power-wendeng\\精细化照片", "G:\\2 文登大唐风电场\\精细化照片");
            //CompressImage("E:\\data\\power-yidao");
            //CompressImage("E:\\data\\power-wendeng");
            //CompressImage("E:\\data\\power-xujiadian\\精细化照片"); 
            //Convert2YoloAnnotation("power-top-15.txt", "D:\\power-dataset\\yolo-power-dataset\\images");
            //CreateKml();
            ProcessFanImages(args);
            //CreateKmlFiles(args[0]);
        }
    }
}