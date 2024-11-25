using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using Bkl.Models;
using System.Linq;
using System.Reactive.Linq;
using Bkl.Infrastructure;

public class CreateCheckTableParagraph : ICreateParagraph
{
	public static string PercentWidth(int pers)
	{
		return ((pers * (11906 - 2880)) / 100).ToString();
	}
	private List<BklInspectionTaskResult> _results;
	private IRedisClient _redis;

	public CreateCheckTableParagraph(List<BklInspectionTaskResult> res, IRedisClient redis)
	{
		_results = res;
		_redis = redis;
	}

	public OpenXmlElement AddParagraph()
	{
		Table table = new Table();
		TableProperties tblProp = new TableProperties(
		   new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct },
		   new TableBorders(
				new TopBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
				new LeftBorder() { Val = BorderValues.Dotted, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U },
				new BottomBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
				new RightBorder() { Val = BorderValues.Dotted, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U },
				new InsideHorizontalBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U },
				new InsideVerticalBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U }
		   ),
		   new TableCellMarginDefault(
			   new TopMargin() { Width = "0", Type = TableWidthUnitValues.Dxa },
			   new BottomMargin() { Width = "0", Type = TableWidthUnitValues.Dxa },
			   new TableCellLeftMargin() { Width = 3, Type = TableWidthValues.Dxa },
			   new TableCellRightMargin() { Width = 3, Type = TableWidthValues.Dxa }
		   ),
		   new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
	   );

		TableGrid tableGrid1 = new TableGrid();

		tableGrid1.Append(new GridColumn() { Width = PercentWidth(17) });
		tableGrid1.Append(new GridColumn() { Width = PercentWidth(16) });
		tableGrid1.Append(new GridColumn() { Width = PercentWidth(17) });
		tableGrid1.Append(new GridColumn() { Width = PercentWidth(16) });
		tableGrid1.Append(new GridColumn() { Width = PercentWidth(18) });
		tableGrid1.Append(new GridColumn() { Width = PercentWidth(16) });
		// tableGrid1.Append(new GridColumn() { Width = PercentWidth(50) });
		// tableGrid1.Append(new GridColumn() { Width = PercentWidth(50) });

		// Append the TableProperties object to the empty table.
		table.AppendChild<TableProperties>(tblProp);
		table.Append(tableGrid1);
		table.Append(new TableRow(
			new TableCell(
			new TableCellProperties(
				new TableCellWidth() { Width = PercentWidth(80), Type = TableWidthUnitValues.Dxa },
				new GridSpan { Val = 6 },
				new Paragraph(new Run(new Text("叶片基本信息")))
				)
			)));
		string[] cols = new string[] {
			"风场名称",
			"风机编号",
			"叶片厂家",
			"叶片型号",
			"叶片长度",
			"检查设备", };
		int i = 0;
		var f = _results.FirstOrDefault();
		var dict = _redis.GetValuesFromHash($"FacilityMeta:{f.FacilityId}");
		Dictionary<string, string> dic = new Dictionary<string, string> {
			{"风场名称",f.FactoryName },
			{"风机编号",f.FacilityName},
			{"叶片厂家",dict.ContainsKey("叶片厂商")? dict["叶片厂商"].ToString():"" },
			{"叶片型号",dict.ContainsKey("叶片型号")?dict["叶片型号"].ToString():""},
			{"叶片长度",dict.ContainsKey("叶片长度")?dict["叶片长度"].ToString():"" },
			{"检查设备","大疆M300" },
		};
		var totalLength = int.Parse(dict.ContainsKey("叶片长度") ? dict["叶片长度"].ToString() : "50");
		var first = totalLength * 0.3;
		var second = totalLength * 0.8;
		TableRow tr = null;
		foreach (var item in cols)
		{
			if (i % 3 == 0)
			{
				tr = new TableRow();
				table.Append(tr);
			}
			tr.Append(new TableCell(new TableCellProperties(new TableCellWidth() { Width = PercentWidth(16), Type = TableWidthUnitValues.Dxa }, new GridSpan { Val = 1 }, new Paragraph(new Run(new Text(item))))));
			tr.Append(new TableCell(new TableCellProperties(new TableCellWidth() { Width = PercentWidth(16), Type = TableWidthUnitValues.Dxa }, new GridSpan { Val = 1 }, new Paragraph(new Run(new Text(dic[item]))))));
			i++;
		}
		var row1 = new TableRow();
		var tc2 = new TableCell(new TableCellProperties(new TableCellWidth() { Width = PercentWidth(50), Type = TableWidthUnitValues.Dxa }, new GridSpan { Val = 4 }, new Paragraph(new Run(new RunProperties(new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "宋体", HighAnsi = "Times New Roman" }), new Text("      正常□") { Space = SpaceProcessingModeValues.Preserve }, new Text("      异响□") { Space = SpaceProcessingModeValues.Preserve }))));
		var tc1 = new TableCell(new TableCellProperties(new TableCellWidth() { Width = PercentWidth(30), Type = TableWidthUnitValues.Dxa }, new GridSpan { Val = 2 }, new Paragraph(new Run(new Text("叶片运行")))));
		row1.Append(tc1);
		row1.Append(tc2);
		table.Append(row1);
		string[] yps = new string[] { "叶片A", "叶片B", "叶片C" };
		string[] mian = new string[] { "背风面", "迎风面" };
		string[] bu = new string[] { "根部", "中部", "尖部" };
		TableRow tr1 = null;
		TableRow tr2 = null;
		int j = 0;
		string[] err = new string[] { "修补痕迹", " 表面漆脱落", "    裂纹", "       腐蚀", "雷击损伤" };
		foreach (var yp in yps)
		{
			foreach (var m in mian)
			{
				var allerror = _results.Where(s => s.Position.StartsWith(yp) && s.Position.EndsWith(m))
				.ToList();

				foreach (var b in bu)
				{
					bool err1 = false, errTuoluo = false, errKailie = false, errFushi = false, errLeiji = false;

					switch (b)
					{
						case "根部":
							foreach (var ae in allerror)
							{
								if (errTuoluo && errKailie && errFushi && errLeiji)
									break;
								var pos = ae.DamagePosition.Substring(6).TrimEnd('m');
								if (float.TryParse(pos, out var fpos) && fpos <= first)
								{
									if (ae.DamageType.Contains("航标漆") || ae.DamageType.Contains("胶衣脱落"))
									{
										errTuoluo = true;
									}
									if (ae.DamageType.Contains("裂纹"))
									{
										errKailie = true;
									}
									if (ae.DamageType.Contains("腐蚀"))
									{
										errFushi = true;
									}
									if (ae.DamageType.Contains("雷击"))
									{
										errLeiji = true;
									}
								}
							}
							break;
						case "中部":
							foreach (var ae in allerror)
							{
								if (errTuoluo && errKailie && errFushi && errLeiji)
									break;
								var pos = ae.DamagePosition.Substring(6).TrimEnd('m');
								if (float.TryParse(pos, out var fpos) && fpos > first && fpos <= second)
								{
									if (ae.DamageType.Contains("航标漆") || ae.DamageType.Contains("胶衣脱落"))
									{
										errTuoluo = true;
									}
									if (ae.DamageType.Contains("裂纹"))
									{
										errKailie = true;
									}
									if (ae.DamageType.Contains("腐蚀"))
									{
										errFushi = true;
									}
									if (ae.DamageType.Contains("雷击"))
									{
										errLeiji = true;
									}
								}
							}
							break;
						case "尖部":
							foreach (var ae in allerror)
							{
								if (errTuoluo && errKailie && errFushi && errLeiji)
									break;
								var pos = ae.DamagePosition.Substring(6).TrimEnd('m');
								if (float.TryParse(pos, out var fpos) && fpos >= second)
								{
									if (ae.DamageType.Contains("航标漆") || ae.DamageType.Contains("胶衣脱落"))
									{
										errTuoluo = true;
									}
									if (ae.DamageType.Contains("裂纹"))
									{
										errKailie = true;
									}
									if (ae.DamageType.Contains("腐蚀"))
									{
										errFushi = true;
									}
									if (ae.DamageType.Contains("雷击"))
									{
										errLeiji = true;
									}
								}
							}
							break;
					}
					if (j % 3 == 0)
					{
						tr1 = new TableRow();
						tr2 = new TableRow();
						table.Append(tr1);
						table.Append(tr2);
					}
					tr1.Append(new TableCell(
					   new TableCellProperties(
						   new TableCellWidth() { Width = PercentWidth(32), Type = TableWidthUnitValues.Dxa },
						   new GridSpan { Val = 2 },
						   new Paragraph(new ParagraphProperties(
							  new SpacingBetweenLines() { Line = "360", LineRule = LineSpacingRuleValues.Auto },
							   new Justification { Val = JustificationValues.Center },
						   new Run(new Text($"{yp}{m}{b}"))))
						   )
					   ));
					var pa1 = new Paragraph(
						new Run(
							new RunProperties(
								new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "宋体", HighAnsi = "Times New Roman" }),
								new Text(err[0] + "□") { Space = SpaceProcessingModeValues.Preserve },
								new Text(err[1] + (errTuoluo ? "√" : "□")) { Space = SpaceProcessingModeValues.Preserve }));
					var pa2 = new Paragraph(
						new Run(
							new RunProperties(
								new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "宋体", HighAnsi = "Times New Roman" }),
								new Text(err[2] + (errKailie ? "√" : "□")) { Space = SpaceProcessingModeValues.Preserve },
								new Text(err[3] + (errFushi ? "√" : "□")) { Space = SpaceProcessingModeValues.Preserve }));
					var pa3 = new Paragraph(
						new Run(
							new RunProperties(
								new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "宋体", HighAnsi = "Times New Roman" }),
								new Text(err[4] + (errLeiji ? "√" : "□")) { Space = SpaceProcessingModeValues.Preserve }));

					var tp = new TableCellProperties(
						  new TableCellWidth() { Width = PercentWidth(32), Type = TableWidthUnitValues.Dxa },
						  new GridSpan { Val = 2 },
						pa1, pa2, pa3);

					tr2.Append(new TableCell(tp));
					j++;
				}
			}
		}
		return new Paragraph(
			new Run(new Text("风机检查表")),
			new Run(table)
		);
	}

	public void Done()
	{
	}
}