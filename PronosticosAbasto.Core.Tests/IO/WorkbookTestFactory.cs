using ClosedXML.Excel;

namespace PronosticosAbasto.Core.Tests.IO;

internal static class WorkbookTestFactory
{
    public static MemoryStream Create(Action<XLWorkbook> configure)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            configure(workbook);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }
}
