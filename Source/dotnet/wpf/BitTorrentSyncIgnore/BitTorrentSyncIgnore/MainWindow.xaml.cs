using FirstFloor.ModernUI.Windows.Controls;

namespace BitTorrentSyncIgnore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : ModernWindow
    {
        public MainWindow()
        {
            
            InitializeComponent();
            
        }

        public override void OnApplyTemplate()
        {
            this.DataContext = new MainWindowViewModel();
            base.OnApplyTemplate();
        }
    }
}
