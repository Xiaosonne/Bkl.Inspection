using DocumentFormat.OpenXml;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Linq;

namespace Bkl.Inspection.Bussiness
{
    public class CreateErrorTable : ICreateParagraph
    {
        private Dictionary<string, List<Dictionary<string, string>>> _data2;

        public CreateErrorTable(
            Dictionary<string, List<Dictionary<string, string>>> data2
            )
        {
            _data2 = data2;
        }
        public OpenXmlElement AddParagraph()
        {
            List<OpenXmlElement> tbs = new List<OpenXmlElement>();
            string[] header = null;

            foreach (var item in _data2)
            {
                tbs.Add(CreateWordElementsHelper.GetTitle(item.Key, fontStyle: "宋体", fontSize: "21", outlineLevel: 3, justification: new Justification { Val = JustificationValues.Left }));
                header = item.Value.FirstOrDefault().Keys.ToArray();
                tbs.Add(CreateWordElementsHelper.GetTable(item.Value, header, header, fontSize: "21"));
            }
            var contentPr = new Paragraph(
              new ParagraphProperties(new Justification { Val = JustificationValues.Center },
              new AdjustRightIndent { Val = false },
           new SnapToGrid() { Val = false }
           ),
              new Run());



            tbs.ForEach(s => contentPr.Append(s));
            return contentPr;
        }

        public void Done()
        {
        }
    }
}
