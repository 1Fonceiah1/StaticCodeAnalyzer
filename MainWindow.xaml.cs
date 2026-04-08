using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Data;
using StaticCodeAnalyzer.Models;
using StaticCodeAnalyzer.Services;

namespace StaticCodeAnalyzer
{
    public partial class MainWindow : Window
    {
        private readonly AnalysisService _analysisService;
        private readonly Repository _repository;
        private List<AnalysisIssue> _currentIssues;
        private string _currentPath;
        private bool _isFolder;
        private List<FileTreeNode> _allRootNodes = new List<FileTreeNode>();

        public MainWindow()
        {
            InitializeComponent();
            _analysisService = new AnalysisService();
            _repository = new Repository(new AppDbContext());
            _currentIssues = new List<AnalysisIssue>();
            this.Closing += MainWindow_Closing;

            Logger.Log("AppStart", "Приложение запущено");
        }

        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                _currentPath = dialog.FileName;
                _isFolder = false;
                await LoadFileTreeAsync(_currentPath);
                StatusText.Text = $"Загружен файл: {Path.GetFileName(_currentPath)}";
                Logger.Log("OpenFile", $"Путь: {_currentPath}");
            }
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                _currentPath = dialog.FolderName;
                _isFolder = true;
                await LoadFolderTreeAsync(_currentPath);
                StatusText.Text = $"Загружена папка: {_currentPath}";
                Logger.Log("OpenFolder", $"Путь: {_currentPath}");
            }
        }

        private async Task LoadFileTreeAsync(string filePath)
        {
            FilesTreeView.Items.Clear();
            _allRootNodes.Clear();
            var root = new FileTreeNode
            {
                Name = Path.GetFileName(filePath),
                FullPath = filePath,
                IsFolder = false,
                Children = new List<FileTreeNode>()
            };
            _allRootNodes.Add(root);
            FilesTreeView.Items.Add(root);
        }

        private async Task LoadFolderTreeAsync(string folderPath)
        {
            FilesTreeView.Items.Clear();
            _allRootNodes.Clear();
            var root = new FileTreeNode
            {
                Name = Path.GetFileName(folderPath),
                FullPath = folderPath,
                IsFolder = true,
                Children = new List<FileTreeNode>()
            };
            await AddFilesToTree(root, folderPath);
            _allRootNodes.Add(root);
            FilesTreeView.Items.Add(root);
        }

        private async Task AddFilesToTree(FileTreeNode parent, string directory)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    var subNode = new FileTreeNode
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsFolder = true,
                        Children = new List<FileTreeNode>()
                    };
                    parent.Children.Add(subNode);
                    await AddFilesToTree(subNode, dir);
                }
                foreach (var file in Directory.GetFiles(directory, "*.cs"))
                {
                    var fileNode = new FileTreeNode
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsFolder = false,
                        Children = new List<FileTreeNode>()
                    };
                    parent.Children.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        private List<FileTreeNode> GetAllFilesFromNodes(List<FileTreeNode> nodes)
        {
            var result = new List<FileTreeNode>();
            foreach (var node in nodes)
            {
                if (!node.IsFolder)
                    result.Add(node);
                if (node.Children != null)
                    result.AddRange(GetAllFilesFromNodes(node.Children));
            }
            return result;
        }

        private async void Analyze_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath) && _allRootNodes.Count == 0)
            {
                MessageBox.Show("Сначала откройте файл или папку.", "Нет данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Анализ...";
            Mouse.OverrideCursor = Cursors.Wait;
            Logger.Log("AnalyzeStart", $"Объект: {_currentPath}");

            try
            {
                var allFiles = GetAllFilesFromNodes(_allRootNodes);
                var filesToAnalyze = allFiles.Where(f => !f.IsExcluded).Select(f => f.FullPath).ToList();

                if (filesToAnalyze.Count == 0)
                {
                    MessageBox.Show("Нет файлов для анализа. Возможно, все файлы исключены.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var issues = await Task.Run(() => _analysisService.AnalyzeFiles(filesToAnalyze));
                _currentIssues = issues;

                await SaveAnalysisResultsToDbAsync(issues, _currentPath, _isFolder);

                ResultsGrid.ItemsSource = _currentIssues;
                StatusText.Text = $"Анализ завершён. Найдено {issues.Count} проблем.";
                Logger.Log("AnalyzeEnd", $"Найдено проблем: {issues.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Анализ не удался.";
                Logger.Log("AnalyzeError", ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void PasteCode_Click(object sender, RoutedEventArgs e)
        {
            var inputWindow = new CodeInputWindow();
            inputWindow.Owner = this;
            inputWindow.ShowDialog();
            if (!inputWindow.IsAnalyze || string.IsNullOrWhiteSpace(inputWindow.EnteredCode))
                return;
            var editor = new CodeEditorWindow(null, inputWindow.EnteredCode);
            editor.Owner = this;
            editor.ShowDialog();
        }

        private async Task SaveAnalysisResultsToDbAsync(List<AnalysisIssue> issues, string path, bool isFolder)
        {
            try
            {
                var project = await _repository.GetOrCreateProjectAsync(path, isFolder ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path));
                var scan = new Scan
                {
                    ProjectId = project.ProjectId,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                    UserName = Environment.UserName,
                    TotalFilesScanned = issues.Select(i => i.FilePath).Distinct().Count(),
                    TotalIssuesFound = issues.Count
                };
                await _repository.AddScanAsync(scan);

                var results = issues.Select(i => new AnalysisResult
                {
                    ScanId = scan.ScanId,
                    ProjectId = project.ProjectId,
                    RuleId = null,
                    FilePath = i.FilePath,
                    LineNumber = i.LineNumber,
                    ColumnNumber = i.ColumnNumber,
                    IssueType = i.Type,
                    IssueSeverity = i.Severity,
                    IssueCode = i.Code,
                    IssueDescription = i.Description,
                    SuggestedFix = i.Suggestion
                }).ToList();

                await _repository.AddAnalysisResultsAsync(results);
                await _repository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения в БД: {ex.Message}");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ResultsGrid.ItemsSource = null;
            _currentIssues.Clear();
            FilesTreeView.Items.Clear();
            _allRootNodes.Clear();
            _currentPath = null;
            _isFolder = false;
            StatusText.Text = "Очищено.";
            Logger.Log("Clear", "Очистка результатов и дерева");
        }

        private void ToggleTreePanel_Checked(object sender, RoutedEventArgs e)
        {
            TreeColumn.Width = new GridLength(0);
            ToggleTreePanel.Content = "Показать дерево";
        }

        private void ToggleTreePanel_Unchecked(object sender, RoutedEventArgs e)
        {
            TreeColumn.Width = new GridLength(250);
            ToggleTreePanel.Content = "Скрыть дерево";
        }

        private void FilesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = FilesTreeView.SelectedItem as FileTreeNode;
            if (item != null && !item.IsFolder && File.Exists(item.FullPath))
            {
                string code = File.ReadAllText(item.FullPath);
                var editor = new CodeEditorWindow(item.FullPath, code);
                editor.Owner = this;
                editor.ShowDialog();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _repository.Dispose();
        }
    }
}