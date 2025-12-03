using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Windows;

namespace 模拟扫码枪
{
    public partial class MainWindowViewModel:ObservableObject
    {
        public ScannerModel[] Scanners { get; set; } = new ScannerModel[4];

        [ObservableProperty]
        public string msg  ;

        public InterfaceType[] DeviceTypeValues => Enum.GetValues(typeof(InterfaceType)).Cast<InterfaceType>().ToArray();
        public Parity[] ParityValues => Enum.GetValues(typeof(Parity)).Cast<Parity>().ToArray();
        public StopBits[] StopBitsValues => Enum.GetValues(typeof(StopBits)).Cast<StopBits>().ToArray();
        public string[] PortNames => SerialPort.GetPortNames();

        public MainWindowViewModel()
        {
            if(File.Exists(settingFileName))
            {
                var obj=JsonSerializer.Deserialize<ScannerModel[]>(File.ReadAllText(settingFileName));
                if(obj!=null&&obj.Length==Scanners.Length)
                {
                    for (int i = 0; i < Scanners.Length; i++)
                    {
                        Scanners[i]= obj[i] ;
                    } 
                }
            }
            for (int i = 0; i < Scanners.Length; i++)
            { 
                Scanners[i].UpdateMessageEvent += AddMessage;
            }
        }

        void AddMessage(string s)
        {
            Application.Current?.Dispatcher?.Invoke(() => {
                Msg = $"[{DateTime.Now:HH:mm:ss}] {s}{Environment.NewLine}" + Msg;
                if (Msg.Length >= 10_000) Msg = Msg.Substring(0,10_000);
            });
        }

        string settingFileName = "Settings.json";
        ~MainWindowViewModel()
        {
            var str=JsonSerializer.Serialize(Scanners,new JsonSerializerOptions() {WriteIndented=true });
        }

        public void SaveSettings()
        {
            var str = JsonSerializer.Serialize(Scanners, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(settingFileName, str);
        }
    }
}
