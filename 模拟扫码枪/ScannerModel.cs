using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Text.Json.Serialization;
using System.Windows;
using System.Net;
using System.ComponentModel;
namespace 模拟扫码枪
{
    public enum InterfaceType { 串口, 以太网 }
    public partial class ScannerModel : ObservableObject,IService
    {
        public event Action<string>? MessageEvent;

        [JsonIgnore]
        public InterfaceType[] DeviceTypeValues => Enum.GetValues(typeof(InterfaceType)).Cast<InterfaceType>().ToArray();
        [JsonIgnore]
        public Parity[] ParityValues => Enum.GetValues(typeof(Parity)).Cast<Parity>().ToArray();
        [JsonIgnore]
        public StopBits[] StopBitsValues => Enum.GetValues(typeof(StopBits)).Cast<StopBits>().ToArray();
        [JsonIgnore]
        public string[] PortNames => SerialPort.GetPortNames();

        [ObservableProperty]
        bool isEnableServer = false;//启用虚拟服务器

        [ObservableProperty]
        bool isUseTriggerString = false;//使用触发字符串

        [ObservableProperty]
        bool isAppendOrderIndex = false;//是否自动附加流水号

        [ObservableProperty]
        InterfaceType deviceType = InterfaceType.以太网;

        [ObservableProperty]
        string portName = "COM1";

        [ObservableProperty]
        int baudRate = 9600;

        [ObservableProperty]
        Parity parity = Parity.None;

        [ObservableProperty]
        int dataBits = 8;

        [ObservableProperty]
        StopBits stopBits = StopBits.One;

        [ObservableProperty]
        string iP = "127.0.0.1";

        [ObservableProperty]
        int port = 8080;

        [ObservableProperty]
        string triggerString = "T";

        [ObservableProperty]
        string receiveString = "";

        [ObservableProperty]
        string sendString = "SendString";

        [ObservableProperty]
        int orderIndex = 0;//流水号 
 
        public ScannerModel()
        {
            _ = Task.Run(Foo);
        }

        async Task Foo()
        {
            TcpListener? server=null;
            SerialPort? sp = null;
            await Task.Delay(1000);
            //TcpServer ser = new TcpServer(this);
            while (true)
            {
                await Task.Delay(10);
                try
                {  
                    if(DeviceType == InterfaceType.串口 && IsEnableServer)
                    {
                        if(sp == null)
                        {
                            sp = new SerialPort();
                            sp.PortName = PortName;
                            sp.BaudRate = BaudRate;
                            sp.DataBits = DataBits;
                            sp.Parity = Parity;
                            sp.StopBits = StopBits;
                            sp.Open();
                            sp.ReadTimeout = 500;
                            sp.DataReceived += Sp_DataReceived;                             
                        } 
                    }else
                    {
                        try
                        {
                            //if (sp != null) sp.DataReceived -= Sp_DataReceived;
                            sp?.Dispose();
                        }
                        catch { }
                        sp = null; 
                    }

                    if (IsEnableServer && DeviceType == InterfaceType.以太网)
                    {
                        if (server == null)
                        {
                            server = new TcpListener(IPAddress.Any, Port);
                            server.Start();
                            _ = Task.Run(async () =>
                            {
                                await AcceptClient(server);
                            });
                        }
                    }else
                    {
                        try
                        {
                            server?.Stop();
                            server?.Dispose();
                        }
                        catch { }
                        server = null;
                    }
                }
                catch (Exception ex)
                {
                    await Task.Delay(10);
                    MessageEvent?.Invoke(ex.Message);
                }
            }
        } 

        //响应客户端
        async Task AcceptClient(TcpListener server)
        {
            while (true)
            {
                try
                {
                    var client = server.AcceptTcpClient();
                    
                    _ = Task.Run(()=>ResponseNetwork(client));
                }
                catch (Exception ex)
                {
                    MessageEvent?.Invoke(ex.Message);
                    await Task.Delay(10);
                }
            }
        }

