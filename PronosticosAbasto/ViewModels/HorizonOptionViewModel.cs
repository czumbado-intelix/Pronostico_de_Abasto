using PronosticosAbasto.Core.Analysis;

namespace PronosticosAbasto.ViewModels;

public sealed class HorizonOptionViewModel
{
    public HorizonOptionViewModel(AnalysisHorizon value, string label)
    {
        Value = value;
        Label = label;
    }

    public AnalysisHorizon Value { get; }

    public string Label { get; }
}
