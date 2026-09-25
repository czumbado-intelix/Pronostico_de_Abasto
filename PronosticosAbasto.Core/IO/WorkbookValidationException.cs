namespace PronosticosAbasto.Core.IO;

public sealed class WorkbookValidationException : Exception
{
    public WorkbookValidationException(string message)
        : base(message)
    {
    }
}
