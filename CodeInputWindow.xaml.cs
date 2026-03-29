using System.Windows;

namespace StaticCodeAnalyzer
{
    public partial class CodeInputWindow : Window
    {
        public string EnteredCode { get; private set; }
        public bool IsAnalyze { get; private set; }

        public CodeInputWindow()
        {
            InitializeComponent();
            IsAnalyze = false;
        }

        private void Analyze_Click(object sender, RoutedEventArgs e)
        {
            EnteredCode = CodeTextBox.Text;
            IsAnalyze = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsAnalyze = false;
            Close();
        }
    }
}