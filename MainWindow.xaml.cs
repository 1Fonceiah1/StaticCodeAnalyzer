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

        public MainWindow()
        {
            InitializeComponent();
            _analysisService = new AnalysisService();
            _repository = new Repository(new AppDbContext());
            _currentIssues = new List<AnalysisIssue>();
            this.Closing += MainWindow_Closing;
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
            }
        }

        private async Task LoadFileTreeAsync(string filePath)
        {
            FilesTreeView.Items.Clear();
            var root = new TreeViewItem { Header = Path.GetFileName(filePath), Tag = filePath };
            root.Items.Add(new TreeViewItem { Header = "(одиночный файл)" });
            FilesTreeView.Items.Add(root);
        }

        private async Task LoadFolderTreeAsync(string folderPath)
        {
            FilesTreeView.Items.Clear();
            var root = new TreeViewItem { Header = Path.GetFileName(folderPath), Tag = folderPath };
            await AddFilesToTree(root, folderPath);
            FilesTreeView.Items.Add(root);
        }

        private async Task AddFilesToTree(TreeViewItem parent, string directory)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    var subItem = new TreeViewItem { Header = Path.GetFileName(dir), Tag = dir };
                    parent.Items.Add(subItem);
                    await AddFilesToTree(subItem, dir);
                }
                foreach (var file in Directory.GetFiles(directory, "*.cs"))
                {
                    var fileItem = new TreeViewItem { Header = Path.GetFileName(file), Tag = file };
                    parent.Items.Add(fileItem);
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        private async void Analyze_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                MessageBox.Show("Сначала откройте файл или папку.", "Нет данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Анализ...";
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                List<string> filesToAnalyze = new List<string>();
                if (_isFolder)
                {
                    filesToAnalyze.AddRange(Directory.GetFiles(_currentPath, "*.cs", SearchOption.AllDirectories));
                }
                else
                {
                    filesToAnalyze.Add(_currentPath);
                }

                var issues = await Task.Run(() => _analysisService.AnalyzeFiles(filesToAnalyze));
                _currentIssues = issues;

                await SaveAnalysisResultsToDbAsync(issues, _currentPath, _isFolder);

                ResultsGrid.ItemsSource = _currentIssues;
                StatusText.Text = $"Анализ завершён. Найдено {issues.Count} проблем.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Анализ не удался.";
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

            StatusText.Text = "Анализ вставленного кода...";
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                string tempFile = Path.GetTempFileName() + ".cs";
                await File.WriteAllTextAsync(tempFile, inputWindow.EnteredCode);

                var issues = await Task.Run(() => _analysisService.AnalyzeFiles(new List<string> { tempFile }));
                _currentIssues = issues;

                await SaveAnalysisResultsToDbAsync(issues, tempFile, false);

                ResultsGrid.ItemsSource = _currentIssues;
                StatusText.Text = $"Анализ завершён. Найдено {issues.Count} проблем.";

                try { File.Delete(tempFile); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Анализ не удался.";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
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
            _currentPath = null;
            _isFolder = false;
            StatusText.Text = "Очищено.";
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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _repository.Dispose();
        }
    }
}