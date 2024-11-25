using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Bkl.Inspection.Bussiness
{
    public class CreateTextParagraphHelper : ICreateParagraph
    {
        private readonly string _text;
        private readonly string _fontSize;
        private readonly string _fontStyle;
        private readonly Justification _justification;

        public CreateTextParagraphHelper(string text, string fontSize, string fontStyle, Justification justification = null)
        {
            _text = text;
            _fontSize = fontSize;
            _fontStyle = fontStyle;
            _justification = justification;
        }

        public OpenXmlElement AddParagraph()
        { // 创建一个新段落
            Paragraph paragraph = new Paragraph();
            ParagraphProperties properties = new ParagraphProperties();
            Indentation indentation = new Indentation() { FirstLineChars = 200 };
            if (_justification != null) properties.Append(_justification);
            else
                properties.Append(indentation);

            // 添加文本
            RunFonts runFonts2 = new RunFonts()
            {
                Hint = FontTypeHintValues.EastAsia,
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                EastAsia = _fontStyle,
                ComplexScript = "Times New Roman"
            };
            Run run = new Run(runFonts2, new RunProperties(new FontSize { Val = _fontSize }), new Text(_text));
            paragraph.Append(properties);
            paragraph.Append(run);
            return paragraph;



        }




        public void Done()
        {
        }


    }
}
