using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RimWorldModTranslate.Models;

namespace RimWorldModTranslate.Views
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, FileTranslationData> _files = new();
        private readonly Dictionary<string, TranslationNode> _nodeLookup = new();
        private FileTranslationData? _currentFile;
        private string? _rootFolder;
        private readonly SettingsViewModel _settingsViewModel;
        private ListBox? _fileList;
        private TreeView? _translationTree;
        private Button? _saveButton;
        private Button? _saveAllButton;
        private TextBlock? _loadedFolderTextBlock;
        private TextBlock? _statusText;
        private TranslationNode? _activeEditNode;
        private readonly List<TranslationNode> _flatNodeList = [];

        private static readonly HashSet<string> TranslatableTags =
        [
            "label", "description", "labelShortAdj", "jobString", "reportString",
            "instruction", "helpText", "inspectString", "rejectInputMessage",
            "menuText", "confirmMessage", "text", "title"
        ];

        public MainWindow()
        {
            InitializeComponent();
            _settingsViewModel = new SettingsViewModel { SelectedLanguage = "Russian" };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _statusText = this.FindControl<TextBlock>("StatusText");
            _saveButton = this.FindControl<Button>("SaveButton");
            _saveAllButton = this.FindControl<Button>("SaveAllButton");
            _fileList = this.FindControl<ListBox>("FileList");
            _loadedFolderTextBlock = this.FindControl<TextBlock>("LoadedFolderTextBlock");
            _translationTree = this.FindControl<TreeView>("TranslationTree");

            UpdateUiState();
        }
        
        private async void OnOpenSettingsClick(object? sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow
            {
                DataContext = _settingsViewModel 
            };
            await settingsWindow.ShowDialog(this);
        }
        
        #region Folder Loading and Parsing

        private async void OnLoadFolderClick(object? _, RoutedEventArgs routedEventArgs)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select mod root folder"
            };

            var folder = await dlg.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(folder))
                return;

            LoadFolder(folder);
        }

        private void LoadFolder(string folder)
        {
            _rootFolder = folder;
            _files.Clear();
            _nodeLookup.Clear();
            _currentFile = null;

            var xmlFiles = Directory.EnumerateFiles(folder, "*.xml", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in xmlFiles)
            {
                try
                {
                    var doc = XDocument.Load(file);
                    var root = doc.Root;
                    if (root == null) continue;

                    var grouped = GroupByDef(root, file, _nodeLookup);
                    if (grouped.Any())
                    {
                        var rel = Path.GetRelativePath(folder, file);
                        _files[file] = new FileTranslationData
                        {
                            FilePath = file,
                            RelativePath = rel,
                            Doc = doc,
                            RootNodes = grouped
                        };
                    }
                }
                catch (Exception ex)
                {
                    _files[$"[error]{file}"] = new FileTranslationData
                    {
                        FilePath = file,
                        RelativePath = Path.GetRelativePath(folder, file),
                        LoadError = ex.Message
                    };
                }
            }
            
            LoadExistingTranslations();

            var fileItems = _files.Values
                .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(f => f.RelativePath + (f.LoadError != null ? $"  (error: {f.LoadError})" : ""))
                .ToList();

            _fileList!.ItemsSource = fileItems;
            _loadedFolderTextBlock!.Text = $"Folder: {folder}";
            UpdateUiState();

            if (_files.Count > 0)
                _fileList.SelectedIndex = 0;
        }

        /// <summary>
        /// Checks if a translated version of the mod exists and loads its values.
        /// </summary>
        private void LoadExistingTranslations()
        {
            if (_rootFolder is null) return;
            var targetLanguage = _settingsViewModel.SelectedLanguage;
            if (string.IsNullOrWhiteSpace(targetLanguage)) return;

            var translatedModPath = $"{_rootFolder} ({targetLanguage})";
            if (!Directory.Exists(translatedModPath)) return;
            
            _statusText!.Text = $"Loading existing translations from '{Path.GetFileName(translatedModPath)}'...";

            foreach (var originalFile in _files.Values)
            {
                if (originalFile.Doc is null) continue;

                var translatedFilePath = Path.Combine(translatedModPath, originalFile.RelativePath);
                if (originalFile.RelativePath.Contains("Languages\\English", StringComparison.OrdinalIgnoreCase))
                {
                    translatedFilePath = translatedFilePath.Replace("Languages\\English", $"Languages\\{targetLanguage}", StringComparison.OrdinalIgnoreCase);
                    var dirName = Path.GetDirectoryName(translatedFilePath) ?? "";
                    if (Path.GetFileName(dirName).Equals("Keyed", StringComparison.OrdinalIgnoreCase))
                    {
                        translatedFilePath = Path.Combine(dirName, $"{targetLanguage}.xml");
                    }
                }

                if (File.Exists(translatedFilePath))
                {
                    try
                    {
                        var translatedDoc = XDocument.Load(translatedFilePath);
                        if (translatedDoc.Root != null)
                        {
                            PopulateNodesFromTranslatedXml(translatedDoc.Root, originalFile.FilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading existing translation file '{translatedFilePath}': {ex.Message}");
                    }
                }
            }
        }
        
        #endregion

        #region XML Parsing and Node Generation

        /// <summary>
        /// Groups XML elements by their definition type.
        /// </summary>
        private static List<TranslationNode> GroupByDef(XElement root, string filePath, Dictionary<string, TranslationNode> lookup)
        {
            if (root.Name.LocalName.Equals("LanguageData", StringComparison.OrdinalIgnoreCase))
            {
                var children = new List<TranslationNode>();
                foreach (var element in root.Elements())
                {
                    var node = new TranslationNode
                    {
                        ElementName = element.Name.LocalName,
                        OriginalText = element.Value.Trim(),
                        BoundElement = element,
                        DefName = Path.GetFileName(filePath)
                    };
                    children.Add(node);
                    lookup.TryAdd($"{filePath}::{element.Name.LocalName}", node);
                }
                
                var wrapper = new TranslationNode
                {
                    ElementName = Path.GetFileName(filePath),
                    OriginalText = "Key/Value Pairs",
                    DefName = Path.GetFileName(filePath),
                    Children = children
                };
                return [wrapper];
            }

            var result = new List<TranslationNode>();
            foreach (var def in root.Elements())
            {
                var defName = def.Element("defName")?.Value ?? def.Name.LocalName;
                var basePath = $"{def.Name.LocalName}[defName={defName}]";
                var entries = ExtractTranslatables(def, defName, basePath, filePath, lookup);
                if (entries.Any())
                {
                    result.Add(new TranslationNode
                    {
                        ElementName = def.Name.LocalName,
                        OriginalText = defName,
                        DefName = defName,
                        Children = entries
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// Recursively extracts translatable elements from a parent element.
        /// </summary>
        private static List<TranslationNode> ExtractTranslatables(XElement element, string defName, string currentPath, string filePath, Dictionary<string, TranslationNode> lookup)
        {
            var nodes = new List<TranslationNode>();
            var path = $"{currentPath}/{element.Name.LocalName}";

            if (TranslatableTags.Contains(element.Name.LocalName) && !string.IsNullOrWhiteSpace(element.Value))
            {
                var node = new TranslationNode
                {
                    ElementName = element.Name.LocalName,
                    OriginalText = element.Value.Trim(),
                    BoundElement = element,
                    DefName = defName
                };
                nodes.Add(node);
                lookup.TryAdd($"{filePath}::{path}", node);
            }

            foreach (var child in element.Elements())
            {
                nodes.AddRange(ExtractTranslatables(child, defName, path, filePath, lookup));
            }

            return nodes;
        }

        /// <summary>
        /// Parses a translated XML file to populate existing translations.
        /// </summary>
        private void PopulateNodesFromTranslatedXml(XElement root, string originalFilePath)
        {
            if (root.Name.LocalName.Equals("LanguageData", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var element in root.Elements())
                {
                    if (_nodeLookup.TryGetValue($"{originalFilePath}::{element.Name.LocalName}", out var node))
                    {
                        var text = element.Value.Trim();
                        node.Translation = text;
                        node.SubmittedTranslation = text;
                    }
                }
                return;
            }

            foreach (var def in root.Elements())
            {
                var defName = def.Element("defName")?.Value ?? def.Name.LocalName;
                var basePath = $"{def.Name.LocalName}[defName={defName}]";
                PopulateFromElementRecursive(def, basePath, originalFilePath);
            }
        }
        
        private void PopulateFromElementRecursive(XElement element, string currentPath, string originalFilePath)
        {
            var path = $"{currentPath}/{element.Name.LocalName}";
            
            if (TranslatableTags.Contains(element.Name.LocalName) && !string.IsNullOrWhiteSpace(element.Value))
            {
                if (_nodeLookup.TryGetValue($"{originalFilePath}::{path}", out var node))
                {
                    var text = element.Value.Trim();
                    node.Translation = text;
                    node.SubmittedTranslation = text;
                }
            }

            foreach (var child in element.Elements())
            {
                PopulateFromElementRecursive(child, path, originalFilePath);
            }
        }

        #endregion
        
        #region UI Interactions

        private async void ApplyActiveEdit(TranslationNode node)
        {
            if (!node.IsEditing) 
                return;
    
            node.SubmittedTranslation = string.IsNullOrWhiteSpace(node.Translation) ? null : node.Translation?.Trim();
            node.IsEditing = false;

            if (!string.IsNullOrWhiteSpace(node.SubmittedTranslation))
            {
                var lang = _settingsViewModel.SelectedLanguage ?? "English";
                node.GrammarIssues = await Services.GrammarCheckerService.CheckGrammarAsync(node.SubmittedTranslation, lang);
            }

            if (_activeEditNode == node)
                _activeEditNode = null;
    
            UpdateStatus();
        }


        private void OnEditKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox { DataContext: TranslationNode node })
                return;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (e.Key)
            {
                case Key.Enter:
                {
                    ApplyActiveEdit(node);
                    e.Handled = true;

                    var currentIndex = _flatNodeList.IndexOf(node);
                    if (currentIndex >= 0 && currentIndex + 1 < _flatNodeList.Count)
                    {
                        var nextNode = _flatNodeList[currentIndex + 1];
                        StartEditingNode(nextNode);
                    }
                    break;
                }
                case Key.Escape:
                    node.IsEditing = false;
                    _activeEditNode = null; 
                    e.Handled = true;
                    break;
            }
        }
        
        private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_activeEditNode != null) 
                ApplyActiveEdit(_activeEditNode);

            if (_fileList == null) return;
            var sel = _fileList.SelectedIndex;
            if (sel < 0) return;

            var ordered = _files.Values.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
            if (sel >= ordered.Count) return;

            var data = ordered[sel];
            DisplayFile(data);
        }

        private void DisplayFile(FileTranslationData data)
        {
            _currentFile = data;
            
            // Build the flat list for navigation
            _flatNodeList.Clear();
            if (_currentFile?.RootNodes != null)
            {
                FlattenNodes(_currentFile.RootNodes);
            }
            
            _translationTree!.ItemsSource = _currentFile!.RootNodes;
            UpdateUiState();
        }

        private void OnNodeDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBlock { DataContext: TranslationNode node })
            {
                StartEditingNode(node);
            }
        }

        private void OnEditLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox { DataContext: TranslationNode node })
            {
                ApplyActiveEdit(node);
                UpdateStatus();
            }
        }
        
        private void OnEditTextBoxLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox { DataContext: TranslationNode node } tb)
                return;

            if (node.IsEditing && node == _activeEditNode)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    tb.Focus();
                    tb.CaretIndex = tb.Text?.Length ?? 0;
                }, DispatcherPriority.Render);
            }
        }


        /// <summary>
        /// Sets a node to be the active editing node.
        /// </summary>
        private void StartEditingNode(TranslationNode node)
        {
            if (_activeEditNode != null && _activeEditNode != node)
            {
                ApplyActiveEdit(_activeEditNode); 
            }
            
            node.Translation ??= node.SubmittedTranslation ?? ""; 
            node.IsEditing = true; 
            _activeEditNode = node; 
        }

        #endregion

        #region Save Logic

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (_currentFile == null || _currentFile.Doc == null) 
                return;

            var dlg = new SaveFileDialog
            {
                Title = "Save translated XML",
                InitialFileName = Path.GetFileName(_currentFile.FilePath),
                Filters = { new FileDialogFilter { Name = "XML files", Extensions = { "xml" } } }
            };

            var path = await dlg.ShowAsync(this);
            if (string.IsNullOrWhiteSpace(path)) 
                return;

            try
            {
                ApplyTranslations(_currentFile.RootNodes);
                _currentFile.Doc.Save(path);
                _statusText!.Text = $"Saved: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                await ShowError($"Failed to save file: {ex.Message}");
            }
        }

        private async void OnSaveAllClick(object? sender, RoutedEventArgs e)
        {
            if (_files.Count == 0 || _rootFolder == null) return;

            var targetLanguage = _settingsViewModel.SelectedLanguage;
            if (string.IsNullOrWhiteSpace(targetLanguage))
            {
                await ShowError("Please select a target language in Settings before saving.");
                return;
            }

            try
            {
                var parentDir = Path.GetDirectoryName(_rootFolder) ?? "";
                var folderName = Path.GetFileName(_rootFolder);
                var languageSuffix = $" ({targetLanguage})";

                var translatedFolder = folderName.EndsWith(languageSuffix, StringComparison.OrdinalIgnoreCase) ? _rootFolder :
                    Path.Combine(parentDir, $"{folderName}{languageSuffix}");

                Directory.CreateDirectory(translatedFolder);

                var errors = new List<string>();

                SaveTranslatedFiles(translatedFolder, errors, targetLanguage);
                RemoveLeftoverEnglishFolders(translatedFolder, errors);
                UpdateAboutXml(translatedFolder, errors, targetLanguage);

                _statusText!.Text = errors.Count == 0
                    ? $"✅ Translated mod saved to: {translatedFolder}"
                    : $"⚠️ Saved with {errors.Count} errors (see console/log)";

                if (errors.Count > 0)
                {
                    Console.WriteLine("Errors during save-all:");
                    errors.ForEach(Console.WriteLine);
                }
            }
            catch (Exception ex)
            {
                await ShowError($"Failed to save translated mod: {ex.Message}");
            }
        }
        
        private void SaveTranslatedFiles(string translatedFolder, ICollection<string> errors, string targetLanguage)
        {
            foreach (var data in _files.Values)
            {
                if (data.Doc == null) continue;

                try
                {
                    ApplyTranslations(data.RootNodes);

                    var destPath = Path.Combine(translatedFolder, data.RelativePath);

                    if (data.RelativePath.Contains("Languages\\English", StringComparison.OrdinalIgnoreCase))
                    {
                        destPath = destPath.Replace("Languages\\English", $"Languages\\{targetLanguage}", StringComparison.OrdinalIgnoreCase);

                        var dirName = Path.GetDirectoryName(destPath) ?? "";
                        if (Path.GetFileName(dirName).Equals("Keyed", StringComparison.OrdinalIgnoreCase))
                        {
                            destPath = Path.Combine(dirName, $"{targetLanguage}.xml");
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    data.Doc.Save(destPath);
                }
                catch (Exception ex)
                {
                    errors.Add($"{data.RelativePath}: {ex.Message}");
                }
            }
        }
        
        private static void RemoveLeftoverEnglishFolders(string translatedFolder, ICollection<string> errors)
        {
            try
            {
                var englishFolders = Directory.GetDirectories(translatedFolder, "English", SearchOption.AllDirectories);
                foreach (var folder in englishFolders)
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to delete leftover English folder: {ex.Message}");
            }
        }

        private static void UpdateAboutXml(string translatedFolder, ICollection<string> errors, string targetLanguage)
        {
            var aboutPath = Path.Combine(translatedFolder, "About", "About.xml");
            if (!File.Exists(aboutPath)) return;

            try
            {
                var doc = XDocument.Load(aboutPath);
                var nameElem = doc.Root?.Element("name");
                if (nameElem != null && !nameElem.Value.EndsWith($"({targetLanguage})"))
                {
                    nameElem.Value = $"{nameElem.Value} ({targetLanguage})";
                    doc.Save(aboutPath);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"About.xml update failed: {ex.Message}");
            }
        }

        private static void ApplyTranslations(IEnumerable<TranslationNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Children.Any())
                {
                    ApplyTranslations(node.Children);
                }
                else if (node.BoundElement != null)
                {
                    var effectiveTranslation = node.SubmittedTranslation ?? node.Translation;
                    if (!string.IsNullOrWhiteSpace(effectiveTranslation))
                    {
                        node.BoundElement.Value = effectiveTranslation;
                    }
                }
            }
        }

        #endregion

        #region Helpers
        
        /// <summary>
        /// Recursively builds a flat list of all editable leaf nodes.
        /// </summary>
        private void FlattenNodes(IEnumerable<TranslationNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Children.Any()) 
                {
                    // This is a grouping node, recurse into its children
                    FlattenNodes(node.Children);
                }
                else
                {
                    // This is an editable (leaf) node
                    _flatNodeList.Add(node);
                }
            }
        }

        private void UpdateUiState()
        {
            var hasFiles = _files.Any();
            _saveButton!.IsEnabled = _currentFile != null;
            _saveAllButton!.IsEnabled = hasFiles;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_currentFile == null)
            {
                _statusText!.Text = _files.Any() ? $"Files loaded: {_files.Count}" : "Load a mod folder to begin.";
                return;
            }

            var total = CountNodes(_currentFile.RootNodes);
            var translated = CountTranslated(_currentFile.RootNodes);
            _statusText!.Text = $"{_currentFile.RelativePath} — {translated}/{total} translated";
        }


        private static int CountNodes(IEnumerable<TranslationNode> nodes) =>
            nodes.Sum(n => (n.Children.Any() ? 0 : 1) + CountNodes(n.Children));

        private static int CountTranslated(IEnumerable<TranslationNode> nodes) =>
            nodes.Sum(n => 
                (n.Children.Any() ? 0 : (!string.IsNullOrWhiteSpace(n.SubmittedTranslation) ? 1 : 0)) 
                + CountTranslated(n.Children));

        private async System.Threading.Tasks.Task ShowError(string message)
        {
            var dialog = new Window
            {
                Title = "Error",
                Width = 480,
                Height = 220,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(20)
                },
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            await dialog.ShowDialog(this);
        }

        private class FileTranslationData
        {
            public string FilePath { get; init; } = "";
            public string RelativePath { get; init; } = "";
            public XDocument? Doc { get; init; }
            public List<TranslationNode> RootNodes { get; init; } = new();
            public string? LoadError { get; init; }
        }

        #endregion
    }
}