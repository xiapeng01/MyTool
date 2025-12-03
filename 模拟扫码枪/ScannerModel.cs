using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace 模拟扫码枪
{
    public enum InterfaceType { 串口, 以太网 }
    public enum ConnectionType { 客户端, 服务器 }
    public enum WorkModel { 主动触发, 被动接收 }
    /// <summary>
    /// 空类，什么都不做
    /// </summary>
    public partial class ScannerModel:ScannerModelBase
    {
        protected override Task ReceiveWork(Stream s)
        {
            return DoServerWork(s);
        }

        async Task DoServerWork(Stream s)
        {


            if (!IsEnableClient) return;
            if (s is null) return;
            if (!s.CanRead) return;
            if (!s.CanWrite) return;
            var buffer = new byte[1024];
            int n = await s.ReadAsync(buffer, 0, buffer.Length);
            if (n > 0)
            {
                var str = Encoding.UTF8.GetString(buffer, 0, n);
                if (string.IsNullOrWhiteSpace(str)) return;
                UpdateMessage($"收到内容：{str}");
                ReceiveString = str;
                var str2 = GetResponseString(str);
                if (string.IsNullOrWhiteSpace(str2)) return;
                var sendData = Encoding.UTF8.GetBytes(str2);
                await s.WriteAsync(sendData);
                await s.FlushAsync();
            }
        }
  
        protected override async Task<string> DoClientWork(Stream s)
        {
            try
            {
                var str = GetResponseString(TriggerString);
                if (string.IsNullOrEmpty(str)) return "";
                var sendData = Encoding.UTF8.GetBytes(str);
                await s.WriteAsync(sendData);
                await s.FlushAsync();
                return "";
            }
            catch (Exception)
            {
                
            }
            return "";
        }
    }

    public partial class ScannerModelBase:ObservableObject,IDisposable
    { 
        public event Action<string>? UpdateMessageEvent;

        [ObservableProperty]
        bool isEnableClient = true;

        [ObservableProperty]
        bool isEnableServer = false;//启用虚拟服务器

        [ObservableProperty]
        bool isUseTriggerString = false;//使用触发字符串

        [ObservableProperty]
        string sendString = "SendString";

        [ObservableProperty]
        bool isAppendOrderIndex = false;//是否自动附加流水号

        [ObservableProperty]
        int orderIndex = 0;//流水号 

        [JsonIgnore]
        public InterfaceType[] DeviceTypeValues => Enum.GetValues(typeof(InterfaceType)).Cast<InterfaceType>().ToArray();
        [JsonIgnore]        
        public ConnectionType[] ConnectionTypes => Enum.GetValues(typeof(ConnectionType)).Cast<ConnectionType>().ToArray();

        [JsonIgnore]
        public WorkModel[] WorkModelValues=> Enum.GetValues(typeof(WorkModel)).Cast<WorkModel>().ToArray();

        [JsonIgnore]
        public string[] PortNames => SerialPort.GetPortNames();

        [JsonIgnore]
        public Parity[] ParityValues => Enum.GetValues(typeof(Parity)).Cast<Parity>().ToArray();
        [JsonIgnore]
        public StopBits[] StopBitsValues => Enum.GetValues(typeof(StopBits)).Cast<StopBits>().ToArray();

        [ObservableProperty]
        ConnectionType connectionType = ConnectionType.客户端;//连接方式

        [ObservableProperty]
        InterfaceType deviceType = InterfaceType.以太网;

        [ObservableProperty]
        WorkModel workModel = WorkModel.主动触发;//默认是主动触发方式

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

        [JsonIgnore]
        public bool CanTrigger => WorkModel == WorkModel.主动触发;

        [property: JsonIgnore]
        [ObservableProperty]
        SolidColorBrush statusBrush = Brushes.Red;

        CancellationTokenSource cts = new CancellationTokenSource();

        TcpClient? pubClient;//TCP客户端 
        public ScannerModelBase()
        {
            //服务器响应客户端连接线程
            _ = Task.Run(ServerJob, cts.Token);

            //检测连接状态线程
            _ = Task.Run(AutoPingWork, cts.Token);

            //检测连接是否是否正常
            _ = Task.Run(CheckClient, cts.Token);

            //服务器模式主动接收线程
            _ = Task.Run(clientWork, cts.Token);

            //被动接收线程
            _ = Task.Run(Job1, cts.Token); 
        }

        protected string GetResponseString(string _receiveString)
        {
            if (IsUseTriggerString && !_receiveString.Trim().Equals(TriggerString)) return "";

            if (IsAppendOrderIndex) return SendString + ((OrderIndex++).ToString().PadLeft(6, '0'));

            return SendString;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if(e!=null && e.PropertyName!=null && (e.PropertyName.Equals(nameof(WorkModel)) || e.PropertyName.Equals(nameof(ConnectionType))||e.PropertyName.Equals(DeviceType)))
            {
                try
                {
                    pubClient?.Dispose();
                }
                catch { }
                pubClient = null;
                TriggerCommand.NotifyCanExecuteChanged();
            }
        }

        async Task AutoPingWork()
        {
            await Task.Delay(1000, cts.Token);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cts.Token);
                    await Ping();
                }
                catch { }
            }
        }

        async Task CheckClient()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(30);
                    if (pubClient != null)
                    {
                        var flag = IsConnected(pubClient);
                        if (flag)
                        {
                            StatusBrush = Brushes.DarkGreen;
                        }
                        else
                        {
                            pubClient = null;
                            StatusBrush = Brushes.Red;
                        }
                    }
                }
                catch (Exception)
                {
                    StatusBrush = Brushes.Red;
                }
            }
        }

        //被动模式Job
        async Task Job1()
        {
            //TcpClient? _client=null;//客户端
            SerialPort? _sp=null;
            while (true)
            {
                try
                {
                    await Task.Delay(50);

                    if (DeviceType != InterfaceType.串口 || WorkModel != WorkModel.被动接收)
                    {
                        if (_sp != null)
                        {
                            try
                            {
                                _sp.DataReceived -= Sp_DataReceived; 
                                _sp?.Dispose();
                                _sp = null;
                            }
                            catch (Exception)
                            {
                                 
                            }
                        }                        
                    } 


                    if (WorkModel == WorkModel.被动接收)
                    {
                        if (DeviceType == InterfaceType.串口)
                        {
                            if(_sp ==null)
                            {
                                _sp = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits); 
                                _sp.ReadTimeout = 500;
                                _sp.WriteTimeout = 500;
                                _sp.Open();
                                _ = Task.Run(async() => await spWork(_sp));
                            }  
                        }
                        else//以太网
                        {
                            if (ConnectionType == ConnectionType.客户端)
                            {
                                //客户端，主动连接
                                if(pubClient==null)
                                {
                                    try
                                    {
                                        var tmp = new TcpClient();
                                        tmp.Connect(IP, Port);
                                        tmp.ReceiveTimeout = 500;
                                        tmp.SendTimeout = 500;
                                        pubClient = tmp;
                                    }
                                    catch { }
                                } 
                            } 
                        }

                    }
                }
                catch (Exception ex)
                {
                    //Service.UpdateMessage($"被动接收任务出错：{ex.Message}");
                }
            }
        }

        async Task spWork(SerialPort _sp)
        {
            try
            {
                _sp.DataReceived += Sp_DataReceived;
                while(DeviceType == InterfaceType.串口 && WorkModel == WorkModel.被动接收)
                {
                    await Task.Delay(50);
                }
            }
            catch (Exception)
            {
                if (_sp != null)
                {
                    try
                    {
                        _sp.DataReceived -= Sp_DataReceived; 
                        _sp?.Dispose();
                    }
                    catch (Exception)
                    {
                         
                    }
                }
            } 
        }

        async Task clientWork()
        {
            while(true)
            {
                try
                {
                    await Task.Delay(10);
                    if (DeviceType == InterfaceType.以太网 && WorkModel == WorkModel.被动接收)
                    {
                        if (pubClient != null) await ReceiveWork(pubClient.GetStream());
                    }
                }
                catch (Exception)
                {
                    if (pubClient != null)
                    {
                        try
                        {
                            pubClient?.Dispose();
                            pubClient = null;
                        }
                        catch (Exception)
                        {

                        }
                    }
                }
            }

        } 

        private async void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (sender is SerialPort sp)
                {
                    await ReceiveWork(sp.BaseStream);
                }
            }
            catch { }
        }

        /// <summary>
        /// 被动接收时的作业，被动模式的串口接收，Tcp服务器,Tcp客户端都汇集到这里
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected virtual async Task ReceiveWork(Stream s)
        {
            try
            { 
                var buffer=new byte[4096];
                int n=await s.ReadAsync(buffer, 0, buffer.Length);
                if (n>0)
                {
                    var str = Encoding.UTF8.GetString(buffer, 0, n);
                    ReceiveString = str;
                } 
            }
            catch { }
        }

        /// <summary>
        /// 用于响应从站的连接请求
        /// </summary>
        /// <returns></returns>
        async Task ServerJob()
        {
            await Task.Delay(1000);
            TcpListener? server = null;
            while (true)
            {
                try
                {
                    await Task.Delay(10);
                    if (DeviceType == InterfaceType.以太网 && ConnectionType == ConnectionType.服务器)
                    {
                        if (server == null) server = new TcpListener(IPAddress.Any, Port);
                        server?.Start();
                        if (server != null)
                        {
                            if(pubClient == null) pubClient = server?.AcceptTcpClient(); 
                        }
                    }else
                    {
                        if(server !=null)
                        {
                            try
                            {
                                server?.Stop();
                                server?.Dispose();
                                server = null;
                            }
                            catch (Exception)
                            { 
                            }
                        }
                    }
                }
                catch (Exception ex)
                {                 
                }
            }
        }
         

        bool IsConnected(Socket s)
        {
            return !(s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) && s.Connected;
        } 

        bool IsConnected(TcpClient client)
        {
            return client !=null && IsConnected(client.Client);
        }

        [property: JsonIgnore]
        [RelayCommand(CanExecute = nameof(CanTrigger))]
        public async Task<string> Trigger()
        {
            try
            { 
                if (DeviceType == InterfaceType.以太网)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (pubClient == null)
                        {
                            if (ConnectionType == ConnectionType.客户端)
                            {
                                pubClient = new TcpClient();
                                pubClient.Connect(IP, Port);
                            } 
                        }
                        else
                        {
                            return await DoClientWork(pubClient.GetStream());
                        }
                    } 
                }
                else if (DeviceType == InterfaceType.串口)
                {
                    using (var sp1 = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits))
                    {
                        sp1.ReadTimeout = 500;
                        sp1.Open();
                        return await DoClientWork(sp1.BaseStream);

                    } 
                }
                StatusBrush = Brushes.DarkGreen;
                return "";
            }
            catch (TimeoutException tex)
            {
                //throw new Exception("读取数据超时，请检查设备连接和设置。", tex);
                StatusBrush = Brushes.Red;
            }
            catch (IOException ioex)
            {
                //throw new Exception("通信错误，请检查设备连接。", ioex);
                StatusBrush = Brushes.Red;
            }
            catch (Exception ex)
            {
                StatusBrush = Brushes.Red;
            }
            return ""; 
        }
         

        [property: JsonIgnore]
        [RelayCommand]
        async Task Ping()
        {
            try
            { 
                if (DeviceType == InterfaceType.以太网)
                {
                    if (ConnectionType == ConnectionType.客户端)
                    {
                        if (string.IsNullOrWhiteSpace(IP)) throw new Exception("IP地址不正确或无法获取IP地址");
                        Ping ping = new Ping();
                        var res = await ping.SendPingAsync(IP);
                        if (res.Status == IPStatus.Success)
                        {
                            StatusBrush = Brushes.DarkGreen;
                        }
                        else
                        {
                            throw new Exception(res.Status.ToString());
                        }
                    } 
                    
                }
            }
            catch (Exception ex)
            {
                StatusBrush = Brushes.Red; 
            }
        }
        /// <summary>
        /// 主动模式的作业都汇集在这里，主动模式的串口，Tcp客户端，Tcp服务器都汇集在这里，主动模式是发送字符串，然后等待接收内容
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected virtual async Task<string> DoClientWork(Stream s)
        {
            s.ReadTimeout = 500; 
            try
            {
                ReceiveString = "";
                var writeData = Encoding.UTF8.GetBytes(TriggerString+Environment.NewLine);
                s.Write(writeData);
                await s.FlushAsync();
                await Task.Delay(20);
                var buffer = new byte[1024];
                int n = s.Read(buffer, 0, buffer.Length); 
                if(n>0)
                {
                    var readString = Encoding.UTF8.GetString(buffer, 0, n);
                    ReceiveString = readString;
                    //Application.Current?.Dispatcher?.Invoke(() =>
                    //{
                    //    ReceiveString = readString;
                    //});
                    StatusBrush = Brushes.DarkGreen;
                    return readString;
                }
                return "";
            }
            catch (Exception)
            {
                StatusBrush = Brushes.Red;
                return "";
            }
        }
         

        public void Dispose()
        {            
            cts?.Cancel();
        }

        protected void UpdateMessage(string msg)
        {
            UpdateMessageEvent?.Invoke(msg);
        }
    }
}
