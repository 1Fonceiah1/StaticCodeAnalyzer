using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private readonly RefactoringEngine _refactoringEngine;
        private readonly Repository _repository;
        private List<AnalysisIssue> _currentIssues;
        private string _currentPath;
        private bool _isFolder;
        private List<FileTreeNode> _allRootNodes = new List<FileTreeNode>();
        private CollectionViewSource _resultsViewSource;

        public MainWindow()
        {
            InitializeComponent();
            _analysisService = new AnalysisService();
            _refactoringEngine = new RefactoringEngine();
            _repository = new Repository(new AppDbContext());
            _currentIssues = new List<AnalysisIssue>();
            this.Closing += MainWindow_Closing;

            _resultsViewSource = new CollectionViewSource();
            _resultsViewSource.Filter += ResultsFilter;

            Logger.Log("AppStart", "Приложение запущено");
        }

        // Открывает диалог выбора файла и загружает его в дерево
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "C# файлы (*.cs)|*.cs|Все файлы (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                _currentPath = dialog.FileName;
                _isFolder = false;
                LoadFileTree(_currentPath);
                StatusText.Text = $"Загружен файл: {Path.GetFileName(_currentPath)}";
                Logger.Log("OpenFile", $"Путь: {_currentPath}");
            }
        }

        // Открывает диалог выбора папки и загружает её содержимое в дерево
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                _currentPath = dialog.FolderName;
                _isFolder = true;
                LoadFolderTree(_currentPath);
                StatusText.Text = $"Загружена папка: {_currentPath}";
                Logger.Log("OpenFolder", $"Путь: {_currentPath}");
            }
        }

        // Загружает один файл в дерево (корневой узел)
        private void LoadFileTree(string filePath)
        {
            FilesTreeView.Items.Clear();
            _allRootNodes.Clear();
            FileTreeNode root = new FileTreeNode
            {
                Name = Path.GetFileName(filePath),
                FullPath = filePath,
                IsFolder = false,
                Children = new List<FileTreeNode>()
            };
            _allRootNodes.Add(root);
            FilesTreeView.Items.Add(root);
        }

        // Рекурсивно загружает структуру папок и файлов в дерево
        private void LoadFolderTree(string folderPath)
        {
            FilesTreeView.Items.Clear();
            _allRootNodes.Clear();
            FileTreeNode root = new FileTreeNode
            {
                Name = Path.GetFileName(folderPath),
                FullPath = folderPath,
                IsFolder = true,
                Children = new List<FileTreeNode>()
            };
            AddFilesToTree(root, folderPath);
            _allRootNodes.Add(root);
            FilesTreeView.Items.Add(root);
        }

        // Рекурсивно добавляет подпапки и .cs-файлы в узел дерева
        private void AddFilesToTree(FileTreeNode parent, string directory)
        {
            try
            {
                string[] directories = Directory.GetDirectories(directory);
                string[] files = Directory.GetFiles(directory, "*.cs");

                foreach (string dir in directories)
                {
                    FileTreeNode subNode = new FileTreeNode
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsFolder = true,
                        Children = new List<FileTreeNode>()
                    };
                    parent.Children.Add(subNode);
                    AddFilesToTree(subNode, dir);
                }

                foreach (string file in files)
                {
                    FileTreeNode fileNode = new FileTreeNode
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsFolder = false,
                        Children = new List<FileTreeNode>()
                    };
                    parent.Children.Add(fileNode);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log("AddFilesToTreeError", $"Нет доступа к {directory}: {ex.Message}");
            }
        }

        // Извлекает все файлы (узлы без папок) из списка корневых узлов
        private List<FileTreeNode> GetAllFilesFromNodes(List<FileTreeNode> nodes)
        {
            List<FileTreeNode> result = new List<FileTreeNode>();
            foreach (FileTreeNode node in nodes)
            {
                if (!node.IsFolder)
                    result.Add(node);
                if (node.Children != null)
                    result.AddRange(GetAllFilesFromNodes(node.Children));
            }
            return result;
        }

        // Запускает анализ для выбранных файлов (исключая помеченные)
        private void Analyze_Click(object sender, RoutedEventArgs e)
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
                List<FileTreeNode> allFiles = GetAllFilesFromNodes(_allRootNodes);
                List<string> filesToAnalyze = allFiles.Where(f => !f.IsExcluded).Select(f => f.FullPath).ToList();

                if (filesToAnalyze.Count == 0)
                {
                    MessageBox.Show("Нет файлов для анализа. Возможно, все файлы исключены.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                List<AnalysisIssue> issues = _analysisService.AnalyzeFiles(filesToAnalyze, null);
                _currentIssues = issues;

                if (_analysisService.Engine.LastErrors.Any())
                {
                    string errorList = string.Join("\n", _analysisService.Engine.LastErrors.Select(er => $"{er.RuleName} в {er.FilePath}: {er.ErrorMessage}"));
                    MessageBox.Show($"Некоторые правила завершились с ошибками:\n{errorList}", "Ошибки анализа", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                try
                {
                    SaveAnalysisResultsToDb(issues, filesToAnalyze, allFiles.Where(f => f.IsExcluded).ToList(), _currentPath, _isFolder);
                }
                catch (Exception dbEx)
                {
                    MessageBox.Show($"Ошибка сохранения в БД: {dbEx.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _resultsViewSource.Source = _currentIssues;
                ResultsGrid.ItemsSource = _resultsViewSource.View;
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

        // Открывает окно вставки кода для анализа
        private void PasteCode_Click(object sender, RoutedEventArgs e)
        {
            CodeInputWindow inputWindow = new CodeInputWindow();
            inputWindow.Owner = this;
            inputWindow.ShowDialog();
            if (!inputWindow.IsAnalyze || string.IsNullOrWhiteSpace(inputWindow.EnteredCode))
                return;
            CodeEditorWindow editor = new CodeEditorWindow(null, inputWindow.EnteredCode);
            editor.Owner = this;
            editor.ShowDialog();
        }

        // Сохраняет результаты анализа в базу данных
        private void SaveAnalysisResultsToDb(List<AnalysisIssue> issues, List<string> filesToAnalyze, List<FileTreeNode> excludedFiles, string path, bool isFolder)
        {
            try
            {
                Logger.Log("DbSave", "Начало сохранения в БД");

                Project project = _repository.GetOrCreateProject(path, isFolder ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path));
                Logger.Log("DbSave", $"Проект получен/создан: ID={project.ProjectId}, Name={project.ProjectName}");

                // Формируем JSON с параметрами сканирования
                string parametersJson = $"{{\"analysisType\":\"{(isFolder ? "folder" : "file")}\",\"totalFiles\":{filesToAnalyze.Count},\"excludedFiles\":{excludedFiles.Count},\"timestamp\":\"{DateTime.UtcNow:O}\"}}";
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "analysis.log");

                Scan scan = new Scan
                {
                    ProjectId = project.ProjectId,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    UserName = Environment.UserName,
                    Parameters = parametersJson,
                    ExternalLogPath = logPath,
                    TotalFilesScanned = filesToAnalyze.Count,
                    TotalIssuesFound = issues.Count
                };
                _repository.AddScan(scan);
                Logger.Log("DbSave", $"Scan добавлен в контекст: ProjectId={scan.ProjectId}, ScanId={scan.ScanId}");

                // Обновляем LastScanned у проекта
                _repository.UpdateProjectLastScanned(project.ProjectId);
                Logger.Log("DbSave", "Обновлен LastScanned у проекта");

                // Сохраняем отсканированные файлы
                Dictionary<string, int> fileIssuesCount = issues.GroupBy(i => i.FilePath).ToDictionary(g => g.Key, g => g.Count());
                List<ScannedFile> scannedFiles = filesToAnalyze.Select(filePath => new ScannedFile
                {
                    ScanId = scan.ScanId,
                    FilePath = filePath,
                    FileExtension = Path.GetExtension(filePath),
                    LinesCount = File.Exists(filePath) ? File.ReadAllLines(filePath).Length : 0,
                    IssuesCount = fileIssuesCount.ContainsKey(filePath) ? fileIssuesCount[filePath] : 0
                }).ToList();
                _repository.AddScannedFiles(scannedFiles);
                Logger.Log("DbSave", $"Добавлено {scannedFiles.Count} отсканированных файлов");

                // Сохраняем уникальные правила из issues (по коду правила)
                Dictionary<string, AnalysisRule> rulesCache = new Dictionary<string, AnalysisRule>();
                foreach (AnalysisIssue issue in issues.Where(i => !string.IsNullOrEmpty(i.Code)).DistinctBy(i => i.Code))
                {
                    AnalysisRule rule = _repository.GetOrCreateRule(issue.Code, issue.RuleName ?? "Unknown", "General", issue.Severity, issue.Description ?? $"Rule for {issue.Code}");
                    rulesCache[issue.Code] = rule;
                }
                Logger.Log("DbSave", $"Сохранено {rulesCache.Count} правил");

                // Сохраняем результаты анализа с привязкой к правилам (по коду)
                List<AnalysisResult> results = issues.Select(i => new AnalysisResult
                {
                    ScanId = scan.ScanId,
                    ProjectId = project.ProjectId,
                    RuleId = !string.IsNullOrEmpty(i.Code) && rulesCache.ContainsKey(i.Code) ? rulesCache[i.Code].RuleId : null,
                    FilePath = i.FilePath,
                    LineNumber = i.LineNumber,
                    ColumnNumber = i.ColumnNumber,
                    IssueType = i.Type,
                    IssueSeverity = i.Severity,
                    IssueCode = i.Code,
                    IssueDescription = i.Description,
                    SuggestedFix = i.Suggestion,
                    CreatedAt = DateTime.UtcNow
                }).ToList();
                _repository.AddAnalysisResults(results);
                Logger.Log("DbSave", $"Добавлено {results.Count} результатов в контекст");

                // Сохраняем исключенные файлы/папки
                if (excludedFiles.Any())
                {
                    List<AnalysisExclusion> exclusions = excludedFiles.Select(f => new AnalysisExclusion
                    {
                        ProjectId = project.ProjectId,
                        ExclusionPattern = f.FullPath,
                        ExclusionType = f.IsFolder ? "Folder" : "File",
                        CreatedAt = DateTime.UtcNow
                    }).ToList();
                    _repository.AddExclusions(exclusions);
                    Logger.Log("DbSave", $"Добавлено {exclusions.Count} исключений ({exclusions.Count(e => e.ExclusionType == "Folder")} папок, {exclusions.Count(e => e.ExclusionType == "File")} файлов)");
                }

                _repository.SaveChanges();
                Logger.Log("DbSave", "SaveChanges выполнен успешно");

                StatusText.Text = $"Сохранено в БД: проект {project.ProjectName}, скан #{scan.ScanId}, {results.Count} проблем, {scannedFiles.Count} файлов, {rulesCache.Count} правил";
            }
            catch (Exception ex)
            {
                string errorMsg = $"Ошибка сохранения в БД: {ex.Message}";
                string innerMsg = ex.InnerException != null ? $"\n\nInner: {ex.InnerException.Message}" : "";
                Logger.Log("DbSaveError", errorMsg + innerMsg);
                MessageBox.Show(errorMsg + innerMsg + "\n\n" + ex.StackTrace, "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        // Очищает результаты анализа и дерево файлов
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

        // Скрывает или показывает левую панель с деревом
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

        // Открывает файл в редакторе при двойном щелчке по узлу дерева
        private void FilesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FileTreeNode item = FilesTreeView.SelectedItem as FileTreeNode;
            if (item != null && !item.IsFolder && File.Exists(item.FullPath))
            {
                string code = File.ReadAllText(item.FullPath);
                CodeEditorWindow editor = new CodeEditorWindow(item.FullPath, code);
                editor.Owner = this;
                editor.ShowDialog();
            }
        }

        // Открывает редактор кода с переходом на строку проблемы при двойном щелчке по строке результата
        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AnalysisIssue issue = ResultsGrid.SelectedItem as AnalysisIssue;
            if (issue == null || string.IsNullOrEmpty(issue.FilePath) || !File.Exists(issue.FilePath))
                return;

            string code = File.ReadAllText(issue.FilePath);
            CodeEditorWindow editor = new CodeEditorWindow(issue.FilePath, code, issue.LineNumber);
            editor.Owner = this;
            editor.ShowDialog();
        }

        // Применяет фильтры при изменении параметров фильтрации
        private void Filter_Changed(object sender, EventArgs e)
        {
            _resultsViewSource?.View?.Refresh();
        }

        // Сбрасывает все фильтры
        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            FilterSeverity.SelectedIndex = 0;
            FilterCode.Text = "";
            FilterFile.Text = "";
        }

        // Логика фильтрации результатов
        private void ResultsFilter(object sender, FilterEventArgs e)
        {
            AnalysisIssue issue = e.Item as AnalysisIssue;
            if (issue == null)
            {
                e.Accepted = false;
                return;
            }

            ComboBoxItem severityItem = FilterSeverity.SelectedItem as ComboBoxItem;
            if (severityItem != null && severityItem.Content.ToString() != "Все")
            {
                if (issue.Severity != severityItem.Content.ToString())
                {
                    e.Accepted = false;
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(FilterCode.Text))
            {
                if (!issue.Code.Contains(FilterCode.Text, StringComparison.OrdinalIgnoreCase))
                {
                    e.Accepted = false;
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(FilterFile.Text))
            {
                if (!issue.FilePath.Contains(FilterFile.Text, StringComparison.OrdinalIgnoreCase))
                {
                    e.Accepted = false;
                    return;
                }
            }

            e.Accepted = true;
        }

        // Отменяет анализ и освобождает ресурсы при закрытии окна
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _repository.Dispose();
        }
    }
}