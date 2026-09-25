using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PronosticosAbasto.ViewModels;

/// <summary>Excel-style per-column value filter with an optional contains fallback.</summary>
public partial class TextColumnFilter : ObservableObject
{
    private readonly Action _changed;
    private bool _isRefreshingOptions;
    private HashSet<string>? _appliedSelectedKeys;

    public TextColumnFilter(Action changed) => _changed = changed;

    public ObservableCollection<ColumnFilterValueOption> Options { get; } = new();

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OptionSearchText { get; set; } = string.Empty;

    public IReadOnlyList<ColumnFilterValueOption> FilteredOptions
    {
        get
        {
            var search = OptionSearchText?.Trim();
            return string.IsNullOrEmpty(search)
                ? Options.ToArray()
                : Options
                    .Where(option => option.DisplayValue.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
        }
    }

    public bool HasOptions => Options.Count > 0;

    public bool IsValueFilterActive => _appliedSelectedKeys is not null;

    public bool IsActive =>
        !string.IsNullOrWhiteSpace(Text) || IsValueFilterActive;

    public string SelectionSummary
    {
        get
        {
            if (Options.Count == 0)
            {
                return "Sin valores";
            }

            var selected = Options.Count(option => option.IsSelected);
            return $"{selected:N0} de {Options.Count:N0} seleccionados";
        }
    }

    public bool? VisibleOptionsSelectionState
    {
        get
        {
            var visibleOptions = FilteredOptions;
            if (visibleOptions.Count == 0)
            {
                return false;
            }

            var selected = visibleOptions.Count(option => option.IsSelected);
            return selected == 0
                ? false
                : selected == visibleOptions.Count
                    ? true
                    : null;
        }
        set
        {
            if (value is bool selected)
            {
                SetVisibleOptions(selected);
            }
        }
    }

    partial void OnTextChanged(string value)
    {
        NotifyFilterStateChanged();
        if (!_isRefreshingOptions)
        {
            _changed();
        }
    }

    partial void OnOptionSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredOptions));
        NotifyDraftSelectionChanged();
    }

    public void SetOptions(IEnumerable<string?> values)
    {
        var previous = Options
            .ToDictionary(
                option => NormalizeForComparison(option.DisplayValue),
                option => option.IsSelected,
                StringComparer.OrdinalIgnoreCase);

        var uniqueValues = values
            .Select(NormalizeDisplayValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _isRefreshingOptions = true;
        try
        {
            Options.Clear();
            foreach (var value in uniqueValues)
            {
                var key = NormalizeForComparison(value);
                var isSelected = previous.TryGetValue(key, out var selected)
                    ? selected
                    : _appliedSelectedKeys?.Contains(key) ?? true;
                Options.Add(new ColumnFilterValueOption(value, isSelected, OnOptionSelectionChanged));
            }
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        NotifyOptionsChanged();
    }

    public bool Matches(string? value)
    {
        var displayValue = NormalizeDisplayValue(value);
        if (!MatchesText(displayValue, Text))
        {
            return false;
        }

        if (_appliedSelectedKeys is null)
        {
            return true;
        }

        return _appliedSelectedKeys.Contains(NormalizeForComparison(displayValue));
    }

    [RelayCommand]
    private void SelectAll() => SetAllOptions(true);

    [RelayCommand]
    private void Apply()
    {
        var selectedKeys = Options
            .Where(option => option.IsSelected)
            .Select(option => NormalizeForComparison(option.DisplayValue))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _appliedSelectedKeys = selectedKeys.Count == Options.Count
            ? null
            : selectedKeys;

        _isRefreshingOptions = true;
        try
        {
            OptionSearchText = string.Empty;
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        NotifyOptionsChanged();
        _changed();
    }

    [RelayCommand]
    private void Cancel()
    {
        _isRefreshingOptions = true;
        try
        {
            OptionSearchText = string.Empty;
            RestoreDraftFromAppliedSelection();
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        NotifyOptionsChanged();
    }

    [RelayCommand]
    private void Clear()
    {
        var shouldNotify = IsActive || !string.IsNullOrWhiteSpace(OptionSearchText);
        _isRefreshingOptions = true;
        try
        {
            _appliedSelectedKeys = null;
            Text = string.Empty;
            OptionSearchText = string.Empty;
            foreach (var option in Options)
            {
                option.IsSelected = true;
            }
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        NotifyOptionsChanged();
        if (shouldNotify)
        {
            _changed();
        }
    }

    private void SetAllOptions(bool selected)
    {
        _isRefreshingOptions = true;
        try
        {
            foreach (var option in Options)
            {
                option.IsSelected = selected;
            }
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        NotifyOptionsChanged();
    }

    private void OnOptionSelectionChanged()
    {
        if (_isRefreshingOptions)
        {
            return;
        }

        NotifyDraftSelectionChanged();
    }

    private void NotifyOptionsChanged()
    {
        OnPropertyChanged(nameof(FilteredOptions));
        OnPropertyChanged(nameof(HasOptions));
        NotifyDraftSelectionChanged();
        NotifyFilterStateChanged();
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(IsValueFilterActive));
        OnPropertyChanged(nameof(IsActive));
    }

    private void NotifyDraftSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(VisibleOptionsSelectionState));
    }

    private void SetVisibleOptions(bool selected)
    {
        _isRefreshingOptions = true;
        try
        {
            foreach (var option in FilteredOptions)
            {
                option.IsSelected = selected;
            }
        }
        finally
        {
            _isRefreshingOptions = false;
        }

        NotifyDraftSelectionChanged();
    }

    private void RestoreDraftFromAppliedSelection()
    {
        foreach (var option in Options)
        {
            option.IsSelected = _appliedSelectedKeys?.Contains(NormalizeForComparison(option.DisplayValue)) ?? true;
        }
    }

    private static bool MatchesText(string value, string? filterText)
    {
        var text = filterText?.Trim();
        return string.IsNullOrWhiteSpace(text)
            || value.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDisplayValue(string? value)
    {
        var trimmed = value?
            .Replace("\r\n", " | ", StringComparison.Ordinal)
            .Replace("\n", " | ", StringComparison.Ordinal)
            .Replace("\r", " | ", StringComparison.Ordinal)
            .Trim();
        return string.IsNullOrEmpty(trimmed) ? "Sin valor" : trimmed;
    }

    private static string NormalizeForComparison(string? value) =>
        NormalizeDisplayValue(value).ToUpperInvariant();
}

public sealed partial class ColumnFilterValueOption : ObservableObject
{
    private readonly Action _selectionChanged;

    public ColumnFilterValueOption(string displayValue, bool isSelected, Action selectionChanged)
    {
        DisplayValue = displayValue;
        _selectionChanged = selectionChanged;
        IsSelected = isSelected;
    }

    public string DisplayValue { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();
}

/// <summary>Google-Sheets-style per-column numeric min/max filter.</summary>
public partial class RangeColumnFilter : ObservableObject
{
    private readonly Action _changed;

    public RangeColumnFilter(Action changed) => _changed = changed;

    [ObservableProperty]
    public partial string MinText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxText { get; set; } = string.Empty;

    public bool IsActive => TryParse(MinText, out _) || TryParse(MaxText, out _);

    partial void OnMinTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsActive));
        _changed();
    }

    partial void OnMaxTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsActive));
        _changed();
    }

    /// <summary>Null values only match when the filter is inactive.</summary>
    public bool Matches(decimal? value)
    {
        var hasMin = TryParse(MinText, out var min);
        var hasMax = TryParse(MaxText, out var max);
        if (!hasMin && !hasMax)
        {
            return true;
        }

        if (value is not decimal number)
        {
            return false;
        }

        return (!hasMin || number >= min) && (!hasMax || number <= max);
    }

    private static bool TryParse(string? text, out decimal value)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            value = 0m;
            return false;
        }

        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    [RelayCommand]
    private void Clear()
    {
        MinText = string.Empty;
        MaxText = string.Empty;
    }
}