        async Task ResponseNetwork(TcpClient client)
        {
            try
            {
                if (IsEnableServer && client != null && client.Connected)
                {
                    client.ReceiveTimeout = 500;
                    MessageEvent?.Invoke($"客户端{client.Client.RemoteEndPoint?.ToString()}已上线");
                    var s = client.GetStream();
                    _ = Task.Run(async () =>
                    {
                        while (s != null)
                        {
                            await Task.Delay(100);
                            if (!IsConnected(client))
                            {
                                s?.Dispose();
                                s = null;
                            }
                        }
                    });
                    while (s != null) await DoServerWork(s);
                }
                else
                {
                    client?.Dispose();
                    client = null;
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                client?.Dispose();
                await Task.Delay(10);
                MessageEvent?.Invoke(ex.Message);
            }
        }

        void Foo1()
        {
            TcpServer? server = new TcpServer(this);
            SerialPort? sp = null; 
            _ = Task.Run(async () => {
                while (true)
                {
                    try
                    {
                        await Task.Delay(10);
                        if (DeviceType != InterfaceType.串口) throw new Exception("类型不匹配");
                        if (!IsEnableServer) throw new Exception("未开启服务器");
                        if (sp == null)
                        {
                            sp = new SerialPort();
                            sp.PortName = PortName;
                            sp.BaudRate = BaudRate;
                            sp.Parity = Parity;
                            sp.DataBits = DataBits;
                            sp.StopBits = StopBits;
                            sp.Open();
                            sp.DataReceived += Sp_DataReceived;
                        }
                    }
                    catch (Exception)
                    {
                        sp?.Dispose();
                        sp = null;
                    }
                }
            });
        }

        async Task DoServerWork(Stream s)
        {
            if (!IsEnableServer) return;
            if (s is null) return;
            var buffer = new byte[1024];
            int n =await s.ReadAsync(buffer, 0, buffer.Length);
            if (n > 0)
            {
                var str = Encoding.UTF8.GetString(buffer, 0, n);
                if (string.IsNullOrWhiteSpace(str)) return;
                MessageEvent?.Invoke($"收到内容：{str}");
                var str2 = GetResponseString(str);
                if (string.IsNullOrWhiteSpace(str2)) return;
                var sendData = Encoding.UTF8.GetBytes(str2);
                await s.WriteAsync(sendData);
                await s.FlushAsync();
            }
        }


        private async void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (sender is SerialPort sp)
            {
                await DoServerWork(sp.BaseStream); 
            }
        }

        public string GetResponseString(string _receiveString)
        {
            if (IsUseTriggerString && !_receiveString.Trim().Equals(TriggerString)) return "";

            if (IsAppendOrderIndex) return SendString + ((OrderIndex++).ToString().PadLeft(6, '0'));
            
            return SendString;
        }
 


        bool IsConnected(Socket s)
        {
            return !(s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) && s.Connected;
        }


        bool IsConnected(TcpClient client)
        {
            return IsConnected(client.Client);
            //return !(client.Client.Poll(1000, SelectMode.SelectRead) && client.Client.Available == 0) && client.Client.Connected;
        }

        void ClearReceiveString()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ReceiveString = "";
            });
        }

        void SetReceiveString(string str)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ReceiveString = str;
            });
        }


        public string Trigger()
        {
            Stream? s = null;
            if (DeviceType == InterfaceType.以太网)
            {
                var client = new TcpClient();
                client.Connect(IP, Port);
                s = client.GetStream();
            }
            else if (DeviceType == InterfaceType.串口)
            {
                var sp = new System.IO.Ports.SerialPort(PortName, BaudRate, Parity, DataBits, StopBits);
                sp.Open();
                s = sp.BaseStream;
            }

            if (s is null) throw new Exception("无法连接到扫码枪");
            return DoClientWork(s);
        }

        string DoClientWork(Stream s)
        {
            s.Write(Encoding.UTF8.GetBytes(TriggerString));
            Thread.Sleep(500);
            byte[] buffer = new byte[1024];
            int bytesRead = s.Read(buffer, 0, buffer.Length);
            Array.Resize(ref buffer, bytesRead);
            ReceiveString= Encoding.UTF8.GetString(buffer);
            return ReceiveString;
        }
    }
}
