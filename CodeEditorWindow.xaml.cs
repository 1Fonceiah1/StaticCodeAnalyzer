using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
                await LoadIssuesAsync();
                if (_goToLine.HasValue && _goToLine.Value > 0)
                {
                    var lineIndex = _goToLine.Value - 1;
                    if (lineIndex >= 0)
                    {
                        CodeEditor.Focus();
                        CodeEditor.CaretIndex = CodeEditor.GetCharacterIndexFromLineIndex(lineIndex);
                        CodeEditor.ScrollToLine(lineIndex);
                    }
                }
            };
        }

        private async Task LoadIssuesAsync()
        {
            try
            {
                string currentCode = CodeEditor.Text;
                string tempFile = Path.GetTempFileName() + ".cs";
                await File.WriteAllTextAsync(tempFile, currentCode);
                var issues = await _analysisService.AnalyzeFiles(new List<string> { tempFile });
                IssuesListBox.ItemsSource = issues;

                var fixableCodes = _refactoringEngine.GetFixableIssueCodes();
                var applicableRules = issues
                    .Where(i => fixableCodes.Contains(i.Code))
                    .Select(i => new RuleSelection { Name = $"{i.Code} – {i.RuleName}", Code = i.Code })
                    .Distinct()
                    .ToList();
                foreach (var rule in applicableRules)
                {
                    rule.IsSelected = true;
                }
                RulesListBox.ItemsSource = applicableRules;
                RefactorButton.IsEnabled = applicableRules.Any();

                File.Delete(tempFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                RefactorButton.IsEnabled = false;
            }
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadIssuesAsync();
        }

        private async void RefactorButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRules = RulesListBox.ItemsSource as IEnumerable<RuleSelection>;
            if (selectedRules == null) return;

            var allowedCodes = selectedRules.Where(r => r.IsSelected).Select(r => r.Code).ToHashSet();
            if (!allowedCodes.Any())
            {
                MessageBox.Show("Не выбрано ни одного правила для рефакторинга.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string currentCode = CodeEditor.Text;
            try
            {
                var result = await _refactoringEngine.ApplyRefactoringWithRollbackAsync(currentCode, allowedCodes);
                if (result.Success && result.NewCode != currentCode)
                {
                    CodeEditor.Text = result.NewCode;
                    _isDirty = true;
                    SaveButton.IsEnabled = true;
                    await LoadIssuesAsync();
                }
                else if (!result.Success)
                {
                    MessageBox.Show($"Рефакторинг не выполнен из-за ошибок:\n{string.Join("\n", result.Errors)}", 
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

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newCode = CodeEditor.Text;
            string savePath = _filePath;
            if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
            {
                var dialog = new SaveFileDialog();
                dialog.Filter = "C# files (*.cs)|*.cs";
                if (dialog.ShowDialog() == true)
                {
                    savePath = dialog.FileName;
                }
                else return;
            }
            await File.WriteAllTextAsync(savePath, newCode);
            _originalCode = newCode;
            _isDirty = false;
            SaveButton.IsEnabled = false;
            MessageBox.Show("Файл сохранён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("Имеются несохранённые изменения. Сохранить?", "Предупреждение", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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

        private class RuleSelection
        {
            public string Name { get; set; }
            public string Code { get; set; }
            public bool IsSelected { get; set; }
        }
    }
}