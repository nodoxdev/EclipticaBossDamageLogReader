using System.Reflection;
using System.Windows;

namespace BossDamageLogger
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version;

            Title = $"EVRC Boss Log Reader v{version?.Major}.{version?.Minor}";
        }
    }
}
