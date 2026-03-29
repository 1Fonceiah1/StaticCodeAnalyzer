using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StaticCodeAnalyzer.Models
{
    public class FileTreeNode : INotifyPropertyChanged
    {
        private bool _isExcluded;
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFolder { get; set; }
        public List<FileTreeNode> Children { get; set; }

        public bool IsExcluded
        {
            get => _isExcluded;
            set
            {
                if (_isExcluded != value)
                {
                    _isExcluded = value;
                    OnPropertyChanged();
                    // Если это папка, синхронизируем с дочерними
                    if (IsFolder && Children != null)
                    {
                        foreach (var child in Children)
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