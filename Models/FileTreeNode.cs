using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StaticCodeAnalyzer.Models
{
    // Модель узла дерева файлов для отображения в интерфейсе
    public class FileTreeNode : INotifyPropertyChanged
    {
        private bool _isExcluded;

        public string Name { get; set; }               // Имя файла или папки
        public string FullPath { get; set; }           // Полный путь
        public bool IsFolder { get; set; }             // Является ли папкой
        public List<FileTreeNode> Children { get; set; } // Дочерние узлы

        // Флаг исключения из анализа (синхронизируется с дочерними элементами)
        public bool IsExcluded
        {
            get => _isExcluded;
            set
            {
                if (_isExcluded != value)
                {
                    _isExcluded = value;
                    OnPropertyChanged();
                    if (IsFolder && Children != null)
                    {
                        foreach (FileTreeNode child in Children)
                        {
                            child.IsExcluded = value;
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}