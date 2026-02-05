using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace 模拟扫码枪
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(this.DataContext is MainWindowViewModel vm)
            {
                vm.SaveSettings();
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(this.DataContext is MainWindowViewModel vm)
            {
                vm.Msg = "";//清空消息
            }
        }

        private void ContentControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(sender is ContentControl cc && cc.DataContext is ScannerModel sm)
            {
                if(this.DataContext is MainWindowViewModel vm)
                {
                    vm.SelectedItem = sm;
                    vm.PropertyVisibility = Visibility.Visible;
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            { 
                vm.PropertyVisibility = Visibility.Visible;

                var obj = vm.SelectedItem;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(this.DataContext is MainWindowViewModel viewModel)
            {
                viewModel.PropertyVisibility = Visibility.Collapsed;
                //viewModel.BlurEffectRadius = 0;
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel viewModel)
            {
                if(viewModel.SelectedItem != null)
                {
                    viewModel.PropertyVisibility = Visibility.Visible;
                    //viewModel.BlurEffectRadius = 8;
                }
            }
        }

        private void Button_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}