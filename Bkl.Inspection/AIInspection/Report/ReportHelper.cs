using Bkl.Infrastructure;
using Bkl.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
public static class ReportHelper
{
    static BladeFacilityNameCompare compare = new BladeFacilityNameCompare();

    public static async Task<ReportResult> GenerateELReport(BklConfig config, IRedisClient redis,BklInspectionTask task, List<BklInspectionTaskDetail> taskDetails,
         List<BklInspectionTaskResult> taskResults)
    {
        MemoryStream ms = new MemoryStream();
        List<ICreateParagraph> lis = new List<ICreateParagraph>();
        var create = new CreateWord();
        var result = new ReportResult
        {
            Status = "init",
            FileName = $"缺陷报告.docx",
            StartTime = DateTime.Now,
        };
        try
        {
            using (WordprocessingDocument word = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                int i = 0;
                //lis.Add(new CreateTextParagraph(
                //         $"表O-{i.ToString().PadLeft(2, '0')}  风机叶片外观检查记录",
                //         fontSize: "24",
                //         values: JustificationValues.Center,
                //         font: new RunFonts()
                //         {
                //             Hint = FontTypeHintValues.EastAsia,
                //             Ascii = "Times New Roman",
                //             HighAnsi = "Times New Roman",
                //             EastAsia = "宋体"
                //         },
                //         lines: new SpacingBetweenLines { Before = "240", After = "120" },
                //         outlineLevel: 2
                // ));
                lis.Add(new CreateBookmarkEnd());
                lis.Add(new CreateELExportParagraph(config, word, redis, task, taskDetails, taskResults));

                var maindoc = word.AddMainDocumentPart();
                Document document1 = new Document()
                {
                    MCAttributes = new MarkupCompatibilityAttributes()
                    {
                        Ignorable = "w14 w15 wp14"
                    }
                };
                document1.AddNamespaceDeclaration("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas");
                document1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
                document1.AddNamespaceDeclaration("o", "urn:schemas-microsoft-com:office:office");
                document1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                document1.AddNamespaceDeclaration("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");
                document1.AddNamespaceDeclaration("v", "urn:schemas-microsoft-com:vml");
                document1.AddNamespaceDeclaration("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing");
                document1.AddNamespaceDeclaration("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing");
                document1.AddNamespaceDeclaration("w10", "urn:schemas-microsoft-com:office:word");
                document1.AddNamespaceDeclaration("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                document1.AddNamespaceDeclaration("w14", "http://schemas.microsoft.com/office/word/2010/wordml");
                document1.AddNamespaceDeclaration("w15", "http://schemas.microsoft.com/office/word/2012/wordml");
                document1.AddNamespaceDeclaration("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup");
                document1.AddNamespaceDeclaration("wpi", "http://schemas.microsoft.com/office/word/2010/wordprocessingInk");
                document1.AddNamespaceDeclaration("wne", "http://schemas.microsoft.com/office/word/2006/wordml");
                document1.AddNamespaceDeclaration("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape");

                Body body1 = new Body();
                int j = 0;
                foreach (var paragraph1 in lis)
                {
                    body1.Append(paragraph1.AddParagraph());
                    Console.WriteLine($"WordCreatePara total:{lis.Count} cur:{j}");
                    j++;
                    i++;
                }

                SectionProperties sectionProperties1 = new SectionProperties() { RsidRPr = "00EC0123", RsidR = "00EC0123", RsidSect = "0009565B" };
                PageSize pageSize1 = new PageSize() { Width = (UInt32Value)11906U, Height = (UInt32Value)16838U };
                PageMargin pageMargin1 = new PageMargin() { Top = 1701, Right = (UInt32Value)1701U, Bottom = 1701, Left = (UInt32Value)1701U, Header = (UInt32Value)851U, Footer = (UInt32Value)992U, Gutter = (UInt32Value)0U };
                Columns columns1 = new Columns() { Space = "425" };
                DocGrid docGrid1 = new DocGrid() { Type = DocGridValues.Lines, LinePitch = 312 };

                sectionProperties1.Append(pageSize1);
                sectionProperties1.Append(pageMargin1);
                sectionProperties1.Append(columns1);
                sectionProperties1.Append(docGrid1);
                body1.Append(sectionProperties1);
                document1.Append(body1);
                AddSettingsToMainDocumentPart(maindoc);
                maindoc.Document = document1;
                word.Save();
            }
        }
        catch (Exception ex)
        {
            result.Status = "error";
            //result.SetValue(redis);
            Console.WriteLine(ex.ToString());
        }

        ms.Seek(0, SeekOrigin.Begin);
        var fileLocation = $"{DateTime.Now.ToString("yyyyMMddHHmmss")}-{Guid.NewGuid().ToString("N")}.docx";
        try
        {
            var pt = System.IO.Path.Combine(config.MinioDataPath, "GenerateReports");
            if (!Directory.Exists(pt))
            {
                Directory.CreateDirectory(pt);
            }
            var filename = System.IO.Path.Combine(pt, fileLocation);
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                await ms.CopyToAsync(fs, 1024 * 1024 * 10);
            }
            result.Status = "done";
            result.Location = $"GenerateReports/{fileLocation}";
            //result.SetValue(redis);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        foreach(var item in lis)
        {
            try
            {
                item.Done();
            }
            catch
            {

            }
        }
        return result;
    }

    private static void AddSettingsToMainDocumentPart(MainDocumentPart part)
    {
        DocumentSettingsPart settingsPart = part.DocumentSettingsPart;
        if (settingsPart == null)
            settingsPart = part.AddNewPart<DocumentSettingsPart>();
        settingsPart.Settings = new Settings(
            new Compatibility(
                new CompatibilitySetting()
                {
                    Name = new EnumValue<CompatSettingNameValues>
                        (CompatSettingNameValues.CompatibilityMode),
                    Val = new StringValue("14"),
                    Uri = new StringValue("http://schemas.microsoft.com/office/word")
                }
            )
        );
        settingsPart.Settings.Save();
    }

    //public static async Task<ReportResult> GeneratorAllReport(BklConfig config, IRedisClient redis, GenerateAllTaskRequest req)
    //{
    //    long taskId = req.taskId;
    //    long factoryId = req.factoryId;
    //    string mode = req.mode;
    //    BklFactory factory = req.factory;
    //    BklInspectionTask task = req.task;
    //    List<BklFactoryFacility> facilities = req.facilities;
    //    List<BklInspectionTaskDetail> taskDetails = req.taskDetails;
    //    List<BklInspectionTaskResult> taskResults = req.taskResults;
    //    MemoryStream ms = new MemoryStream();
    //    List<ICreateParagraph> lis = new List<ICreateParagraph>();
    //    var create = new CreateWord();
    //    var result = new ReportResult
    //    {
    //        SeqId = req.SeqId,
    //        TaskId = req.taskId,
    //        FactoryId = factory.Id,
    //        FacilityCount = facilities.Count,
    //        Status = "init",
    //        FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{task.Id}-{facilities.Count}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx",
    //        StartTime = DateTime.Now,
    //    };
    //    if (facilities.Count == 1)
    //    {
    //        result.FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{facilities[0].Name}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
    //    }
    //    else
    //    {
    //        if (facilities.Count < 10)
    //            result.FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{string.Join("-", facilities.Select(s => s.Name))}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
    //        else
    //            result.FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{facilities.Count}台风机-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
    //    }
    //    result.SetValue(redis);
    //    try
    //    {
    //        using (WordprocessingDocument word = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
    //        {
    //            int i = 0;
    //            foreach (var faci in facilities.OrderBy(s => s.Name, compare))
    //            {
    //                i++;
    //                var taskDetailsTemp = taskDetails
    //                    .Where(s => s.FactoryId == factoryId && s.FacilityId == faci.Id)
    //                    .ToList();
    //                var taskResultsTemp = taskResults
    //                    .Where(p => p.FactoryId == factoryId && p.FacilityId == faci.Id && p.TaskId == taskId)
    //                    .ToList();

    //                var vals = redis.GetValuesFromHash($"FacilityMeta:{faci.Id}");

    //                lis.Add(new CreateTextParagraph(
    //                        $"表O-{i.ToString().PadLeft(2, '0')}  {faci.Name.ToUpper()}风机叶片外观检查记录",
    //                        fontSize: "24",
    //                        values: JustificationValues.Center,
    //                        font: new RunFonts()
    //                        {
    //                            Hint = FontTypeHintValues.EastAsia,
    //                            Ascii = "Times New Roman",
    //                            HighAnsi = "Times New Roman",
    //                            EastAsia = "宋体"
    //                        },
    //                        lines: new SpacingBetweenLines { Before = "240", After = "120" },
    //                        outlineLevel: 2
    //                    )
    //                );
    //                lis.Add(new CreateBookmarkEnd());
    //                lis.Add(new CreateFJExportParagraph(config, word, redis, taskDetailsTemp, taskResultsTemp, factory, faci, mode));
    //                result.Status = $"processing {i}/{facilities.Count + facilities.Count * 3}";
    //                result.SetValue(redis);
    //            }
    //            var maindoc = word.AddMainDocumentPart();
    //            Document document1 = new Document()
    //            {
    //                MCAttributes = new MarkupCompatibilityAttributes()
    //                {
    //                    Ignorable = "w14 w15 wp14"
    //                }
    //            };
    //            document1.AddNamespaceDeclaration("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas");
    //            document1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
    //            document1.AddNamespaceDeclaration("o", "urn:schemas-microsoft-com:office:office");
    //            document1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
    //            document1.AddNamespaceDeclaration("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");
    //            document1.AddNamespaceDeclaration("v", "urn:schemas-microsoft-com:vml");
    //            document1.AddNamespaceDeclaration("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing");
    //            document1.AddNamespaceDeclaration("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing");
    //            document1.AddNamespaceDeclaration("w10", "urn:schemas-microsoft-com:office:word");
    //            document1.AddNamespaceDeclaration("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
    //            document1.AddNamespaceDeclaration("w14", "http://schemas.microsoft.com/office/word/2010/wordml");
    //            document1.AddNamespaceDeclaration("w15", "http://schemas.microsoft.com/office/word/2012/wordml");
    //            document1.AddNamespaceDeclaration("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup");
    //            document1.AddNamespaceDeclaration("wpi", "http://schemas.microsoft.com/office/word/2010/wordprocessingInk");
    //            document1.AddNamespaceDeclaration("wne", "http://schemas.microsoft.com/office/word/2006/wordml");
    //            document1.AddNamespaceDeclaration("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape");

    //            Body body1 = new Body();
    //            int j = 0;
    //            foreach (var paragraph1 in lis)
    //            {
    //                body1.Append(paragraph1.AddParagraph());
    //                Console.WriteLine($"WordCreatePara total:{lis.Count} cur:{j}");
    //                j++;
    //                i++;
    //                result.Status = $"processing {i}/{facilities.Count + facilities.Count * 3}";
    //                result.SetValue(redis);
    //            }

    //            SectionProperties sectionProperties1 = new SectionProperties() { RsidRPr = "00EC0123", RsidR = "00EC0123", RsidSect = "0009565B" };
    //            PageSize pageSize1 = new PageSize() { Width = (UInt32Value)11906U, Height = (UInt32Value)16838U };
    //            PageMargin pageMargin1 = new PageMargin() { Top = 1701, Right = (UInt32Value)1701U, Bottom = 1701, Left = (UInt32Value)1701U, Header = (UInt32Value)851U, Footer = (UInt32Value)992U, Gutter = (UInt32Value)0U };
    //            Columns columns1 = new Columns() { Space = "425" };
    //            DocGrid docGrid1 = new DocGrid() { Type = DocGridValues.Lines, LinePitch = 312 };

    //            sectionProperties1.Append(pageSize1);
    //            sectionProperties1.Append(pageMargin1);
    //            sectionProperties1.Append(columns1);
    //            sectionProperties1.Append(docGrid1);
    //            body1.Append(sectionProperties1);
    //            document1.Append(body1);
    //            maindoc.Document = document1;
    //            word.Save();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        result.Status = "error";
    //        result.SetValue(redis);
    //        Console.WriteLine(ex.ToString());
    //    }

    //    ms.Seek(0, SeekOrigin.Begin);
    //    var fileLocation = $"{factory.Id}-{task.Id}-{DateTime.Now.ToString("yyyyMMddHHmmss")}-{Guid.NewGuid().ToString("N")}.docx";
    //    try
    //    {
    //        var pt = System.IO.Path.Combine(config.MinioDataPath, "GenerateReports");
    //        if (!Directory.Exists(pt))
    //        {
    //            Directory.CreateDirectory(pt);
    //        }
    //        var filename = System.IO.Path.Combine(pt, fileLocation);
    //        using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
    //        {
    //            await ms.CopyToAsync(fs, 1024 * 1024 * 10);
    //        }
    //        result.Status = "done";
    //        result.Location = $"GenerateReports/{fileLocation}";
    //        result.SetValue(redis);
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine(ex.ToString());
    //    }
    //    return result;
    //}
    public static async Task<ReportResult> BladeReport(BklConfig config, IRedisClient redis, GenerateAllTaskRequest req)
    {
        long taskId = req.taskId;
        long factoryId = req.factoryId;
        string mode = req.mode;
        BklFactory factory = req.factory;
        BklInspectionTask task = req.task;
        List<BklFactoryFacility> facilities = req.facilities;
        List<BklInspectionTaskDetail> taskDetails = req.taskDetails;
        List<BklInspectionTaskResult> taskResults = req.taskResults;
        MemoryStream ms = new MemoryStream();
        List<ICreateParagraph> lis = new List<ICreateParagraph>();
        var create = new CreateWord();
        var result = new ReportResult
        {
            SeqId = req.SeqId,
            TaskId = req.taskId,
            FactoryId = factory.Id,
            FacilityCount = facilities.Count,
            Status = "init",
            FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{task.Id}-{facilities.Count}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx",
            StartTime = DateTime.Now,
        };
        if (facilities.Count == 1)
        {
            result.FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{facilities[0].Name}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
        }
        else
        {
            if (facilities.Count < 10)
                result.FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{string.Join("-", facilities.Select(s => s.Name))}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
            else
                result.FileName = $"缺陷报告{factory.FactoryName}-{task.TaskName}-{facilities.Count}台风机-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
        }
        result.SetValue(redis);
        try
        {
            using (WordprocessingDocument word = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
               
                int i = 0;
                foreach (var faci in facilities.OrderBy(s => s.Name, compare))
                {
                    i++;
                    var taskDetailsTemp = taskDetails
                        .Where(s => s.FactoryId == factoryId && s.FacilityId == faci.Id)
                        .ToList();
                    var taskResultsTemp = taskResults
                        .Where(p => p.FactoryId == factoryId && p.FacilityId == faci.Id && p.TaskId == taskId)
                        .ToList();

                    var vals = redis.GetValuesFromHash($"FacilityMeta:{faci.Id}");

                    lis.Add(new CreateTextParagraph(
                            $"表O-{i.ToString().PadLeft(2, '0')}  {faci.Name.ToUpper()}风机叶片外观检查记录",
                            fontSize: "24",
                            values: JustificationValues.Center,
                            font: new RunFonts()
                            {
                                Hint = FontTypeHintValues.EastAsia,
                                Ascii = "Times New Roman",
                                HighAnsi = "Times New Roman",
                                EastAsia = "宋体"
                            },
                            lines: new SpacingBetweenLines { Before = "240", After = "120" },
                            outlineLevel: 2
                        )
                    );
                    lis.Add(new CreateBookmarkEnd());
                    lis.Add(new CreateFJExportNoOpenCVParagraph(config, word, redis, taskDetailsTemp, taskResultsTemp, factory, faci, mode));
                    result.Status = $"processing {i}/{facilities.Count + facilities.Count * 3}";
                    result.SetValue(redis);
                }
                var maindoc = word.AddMainDocumentPart();
                Document document1 = new Document()
                {
                    MCAttributes = new MarkupCompatibilityAttributes()
                    {
                        Ignorable = "w14 w15 wp14"
                    }
                };
                document1.AddNamespaceDeclaration("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas");
                document1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
                document1.AddNamespaceDeclaration("o", "urn:schemas-microsoft-com:office:office");
                document1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                document1.AddNamespaceDeclaration("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");
                document1.AddNamespaceDeclaration("v", "urn:schemas-microsoft-com:vml");
                document1.AddNamespaceDeclaration("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing");
                document1.AddNamespaceDeclaration("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing");
                document1.AddNamespaceDeclaration("w10", "urn:schemas-microsoft-com:office:word");
                document1.AddNamespaceDeclaration("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                document1.AddNamespaceDeclaration("w14", "http://schemas.microsoft.com/office/word/2010/wordml");
                document1.AddNamespaceDeclaration("w15", "http://schemas.microsoft.com/office/word/2012/wordml");
                document1.AddNamespaceDeclaration("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup");
                document1.AddNamespaceDeclaration("wpi", "http://schemas.microsoft.com/office/word/2010/wordprocessingInk");
                document1.AddNamespaceDeclaration("wne", "http://schemas.microsoft.com/office/word/2006/wordml");
                document1.AddNamespaceDeclaration("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape");

                Body body1 = new Body();
                int j = 0;
                foreach (var paragraph1 in lis)
                {
                    body1.Append(paragraph1.AddParagraph());
                    Console.WriteLine($"WordCreatePara total:{lis.Count} cur:{j}");
                    j++;
                    i++;
                    result.Status = $"processing {i}/{facilities.Count + facilities.Count * 3}";
                    result.SetValue(redis);
                }

                SectionProperties sectionProperties1 = new SectionProperties() { RsidRPr = "00EC0123", RsidR = "00EC0123", RsidSect = "0009565B" };
                PageSize pageSize1 = new PageSize() { Width = (UInt32Value)11906U, Height = (UInt32Value)16838U };
                PageMargin pageMargin1 = new PageMargin() { Top = 1701, Right = (UInt32Value)1701U, Bottom = 1701, Left = (UInt32Value)1701U, Header = (UInt32Value)851U, Footer = (UInt32Value)992U, Gutter = (UInt32Value)0U };
                Columns columns1 = new Columns() { Space = "425" };
                DocGrid docGrid1 = new DocGrid() { Type = DocGridValues.Lines, LinePitch = 312 };

                sectionProperties1.Append(pageSize1);
                sectionProperties1.Append(pageMargin1);
                sectionProperties1.Append(columns1);
                sectionProperties1.Append(docGrid1);
                body1.Append(sectionProperties1);
                document1.Append(body1);
                maindoc.Document = document1;
                word.Save();
            }
        }
        catch (Exception ex)
        {
            result.Status = "error";
            result.SetValue(redis);
            Console.WriteLine(ex.ToString());
        }

        ms.Seek(0, SeekOrigin.Begin);
        var fileLocation = $"{factory.Id}-{task.Id}-{DateTime.Now.ToString("yyyyMMddHHmmss")}-{Guid.NewGuid().ToString("N")}.docx";
        try
        {
            var pt = System.IO.Path.Combine(config.MinioDataPath, "GenerateReports");
            if (!Directory.Exists(pt))
            {
                Directory.CreateDirectory(pt);
            }
            var filename = System.IO.Path.Combine(pt, fileLocation);
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                await ms.CopyToAsync(fs, 1024 * 1024 * 10);
            }
            result.Status = "done";
            result.Location = $"GenerateReports/{fileLocation}";
            result.SetValue(redis);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        return result;
    }
}