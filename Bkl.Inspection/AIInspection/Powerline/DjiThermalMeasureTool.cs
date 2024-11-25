using System.Diagnostics;
using System;
using System.IO;
using System.Linq;

namespace Bkl.Inspection
{
    public static class DjiThermalMeasureTool
    {
        public static float[][] ReadThermal(Stream data, int width, int height)
        {
            float[][] ret = new float[width][];
            using (BinaryReader br = new BinaryReader(data))
            {
                for (int i = 0; i < width; i++)
                {
                    ret[i] = new float[height];
                    for (int j = 0; j < height; j++)
                    {
                        var bts = br.ReadBytes(2);
                        var btsrev = bts.Reverse().ToArray();
                        ret[i][j] = BitConverter.ToInt16(bts, 0) / 10.0f;
                    }
                }
            }
            return ret;
        }
        public static float[][] ReadThermal(string data, int width, int height)
        {
            float[][] ret = new float[width][];
            using (BinaryReader br = new BinaryReader(new FileStream(data, FileMode.Open)))
            {
                for (int i = 0; i < width; i++)
                {
                    ret[i] = new float[height];
                    for (int j = 0; j < height; j++)
                    {
                        var bts = br.ReadBytes(2);
                        var btsrev = bts.Reverse().ToArray();
                        ret[i][j] = BitConverter.ToInt16(bts, 0) / 10.0f;
                    }
                }
            }
            return ret;
        }
        public static void ParseThermalRaw(string exe, string img, string outPut)
        {
            Process process = new Process();
            process.StartInfo.FileName = exe;
            process.StartInfo.Arguments = $" -s {img}  -a measure -o {outPut} --measurefmt int16";

            // 必须禁用操作系统外壳程序
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;


            //启动进程
            process.Start();

            //准备读出输出流和错误流
            string outputData = string.Empty;
            string errorData = string.Empty;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.OutputDataReceived += (sender, e) =>
            {
                outputData += (e.Data + "\n");
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                errorData += (e.Data + "\n");
            };
            //等待退出
            process.WaitForExit();

            //返回流结果

            Console.WriteLine("output:" + outputData);
            Console.WriteLine("error:" + errorData);
        }

    }
}
