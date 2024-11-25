using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using DocumentFormat.OpenXml.Drawing.Charts;

public class CreateTableParagraph : ICreateParagraph
{
    private IEnumerable<Dictionary<string, string>> _dataSource;
    private Merge _colMerge;
    private Merge _rowMerge;
    private string[] _column;
    private string[] _headers;
    private Dictionary<string, int> _colWidth;
    string _fontSize;

    public static string PercentWidth(int pers)
    {
        return ((pers * 8505) / 100).ToString();
    }
    public class Merge
    {
        public int StartRow { get; set; }
        public int EndRow { get; set; }
        public int StartCol { get; set; }
        public int EndCol { get; set; }
    }
    public CreateTableParagraph(
        IEnumerable<Dictionary<string, string>> dataSource,
        string[] column,
        string[] headers = null,

        Dictionary<string, int> colWith = null, Merge colMerge = null, Merge rowMerge = null, string fontSize = "24")
    {
        _dataSource = dataSource;
        _column = column;
        _headers = headers;
        _colWidth = colWith;
        _colMerge = colMerge;
        _rowMerge = rowMerge;
        _fontSize = fontSize;
    }
    public OpenXmlElement AddParagraph()
    {
        Table table = new Table();

        // Create a TableProperties object and specify its border information.
        TableProperties tblProp = new TableProperties(
            new TableJustification() { Val = TableRowAlignmentValues.Center },
            new TableWidth() { Width = "8505", Type = TableWidthUnitValues.Dxa },
           new TableBorders(
                            new TopBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
                            new LeftBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U },
                            new BottomBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
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
        table.AppendChild<TableProperties>(tblProp);


        // GridColumn gridColumn3 = new GridColumn() { Width = PercentWidth(50) };
        // GridColumn gridColumn4 = new GridColumn() { Width = PercentWidth(50) };
        // if (_column != null && _colWidth != null)
        // {
        //     TableGrid tableGrid1 = new TableGrid();
        //     foreach (var item in _column)
        //     {
        //         tableGrid1.Append(new GridColumn() { Width = PercentWidth(_colWidth[item]) });
        //     }
        //     table.Append(tableGrid1);

        // }
        // Append the TableProperties object to the empty table.
        if (_headers != null)
        {
            TableRow tr = new TableRow();
            foreach (var col in _headers)
            {
                TableCell tc1 = new TableCell();
                tc1.Append(new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(
                            new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                            new FontSize { Val = _fontSize },
                            new FontSizeComplexScript() { Val = _fontSize }),
                        new Text(col))));
                tr.Append(tc1);
            }
            table.Append(tr);
        }
        int rowIndex = 0;
        foreach (var raw in _dataSource)
        {
            TableRow tr = new TableRow();
            int colIndex = 0;
            foreach (var col in _column)
            {
                var tcProperties = new TableCellProperties();
                tcProperties.Append(new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = PercentWidth(_colWidth != null ? _colWidth[col] : 50) });
                tcProperties.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center });
                if (_rowMerge != null)
                {
                    if (rowIndex <= _rowMerge.EndRow && rowIndex >= _rowMerge.StartRow && colIndex <= _rowMerge.EndCol && colIndex >= _rowMerge.StartCol)
                    {
                        if (rowIndex == _rowMerge.StartRow)
                            tcProperties.Append(new VerticalMerge() { Val = MergedCellValues.Restart });
                        else
                            tcProperties.Append(new VerticalMerge());
                    }
                }
                if (_colMerge != null)
                {
                    if (rowIndex <= _colMerge.EndRow && rowIndex >= _colMerge.StartRow)
                    {
                        if (colIndex <= _colMerge.EndCol && colIndex >= _colMerge.StartCol)
                        {
                            if (colIndex == _colMerge.StartCol)
                            {
                                tcProperties.Append(new GridSpan { Val = _colMerge.EndCol - _colMerge.StartCol + 1 });
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
                        new FontSize { Val = _fontSize },
                        new FontSizeComplexScript() { Val = _fontSize }),
                        new Text(raw[col])
                    )));
                tc1.Append(tcProperties);
                tr.Append(tc1);
                colIndex++;
            }
            rowIndex++;
            table.Append(tr);
        }
        return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center },
                new AdjustRightIndent { Val = false },
             new SnapToGrid() { Val = false }
             ),
                new Run(table));
    }
    Paragraph createText(string text)
    {
        return new Paragraph(new ParagraphProperties(
                new AdjustRightIndent { Val = false },
             new SnapToGrid() { Val = false },
                            new SpacingBetweenLines() { Before = "60", After = "60", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                              new Justification { Val = JustificationValues.Center },
                              new Run(new Text(text))));
    }
    public void Done()
    {
    }
}
