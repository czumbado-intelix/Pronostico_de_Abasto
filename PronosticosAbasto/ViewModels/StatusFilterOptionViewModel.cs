using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.ViewModels;

public sealed class StatusFilterOptionViewModel
{
    public StatusFilterOptionViewModel(StatusFilterOption value, string label)
    {
        Value = value;
        Label = label;
    }

    public StatusFilterOption Value { get; }

    public string Label { get; }
}
