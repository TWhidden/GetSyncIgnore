using System.Windows;
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

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            MyPopup.IsOpen = true;
        }

        private void ButtonBase1_OnClick(object sender, RoutedEventArgs e)
        {
            MyPopup.IsOpen = false;
        }
    }
}
