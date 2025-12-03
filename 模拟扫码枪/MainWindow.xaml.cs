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
    }
}