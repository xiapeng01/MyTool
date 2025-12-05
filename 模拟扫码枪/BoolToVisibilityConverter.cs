using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace 模拟扫码枪
{
    public partial class BoolToVisibilityConverter:ConverterBase
    { 
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        { 
            if (value is bool b1) return b1 ?Visibility.Visible:Visibility.Collapsed;
            return Visibility.Hidden;
        }


    }
}
