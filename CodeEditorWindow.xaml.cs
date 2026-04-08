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

        public CodeEditorWindow(string filePath, string code)
        {
            InitializeComponent();
            _analysisService = new AnalysisService();
            _refactoringEngine = new RefactoringEngine();
            _filePath = filePath;
            _originalCode = code;
            CodeEditor.Text = code;
            _isDirty = false;
            CodeEditor.TextChanged += (s, e) => { _isDirty = true; SaveButton.IsEnabled = true; };
            _ = LoadIssuesAsync();
        }

        private async Task<List<AnalysisIssue>> LoadIssuesAsync()
        {
            try
            {
                string currentCode = CodeEditor.Text;
                string tempFile = Path.GetTempFileName() + ".cs";
                await File.WriteAllTextAsync(tempFile, currentCode);
                var issues = await _analysisService.AnalyzeFiles(new List<string> { tempFile });
                IssuesListBox.ItemsSource = issues;
                // Определяем, есть ли исправимые ошибки
                var fixableCodes = _refactoringEngine.GetFixableIssueCodes();
                bool hasFixable = issues.Any(i => fixableCodes.Contains(i.Code));
                RefactorButton.IsEnabled = hasFixable;
                File.Delete(tempFile);
                return issues;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                RefactorButton.IsEnabled = false;
                return new List<AnalysisIssue>();
            }
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadIssuesAsync();
        }

        private async void RefactorButton_Click(object sender, RoutedEventArgs e)
        {
            string currentCode = CodeEditor.Text;
            try
            {
                string refactoredCode = await _refactoringEngine.ApplyRefactoringAsync(currentCode);
                if (refactoredCode != currentCode)
                {
                    CodeEditor.Text = refactoredCode;
                    _isDirty = true;
                    SaveButton.IsEnabled = true;
                    await LoadIssuesAsync(); // обновляем ошибки после рефакторинга
                }
                else
                {
                    MessageBox.Show("Рефакторинг не применим или код не изменился.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}