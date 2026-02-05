using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace 模拟扫码枪
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        double blurEffectRadius = 0;

        [ObservableProperty]
        ScannerModel selectedItem;

        [ObservableProperty]
        Visibility propertyVisibility = Visibility.Collapsed;

        [ObservableProperty]
        BindingList<ScannerModel> scanners = new();

        [ObservableProperty]
        public string msg;

        //public InterfaceType[] DeviceTypeValues => Enum.GetValues(typeof(InterfaceType)).Cast<InterfaceType>().ToArray();
        public Parity[] ParityValues => Enum.GetValues(typeof(Parity)).Cast<Parity>().ToArray();
        public StopBits[] StopBitsValues => Enum.GetValues(typeof(StopBits)).Cast<StopBits>().ToArray();
        public string[] PortNames => SerialPort.GetPortNames();


        public MainWindowViewModel()
        {
            try
            {
                if (File.Exists(settingFileName))
                {
                    Scanners = JsonSerializer.Deserialize<BindingList<ScannerModel>>(File.ReadAllText(settingFileName)); 
                }
                for (int i = 0; i < Scanners.Count; i++)
                {
                    if (Scanners[i] == null) throw new Exception();
                    Scanners[i].UpdateMessageEvent += AddMessage;
                }
            }
            catch (Exception)
            {
                Scanners = new BindingList<ScannerModel>();
                for (int i = 0; i < 6; i++)
                {
                    var Scanner = new ScannerModel();
                    Scanner.UpdateMessageEvent += AddMessage;
                    Scanner.PortName = "COM" + i * 2 + 2;
                    Scanner.Port = 8080 + i;
                    Scanners.Add(Scanner);  
                }
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if(e.PropertyName == nameof(PropertyVisibility))
            {
                if (PropertyVisibility == Visibility.Visible)
                {
                    BlurEffectRadius = 5;
                }
                else
                {
                    BlurEffectRadius = 0;
                }
            }
        } 

        void AddMessage(string s)
        {
            Foo1(s);
        }
         
        void Foo1(string s)
        { 
            string str = $"{DateTime.Now.ToString("HH:mm:ss")} {s}{Environment.NewLine}";
            Application.Current?.Dispatcher?.Invoke(() => {
                Msg = str + Msg ;
                if (Msg.Length > 2000) Msg = Msg.Substring(0, 1000);//限制最大长度，避免内存占用过高
            });
        } 

        string settingFileName = "Settings.json";
        ~MainWindowViewModel()
        {
            var str = JsonSerializer.Serialize(Scanners, new JsonSerializerOptions() { WriteIndented = true });
        }
         
        public void SaveSettings()
        {
            var str = JsonSerializer.Serialize(Scanners, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(settingFileName, str);
        }

        [RelayCommand]
        void Close()
        {
            PropertyVisibility = Visibility.Collapsed;
            //BlurEffectRadius = 0;
        }

        [RelayCommand]
        void AddItem()
        {
            var scanner = new ScannerModel();
            scanner.UpdateMessageEvent += AddMessage;
            Scanners.Add(new ScannerModel());
        }

        [RelayCommand]
        void RemoveItem()
        {
            if (SelectedItem != null && Scanners.Contains(SelectedItem))
            {
                if (System.Windows.MessageBox.Show($"是否删除设备：[{SelectedItem.Name}]","提示",MessageBoxButton.YesNo,MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                SelectedItem.IsEnable = false;
                var obj = SelectedItem;
                SelectedItem.UpdateMessageEvent -= AddMessage;
                Scanners.Remove(SelectedItem);
                PropertyVisibility = Visibility.Collapsed;
                obj.Dispose();
            }
        }

        [RelayCommand]
        void SaveList()
        {
            try
            {
                SaveSettings();
                System.Windows.MessageBox.Show("保存成功！");
            }
            catch (Exception ex)
            {
                 System.Windows.MessageBox.Show("保存失败！" + ex.Message);
            }
        }
    }
}
