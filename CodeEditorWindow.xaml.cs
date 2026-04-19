using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using StaticCodeAnalyzer.Analysis;
using StaticCodeAnalyzer.Models;
using StaticCodeAnalyzer.Services;

namespace StaticCodeAnalyzer
{
    public partial class CodeEditorWindow : Window
    {
        private readonly AnalysisService _analysisService;
        private readonly RefactoringEngine _refactoringEngine;
        private string _originalCode;
        private string _filePath;
        private bool _isDirty;
        private int? _goToLine;

        public CodeEditorWindow(string filePath, string code, int? goToLine = null)
        {
            InitializeComponent();
            _analysisService = new AnalysisService();
            _refactoringEngine = new RefactoringEngine();
            _filePath = filePath;
            _originalCode = code;
            CodeEditor.Text = code;
            _isDirty = false;
            _goToLine = goToLine;
            CodeEditor.TextChanged += (s, e) => { _isDirty = true; SaveButton.IsEnabled = true; };
            this.Loaded += async (s, e) =>
            {
                if (_goToLine.HasValue && _goToLine.Value > 0)
                {
                    int lineIndex = _goToLine.Value - 1;
                    if (lineIndex >= 0)
                    {
                        CodeEditor.Focus();
                        CodeEditor.CaretIndex = CodeEditor.GetCharacterIndexFromLineIndex(lineIndex);
                        CodeEditor.ScrollToLine(lineIndex);
                    }
                }
                await LoadIssuesAsync();
            };
        }

        // Загружает проблемы анализа для текущего кода и обновляет список доступных рефакторингов
        private async Task LoadIssuesAsync()
        {
            try
            {
                string currentCode = CodeEditor.Text;
                string tempFile = Path.GetTempFileName() + ".cs";
                File.WriteAllText(tempFile, currentCode);

                // Run analysis on background thread to prevent UI blocking
                List<AnalysisIssue> issues = await Task.Run(() =>
                    _analysisService.AnalyzeFiles(new List<string> { tempFile }));

                // Update UI on dispatcher thread
                await Dispatcher.InvokeAsync(() =>
                {
                    IssuesListBox.ItemsSource = issues;

                    HashSet<string> fixableCodes = _refactoringEngine.GetFixableIssueCodes();
                    List<RuleSelection> applicableRules = issues
                        .Where(i => fixableCodes.Contains(i.Code))
                        .Select(i => new RuleSelection { Name = $"{i.Code} – {i.RuleName}", Code = i.Code })
                        .Distinct()
                        .ToList();
                    foreach (RuleSelection rule in applicableRules)
                    {
                        rule.IsSelected = true;
                    }
                    RulesListBox.ItemsSource = applicableRules;
                    RefactorButton.IsEnabled = applicableRules.Any();
                });

                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка при анализе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    RefactorButton.IsEnabled = false;
                });
            }
        }

        // Обработчик кнопки "Анализ" – повторно загружает проблемы
        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadIssuesAsync();
        }

        // Применяет выбранные правила рефакторинга к текущему коду
        private async void RefactorButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<RuleSelection> selectedRules = RulesListBox.ItemsSource as IEnumerable<RuleSelection>;
            if (selectedRules == null) return;

            HashSet<string> allowedCodes = selectedRules.Where(r => r.IsSelected).Select(r => r.Code).ToHashSet();
            if (!allowedCodes.Any())
            {
                MessageBox.Show("Не выбрано ни одного правила для рефакторинга.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string currentCode = CodeEditor.Text;
            try
            {
                (string newCode, bool success, List<string> errors) = _refactoringEngine.ApplyRefactoringWithRollback(currentCode, allowedCodes);
                if (success && newCode != currentCode)
                {
                    CodeEditor.Text = newCode;
                    _isDirty = true;
                    SaveButton.IsEnabled = true;
                    await LoadIssuesAsync();
                }
                else if (!success)
                {
                    MessageBox.Show($"Рефакторинг не выполнен из-за ошибок:\n{string.Join("\n", errors)}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Выбранные рефакторинги не применимы или код не изменился.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при рефакторинге: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Сохраняет текущий код в файл
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newCode = CodeEditor.Text;
            string savePath = _filePath;
            if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "C# files (*.cs)|*.cs";
                if (dialog.ShowDialog() == true)
                {
                    savePath = dialog.FileName;
                }
                else return;
            }
            File.WriteAllText(savePath, newCode);
            _originalCode = newCode;
            _isDirty = false;
            SaveButton.IsEnabled = false;
            MessageBox.Show("Файл сохранён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Закрывает окно, при необходимости предлагает сохранить изменения
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                MessageBoxResult result = MessageBox.Show("Имеются несохранённые изменения. Сохранить?", "Предупреждение", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    SaveButton_Click(null, null);
                }
                else if (result == MessageBoxResult.Cancel)
                    return;
            }
            Close();
        }

        private void IssuesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Можно добавить подсветку в редакторе (опционально)
        }

        // Вспомогательный класс для представления правила в списке выбора
        private class RuleSelection
        {
            public string Name { get; set; }
            public string Code { get; set; }
            public bool IsSelected { get; set; }
        }
    }
}