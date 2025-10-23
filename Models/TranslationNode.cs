using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace RimWorldModTranslate.Models;

public class TranslationNode : INotifyPropertyChanged
{
    private string? _translation;
    private bool _isEditing;
    private string? _submittedTranslation;

    public string ElementName { get; init; } = "";
    public string OriginalText { get; init; } = "";
    public string DefName { get; init; } = "";
    public XElement? BoundElement { get; init; }
    
    private List<string> _grammarIssues = [];
    public List<TranslationNode> Children { get; set; } = [];

    public string? Translation
    {
        get => _translation;
        set
        {
            if (_translation == value) return;
            _translation = value;
            OnPropertyChanged();
        }
    }

    public string? SubmittedTranslation
    {
        get => _submittedTranslation;
        set
        {
            if (_submittedTranslation == value) return;
            _submittedTranslation = value;
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    public string DisplayText =>
        IsEditing
            ? $"{ElementName}: {OriginalText} →"
            : !string.IsNullOrWhiteSpace(SubmittedTranslation)
                ? $"{ElementName}: {OriginalText} → {SubmittedTranslation}"
                : $"{ElementName}: {OriginalText}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    
    public List<string> GrammarIssues
    {
        get => _grammarIssues;
        set
        {
            _grammarIssues = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasGrammarIssues));
            OnPropertyChanged(nameof(GrammarSummary));
        }
    }

    public bool HasGrammarIssues => GrammarIssues.Count > 0;
    public string GrammarSummary => GrammarIssues.Count == 0
        ? ""
        : string.Join(Environment.NewLine, GrammarIssues);
}