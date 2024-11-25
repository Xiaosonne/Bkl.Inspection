using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using static CreateTableParagraph;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
namespace Bkl.Inspection.Bussiness
{
    public static class CreateWordElementsHelper
    {
        /// <summary>
        /// 文本段落
        /// </summary>
        /// <param name="text"></param>
        /// <param name="fontSize"></param>
        /// <param name="fontStyle"></param>
        /// <param name="justification"></param>
        /// <returns></returns>
        public static Paragraph CreateText(string text, string fontSize, string fontStyle, Justification justification = null)
        {
            // 创建一个新段落
            Paragraph paragraph = new Paragraph();
            ParagraphProperties properties = new ParagraphProperties();

            Indentation indentation = new Indentation() { FirstLineChars = 200 };
            if (justification != null) properties.Append(justification);
            else
                properties.Append(indentation);
            // 添加文本
            RunFonts runFonts2 = new RunFonts()
            {
                Hint = FontTypeHintValues.EastAsia,
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                EastAsia = fontStyle,
                ComplexScript = "Times New Roman"
            };
            Run run = new Run(runFonts2, new RunProperties(new FontSize { Val = fontSize }), new Text(text));
            paragraph.Append(properties);
            paragraph.Append(run);

            return paragraph;
        }
        /// <summary>
        /// 添加图片
        /// </summary>
        /// <param name="mainPart"></param>
        /// <param name="imagePath"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Paragraph AddImageToBody(MainDocumentPart mainPart, string imagePath, int x = 1, int y = 1)
        {
            // 添加图片
            if (string.IsNullOrEmpty(imagePath) || mainPart == null) return new Paragraph();
            ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
            if (imagePath.Contains("http"))
            {

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(imagePath);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // 确保请求成功  
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // 获取响应的内容作为流  
                    using (Stream stream = response.GetResponseStream())
                    {
                        imagePart.FeedData(stream);
                    }
                }
            }
            else
            {
                using (System.IO.FileStream stream = new System.IO.FileStream(imagePath, System.IO.FileMode.Open))
                {
                    imagePart.FeedData(stream);
                }
            }
            string relationshipId = mainPart.GetIdOfPart(imagePart);
            var element =
                 new Drawing(
                     new DW.Inline(
                         new DW.Extent() { Cx = 990000L * x, Cy = 792000L * y },
                         new DW.EffectExtent()
                         {
                             LeftEdge = 0L,
                             TopEdge = 0L,
                             RightEdge = 0L,
                             BottomEdge = 0L
                         },
                         new DW.DocProperties()
                         {
                             Id = (UInt32Value)1U,
                             Name = "Picture"
                         },
                         new DW.NonVisualGraphicFrameDrawingProperties(
                             new A.GraphicFrameLocks() { NoChangeAspect = true }),
                         new A.Graphic(
                             new A.GraphicData(
                                 new PIC.Picture(
                                     new PIC.NonVisualPictureProperties(
                                         new PIC.NonVisualDrawingProperties()
                                         {
                                             Id = (UInt32Value)0U,
                                             Name = "New Bitmap Image.jpg"
                                         },
                                         new PIC.NonVisualPictureDrawingProperties()),
                                     new PIC.BlipFill(
                                         new A.Blip(
                                             new A.BlipExtensionList(
                                                 new A.BlipExtension()
                                                 {
                                                     Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}"
                                                 })
                                         )
                                         {
                                             Embed = relationshipId,
                                             CompressionState = A.BlipCompressionValues.Print
                                         },
                                         new A.Stretch(
                                             new A.FillRectangle())),
                                     new PIC.ShapeProperties(
                                         new A.Transform2D(
                                             new A.Offset() { X = 0L, Y = 0L },
                                             new A.Extents() { Cx = 990000L * x, Cy = 792000L * y }),
                                         new A.PresetGeometry(
                                             new A.AdjustValueList()
                                         )
                                         { Preset = A.ShapeTypeValues.Rectangle }))
                             )
                             { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                     )
                     {
                         DistanceFromTop = (UInt32Value)0U,
                         DistanceFromBottom = (UInt32Value)0U,
                         DistanceFromLeft = (UInt32Value)0U,
                         DistanceFromRight = (UInt32Value)0U,
                         EditId = "50D07946"
                     });
            return new Paragraph(new Run(element));
        }
        /// <summary>
        /// 空段落
        /// </summary>
        /// <returns></returns>
        public static Paragraph AddBreakParagraph()
        {
            Paragraph paragraph5 = new Paragraph() { RsidParagraphAddition = "006A0F5A", RsidParagraphProperties = "006A0F5A", RsidRunAdditionDefault = "006A0F5A", ParagraphId = "048DA5A9", TextId = "77777777" };

            ParagraphProperties paragraphProperties5 = new ParagraphProperties();
            AdjustRightIndent adjustRightIndent3 = new AdjustRightIndent() { Val = false };
            SnapToGrid snapToGrid3 = new SnapToGrid() { Val = false };

            ParagraphMarkRunProperties paragraphMarkRunProperties3 = new ParagraphMarkRunProperties();
            RunFonts runFonts10 = new RunFonts() { ComplexScript = "Times New Roman" };

            paragraphMarkRunProperties3.Append(runFonts10);

            paragraphProperties5.Append(adjustRightIndent3);
            paragraphProperties5.Append(snapToGrid3);
            paragraphProperties5.Append(paragraphMarkRunProperties3);

            Run run24 = new Run();

            RunProperties runProperties23 = new RunProperties();
            RunFonts runFonts11 = new RunFonts() { ComplexScript = "Times New Roman" };

            runProperties23.Append(runFonts11);
            Break break1 = new Break() { Type = BreakValues.Page };

            run24.Append(runProperties23);
            run24.Append(break1);

            paragraph5.Append(paragraphProperties5);
            paragraph5.Append(run24);


            return paragraph5;
        }
        /// <summary>
        /// 标题
        /// </summary>
        /// <param name="text"></param>
        /// <param name="fontStyle"></param>
        /// <param name="fontSize"></param>
        /// <param name="outlineLevel"></param>
        /// <param name="justification"></param>
        /// <param name="spacingBetweenLines"></param>
        /// <param name="bold"></param>
        /// <returns></returns>
        public static Paragraph GetTitle(string text, string fontStyle = "黑体", string fontSize = "30", int outlineLevel = 0, Justification justification = null, SpacingBetweenLines spacingBetweenLines = null, Bold bold = null)
        {
            Paragraph paragraph1 = new Paragraph() { RsidParagraphMarkRevision = "007A7D1A", RsidParagraphAddition = "007A7D1A", RsidParagraphProperties = "007A7D1A", RsidRunAdditionDefault = "007A7D1A" };

            ParagraphProperties paragraphProperties1 = new ParagraphProperties();
            KeepNext keepNext1 = new KeepNext();
            KeepLines keepLines1 = new KeepLines();
            AutoSpaceDE autoSpaceDE1 = new AutoSpaceDE() { Val = false };
            AutoSpaceDN autoSpaceDN1 = new AutoSpaceDN() { Val = false };
            AdjustRightIndent adjustRightIndent1 = new AdjustRightIndent() { Val = false };
            SnapToGrid snapToGrid1 = new SnapToGrid() { Val = false };


            paragraphProperties1.Append(keepNext1);
            paragraphProperties1.Append(keepLines1);
            paragraphProperties1.Append(autoSpaceDE1);
            paragraphProperties1.Append(autoSpaceDN1);
            paragraphProperties1.Append(adjustRightIndent1);
            paragraphProperties1.Append(snapToGrid1);
            if (spacingBetweenLines != null)
                paragraphProperties1.Append(spacingBetweenLines);
            else
            {
                SpacingBetweenLines spacingBetweenLines1 = new SpacingBetweenLines() { Before = "300", Line = "250", LineRule = LineSpacingRuleValues.Auto };
                paragraphProperties1.Append(spacingBetweenLines1);
            }
            if (justification != null)
                paragraphProperties1.Append(justification);
            if (outlineLevel > 0)
                paragraphProperties1.Append(new OutlineLevel() { Val = (outlineLevel - 1) });


            BookmarkStart bookmarkStart1 = new BookmarkStart() { Name = $"_Toc{Guid.NewGuid().ToString()}", Id = "2" };


            Run run1 = new Run() { RsidRunProperties = "007A7D1A" };

            RunProperties runProperties1 = new RunProperties();
            RunFonts runFonts2 = new RunFonts()
            {
                Hint = FontTypeHintValues.EastAsia,
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                EastAsia = fontStyle,
                ComplexScript = "Times New Roman"
            };
            BoldComplexScript boldComplexScript2 = new BoldComplexScript();
            Kern kern2 = new Kern() { Val = (UInt32Value)44U };
            FontSize fontSize2 = new FontSize() { Val = fontSize };
            if (bold != null) runProperties1.Append(bold);
            runProperties1.Append(runFonts2);
            runProperties1.Append(boldComplexScript2);
            runProperties1.Append(kern2);
            runProperties1.Append(fontSize2);
            LastRenderedPageBreak lastRenderedPageBreak1 = new LastRenderedPageBreak();
            Text text1 = new Text();
            text1.Text = text;

            run1.Append(runProperties1);
            run1.Append(lastRenderedPageBreak1);
            run1.Append(text1);
            BookmarkEnd bookmarkEnd1 = new BookmarkEnd() { Id = "2" };

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(bookmarkStart1);
            paragraph1.Append(run1);
            paragraph1.Append(bookmarkEnd1);
            return paragraph1;

        }
        /// <summary>
        /// 表格边框样式
        /// </summary>
        /// <returns></returns>
        public static TableProperties TableStyle()
        {
            TableProperties tblProp = new TableProperties(
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableWidth() { Width = "8505", Type = TableWidthUnitValues.Dxa },
               new TableBorders(
                                new TopBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)15U, Space = (UInt32Value)0U },
                                new LeftBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U },
                                new BottomBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)15U, Space = (UInt32Value)0U },
                                new RightBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U },
                                new InsideHorizontalBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U },
                                new InsideVerticalBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U }
                ),
                new TableCellMarginDefault(
                    new TableCellLeftMargin() { Width = 108, Type = TableWidthValues.Dxa },
                    new TableCellRightMargin() { Width = 108, Type = TableWidthValues.Dxa }),
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                  new TableRowHeight() { Val = (UInt32Value)600U, HeightType = HeightRuleValues.AtLeast }
            );
            return tblProp;
        }
        /// <summary>
        /// 表格
        /// </summary>
        /// <param name="dataSource"></param>
        /// <param name="column"></param>
        /// <param name="headers"></param>
        /// <param name="colWidth"></param>
        /// <param name="colMerge"></param>
        /// <param name="rowMerge"></param>
        /// <returns></returns>
        public static Table GetTable(IEnumerable<Dictionary<string, string>> dataSource,
        string[] column,
        string[] headers = null,
        Dictionary<string, int> colWidth = null, Merge colMerge = null, Merge rowMerge = null, string fontSize = "24")
        {
            Table table = new Table();


            table.AppendChild<TableProperties>(TableStyle());

            if (headers != null)
            {
                TableRow tr = new TableRow();
                foreach (var col in headers)
                {
                    TableCell tc1 = new TableCell();
                    tc1.Append(new Paragraph(
                        new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                        new Run(
                            new RunProperties(
                                new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                                new FontSize { Val = fontSize },
                                new FontSizeComplexScript() { Val = fontSize }),
                            new Text(col))));
                    tr.Append(tc1);
                }
                table.Append(tr);
            }
            int rowIndex = 0;
            foreach (var raw in dataSource)
            {
                TableRow tr = new TableRow();
                int colIndex = 0;
                foreach (var col in column)
                {
                    var tcProperties = new TableCellProperties();
                    tcProperties.Append(new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = PercentWidth(colWidth != null ? colWidth[col] : 50) });
                    tcProperties.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center });
                    if (rowMerge != null)
                    {
                        if (rowIndex <= rowMerge.EndRow && rowIndex >= rowMerge.StartRow && colIndex <= rowMerge.EndCol && colIndex >= rowMerge.StartCol)
                        {
                            if (rowIndex == rowMerge.StartRow)
                                tcProperties.Append(new VerticalMerge() { Val = MergedCellValues.Restart });
                            else
                                tcProperties.Append(new VerticalMerge());
                        }
                    }
                    if (colMerge != null)
                    {
                        if (rowIndex <= colMerge.EndRow && rowIndex >= colMerge.StartRow)
                        {
                            if (colIndex <= colMerge.EndCol && colIndex >= colMerge.StartCol)
                            {
                                if (colIndex == colMerge.StartCol)
                                {
                                    tcProperties.Append(new GridSpan { Val = colMerge.EndCol - colMerge.StartCol + 1 });
                                }
                                else
                                {
                                    colIndex++;
                                    continue;
                                }
                            }
                        }
                    }
                    TableCell tc1 = new TableCell(new Paragraph(
                        new ParagraphProperties(
                              new SpacingBetweenLines() { Before = "60", After = "60", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                            new Justification { Val = JustificationValues.Center }),
                        new Run(
                          new RunProperties(
                            new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                            new FontSize { Val = fontSize },
                            new FontSizeComplexScript() { Val = fontSize }),
                            new Text(raw[col])
                        )));
                    tc1.Append(tcProperties);
                    tr.Append(tc1);
                    colIndex++;
                }
                rowIndex++;
                table.Append(tr);
            }

            return table;
        }


        public static Table InspOverview(IEnumerable<Dictionary<string, string>> dataSource,
        string[] column,
        string[] headers = null,
        Dictionary<string, int> colWidth = null, Merge colMerge = null, Merge rowMerge = null, string fontSize = "24")
        {
            Table table = new Table();

            table.AppendChild<TableProperties>(TableStyle());
            if (headers != null)
            {
                TableRow headerRow = new TableRow();
                foreach (var item in headers)
                {
                    TableCell tc = new TableCell();
                  
                    tc.Append(new TableCellProperties(), new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }), new Run(new Text(item))));
                    headerRow.Append(tc);
                }
                table.Append(headerRow);
            }

            foreach (var item in dataSource)
            {
                TableRow tr = new TableRow();
                foreach (var col in column)
                {
                    if (item["1"].Equals("站点名称") || item["1"].Equals("任务名称")
                        || item["1"].Equals("任务类型") || item["1"].Equals("开始时间") || item["1"].Equals("结束时间")

                        )
                    {
                        TableCell tc = new TableCell();
                        if (col.Equals("1"))
                        {
                            tc.Append(new TableCellProperties(new GridSpan { Val = 1 }), new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }), new Run(new Text(item[col]))));
                            tr.Append(tc);
                        }
                        else if (col.Equals("2"))
                        {
                            tc.Append(new TableCellProperties(new GridSpan { Val = 4 }), new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }), new Run(new Text(item[col]))));
                            tr.Append(tc);
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        TableCell tc = new TableCell();
                        tc.Append(new TableCellProperties(new GridSpan { Val = 1 }), new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }), new Run(new Text(item[col]))));
                        tr.Append(tc);
                    }
                }

                table.Append(tr);   
            }


            return table;


        }
    }
}
