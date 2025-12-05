using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;

namespace 模拟扫码枪
{
    /// <summary>
    /// 空类，什么都不做
    /// </summary>
    public partial class ScannerModel : CommunicationModelBase
    {
 
    }

    public partial class CommunicationModelBase : ObservableObject, IDisposable
    {
        public enum IInterfaceType { 串口, TCP客户端, TCP服务器 }
        /// <summary>
        /// 主动触发=先发后收；被动接收=先收后发；
        /// </summary>
        public enum IWorkModel { 主动触发, 被动接收, 仅发送, 仅接收 }

        public enum IResponseModel { 常规, 附加流水号, 附加随机数, 解析发送 }//响应模式

        protected string GetResponseString(string _receiveString)
        {
            if (IsUseTriggerString && !_receiveString.Trim().Equals(TriggerString)) return "";

            switch (ResponseModel)
            {
                default:
                case IResponseModel.常规:
                    return SendString;
                case IResponseModel.附加流水号:
                    var n1 = OrderIndex++;
                    var str1 = (AppendHexString ? n1.ToString("X") : n1.ToString());
                    if (str1.Length < AppendContentLength) str1 = str1.PadLeft(AppendContentLength, '0');
                    else if (str1.Length > AppendContentLength) str1 = str1.Substring(str1.Length - AppendContentLength, AppendContentLength);
                    return $"{SendString}{str1}";
                case IResponseModel.附加随机数:
                    var n2 = new Random(Guid.NewGuid().GetHashCode()).Next(0, int.MaxValue);
                    var str2 = (AppendHexString ? n2.ToString("X") : n2.ToString());
                    if (str2.Length < AppendContentLength) str2 = str2.PadLeft(AppendContentLength, '0');
                    else if (str2.Length > AppendContentLength) str2 = str2.Substring(str2.Length - AppendContentLength, AppendContentLength);
                    return $"{SendString}{str2}"; ;
                case IResponseModel.解析发送:
                    var group = SendString.Split("|");
                    var n3 = new Random(Guid.NewGuid().GetHashCode()).Next(0, group.Length);
                    return group[n3];
            }
        }

        private static string CmdPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System) + "\\cmd.exe";

        public static string RunCmdCommand(string command)
        {
            using (Process process = new Process())
            {
                command = command.Trim().TrimEnd('&') + "&exit";

                process.StartInfo.FileName = CmdPath;
                process.StartInfo.CreateNoWindow = true;// 隐藏窗口运行
                process.StartInfo.RedirectStandardError = true;// 重定向错误流
                process.StartInfo.RedirectStandardInput = true;// 重定向输入流
                process.StartInfo.RedirectStandardOutput = true;// 重定向输出流
                process.StartInfo.UseShellExecute = false;

                process.Start();

                process.StandardInput.WriteLine(command);// 写入Cmd命令
                process.StandardInput.AutoFlush = true;

                string output = process.StandardOutput.ReadToEnd();//读取结果
                process.WaitForExit();
                process.Close();
                return output;
            }
        }

        public static async Task<string> RunCmdCommandAsync(string command)
        {
            using (Process process = new Process())
            {
                command = command.Trim().TrimEnd('&') + "&exit";

                process.StartInfo.FileName = CmdPath;
                process.StartInfo.CreateNoWindow = true;// 隐藏窗口运行
                process.StartInfo.RedirectStandardError = true;// 重定向错误流
                process.StartInfo.RedirectStandardInput = true;// 重定向输入流
                process.StartInfo.RedirectStandardOutput = true;// 重定向输出流
                process.StartInfo.UseShellExecute = false;

                process.Start();

                process.StandardInput.WriteLine(command);// 写入Cmd命令
                process.StandardInput.AutoFlush = true;

                string output = process.StandardOutput.ReadToEnd();//读取结果
                await process.WaitForExitAsync();
                process.Close();
                return output;
            }
        }

        public event Action<string>? UpdateMessageEvent;
        public event Action<string>? ReceiveCodeEvent;

        [ObservableProperty]
        bool isEnable = true;

        [ObservableProperty]
        bool isUseTriggerString = false;//使用触发字符串

        [ObservableProperty]
        string sendString = "SendString";

        [ObservableProperty]
        int orderIndex = 0;//流水号 

        [JsonIgnore]
        public IInterfaceType[] DeviceTypeValues => Enum.GetValues(typeof(IInterfaceType)).Cast<IInterfaceType>().ToArray();

        [JsonIgnore]
        public IWorkModel[] WorkModelValues => Enum.GetValues(typeof(IWorkModel)).Cast<IWorkModel>().ToArray();

        [JsonIgnore]
        public IResponseModel[] ResponseModelValues => Enum.GetValues(typeof(IResponseModel)).Cast<IResponseModel>().ToArray();

        [JsonIgnore]
        public string[] PortNames => new string[1] { "" }.Concat(SerialPort.GetPortNames()).ToArray();

        [JsonIgnore]
        public Parity[] ParityValues => Enum.GetValues(typeof(Parity)).Cast<Parity>().ToArray();
        [JsonIgnore]
        public StopBits[] StopBitsValues => Enum.GetValues(typeof(StopBits)).Cast<StopBits>().ToArray();


        [ObservableProperty]
        IInterfaceType deviceType = IInterfaceType.TCP客户端;

        [ObservableProperty]
        IWorkModel workModel = IWorkModel.主动触发;//默认是主动触发方式

        [ObservableProperty]
        IResponseModel responseModel = IResponseModel.常规;//响应内容

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
        int appendContentLength = 6;

        [ObservableProperty]
        bool appendHexString = false;

        [ObservableProperty]
        bool appendCRLF = false;

        [ObservableProperty]
        bool isTransmitReceiveDataToStandardInput = false;//转发接收内容到标准输入设备

        [ObservableProperty]
        int autoSendInterval = 0;//大于0，且仅发送才会生效

        [property: JsonIgnore]
        [ObservableProperty]
        SolidColorBrush statusBrush = Brushes.Red;

        CancellationTokenSource cts = new CancellationTokenSource();

        TcpClient? pubClient;//TCP客户端 

        [JsonIgnore]
        public bool CanTrigger => IsEnable && (WorkModel == IWorkModel.主动触发 || WorkModel == IWorkModel.仅发送);

        [JsonIgnore]
        public bool IsSerialPort => DeviceType == IInterfaceType.串口;

        [JsonIgnore]
        public bool IsNetwork => DeviceType == IInterfaceType.TCP服务器 || DeviceType == IInterfaceType.TCP客户端;

        [JsonIgnore]
        public bool IsOnlyReceiveModel => WorkModel == IWorkModel.仅接收;

        public CommunicationModelBase()
        {
            //服务器响应客户端连接线程
            _ = Task.Run(AcceptClientTask, cts.Token);

            //检测连接状态线程
            _ = Task.Run(AutoPingTask, cts.Token);

            //检测连接是否是否正常
            _ = Task.Run(CheckClientTask, cts.Token);

            //服务器模式主动接收线程
            _ = Task.Run(NetClientTask, cts.Token);

            //被动接收线程
            _ = Task.Run(ReceiveTask, cts.Token);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e != null && e.PropertyName != null)
            {
                if (e.PropertyName.Equals(nameof(WorkModel))
                || e.PropertyName.Equals(nameof(DeviceType))
                || e.PropertyName.Equals(nameof(IsEnable))
                || e.PropertyName.Equals(nameof(IP))
                || e.PropertyName.Equals(nameof(Port))
                || e.PropertyName.Equals(nameof(PortName))
                || e.PropertyName.Equals(nameof(BaudRate))
                || e.PropertyName.Equals(nameof(DataBits))
                || e.PropertyName.Equals(nameof(Parity))
                || e.PropertyName.Equals(nameof(StopBits))
                )
                {
                    SendCommand.NotifyCanExecuteChanged();
                    TriggerCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(IsSerialPort));
                    OnPropertyChanged(nameof(IsNetwork));
                    OnPropertyChanged(nameof(IsOnlyReceiveModel));
                    try
                    {
                        pubClient?.Dispose();
                    }
                    catch { }
                    pubClient = null;
                    ctsServer?.Cancel();
                }
                //SendKeys.SendWait("");
            }
        }

        async Task AutoPingTask()
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

        async Task CheckClientTask()
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

        //被动模式
        async Task ReceiveTask()
        {
            //TcpClient? _client=null;//客户端
            SerialPort? _sp = null;
            while (true)
            {
                try
                {
                    await Task.Delay(50);

                    if (DeviceType != IInterfaceType.串口 || (WorkModel != IWorkModel.被动接收 && WorkModel != IWorkModel.仅接收))
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


                    if (IsEnable && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                    {
                        if (DeviceType == IInterfaceType.串口)
                        {
                            if (_sp == null)
                            {
                                _sp = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits);
                                _sp.ReadTimeout = 500;
                                _sp.WriteTimeout = 500;
                                _sp.Open();
                                _ = Task.Run(async () => await spWork(_sp));
                            }
                        }
                        else//以太网
                        {
                            if (DeviceType == IInterfaceType.TCP客户端)
                            {
                                //客户端，主动连接
                                if (pubClient == null)
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
                    UpdateFilterMessage($"【{IP}:{Port}/{PortName}】被动接收模式出现错误：{ex.Message}");
                }
            }
        }

        async Task spWork(SerialPort _sp)
        {
            try
            {
                _sp.DataReceived += Sp_DataReceived;
                while (true)
                {
                    if (IsEnable && IsEnable && DeviceType == IInterfaceType.串口 && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                    {
                        await Task.Delay(50);
                    }
                    else if (_sp != null)
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

        async Task NetClientTask()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(10);
                    if (IsEnable && (DeviceType == IInterfaceType.TCP客户端 || DeviceType == IInterfaceType.TCP服务器) && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                    {
                        if (pubClient != null) await ReceiveWork(pubClient.GetStream());
                    }
                }
                catch (Exception ex)
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
                StatusBrush = Brushes.DarkGreen;
                if (!IsEnable || s is null || !s.CanRead || !s.CanWrite) return;
                var buffer = new byte[4096];
                int n = await s.ReadAsync(buffer, 0, buffer.Length);
                if (n > 0)
                {
                    if (WorkModel == IWorkModel.仅接收)
                    {
                        var str = Encoding.UTF8.GetString(buffer, 0, n);
                        ReceiveString = str;
                        ReceiveCodeEvent?.Invoke(ReceiveString);
                        if (IsTransmitReceiveDataToStandardInput)
                        {
                            SendKeys.SendWait(str);
                        }
                    }
                    else if (WorkModel == IWorkModel.被动接收)
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

            }
            catch { }
        }

        CancellationTokenSource? ctsServer = null;

        /// <summary>
        /// 用于响应从站的连接请求
        /// </summary>
        /// <returns></returns>
        async Task AcceptClientTask()
        {
            await Task.Delay(1000);
            TcpListener? server = null;
            while (true)
            {
                try
                {
                    await Task.Delay(10);
                    if (IsEnable && DeviceType == IInterfaceType.TCP服务器)
                    {
                        if (server == null) server = new TcpListener(IPAddress.Any, Port);
                        server?.Start();
                        if (server != null && pubClient == null)
                        {
                            ctsServer = new CancellationTokenSource();
                            pubClient = await server.AcceptTcpClientAsync(ctsServer.Token);
                            try
                            {
                                ctsServer.Dispose();
                            }
                            catch (Exception)
                            {
                            }
                            ctsServer = null;
                        }
                    }
                    else
                    {
                        if (server != null)
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
                    if (ex.Message.Contains("通常每个套接字地址(协议/网络地址/端口)只允许使用一次"))
                    {
                        var msg = await RunCmdCommandAsync($"netstat -ano|findstr 127.0.0.1:{Port}");
                        UpdateFilterMessage($"作为服务器需要使用的端口被占用，请查看信息：\n{msg}");
                    }
                    else
                    {
                        UpdateFilterMessage($"【{IP}:{Port}/{PortName}】服务器模式出现错误：{ex.Message}");
                    }
                }
                finally
                {
                    try
                    {
                        ctsServer?.Cancel();
                    }
                    catch { }
                }
            }
        }


        bool IsConnected(Socket s)
        {
            return s != null && !(s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) && s.Connected;
        }

        bool IsConnected(TcpClient client)
        {
            return client != null && IsConnected(client.Client);
        }

        [property: JsonIgnore]
        [RelayCommand(CanExecute = nameof(CanTrigger))]
        public async Task<string> Trigger()
        {
            try
            {
                if (DeviceType == IInterfaceType.TCP客户端 || DeviceType == IInterfaceType.TCP服务器)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (DeviceType == IInterfaceType.TCP服务器 && pubClient is null)
                        {
                            UpdateFilterMessage("当前模式为TCP服务器，且无有效客户端连接");
                        }

                        if (DeviceType == IInterfaceType.TCP客户端 && pubClient == null)
                        {
                            pubClient = new TcpClient();
                            pubClient.Connect(IP, Port);
                        }
                        else if (pubClient != null)
                        {
                            return await DoClientWork(pubClient.GetStream());
                        }
                    }
                }
                else if (DeviceType == IInterfaceType.串口)
                {
                    try
                    {
                        using (var sp1 = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits))
                        {
                            sp1.ReadTimeout = 500;
                            sp1.Open();
                            return await DoClientWork(sp1.BaseStream);

                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateFilterMessage($"串口模式触发时发生错误：{ex.Message}");
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

        [RelayCommand(CanExecute = nameof(CanTrigger))]
        async void Send()
        {
            try
            {
                do
                {
                    await Trigger();
                    await Task.Delay(AutoSendInterval);
                } while (IsEnable && AutoSendInterval > 0); 
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
        }


        [property: JsonIgnore]
        [RelayCommand]
        async Task Ping()
        {
            try
            {
                if (DeviceType == IInterfaceType.TCP客户端)
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
                if (WorkModel == IWorkModel.仅发送)
                {
                    var str = GetResponseString(TriggerString);
                    if (string.IsNullOrEmpty(str)) return "";
                    var sendData = Encoding.UTF8.GetBytes(str + (AppendCRLF ? Environment.NewLine : ""));
                    await s.WriteAsync(sendData);
                    await s.FlushAsync();
                    StatusBrush = Brushes.DarkGreen;
                    return "";
                }
                else if (WorkModel == IWorkModel.主动触发)//主动触发时是要发送触发字符串的
                {
                    ReceiveString = "";
                    var writeData = Encoding.UTF8.GetBytes(TriggerString + (AppendCRLF ? Environment.NewLine : ""));
                    s.Write(writeData);
                    await s.FlushAsync();
                    if (DeviceType == IInterfaceType.串口)
                        await Task.Delay(50);
                    var buffer = new byte[4096];
                    int n = s.Read(buffer, 0, buffer.Length);
                    if (n > 0)
                    {
                        ReceiveString = Encoding.UTF8.GetString(buffer, 0, n);
                        ReceiveCodeEvent?.Invoke(ReceiveString);
                        StatusBrush = Brushes.DarkGreen;
                        return ReceiveString;
                    }
                    return "";
                }
                return "";
            }
            catch (Exception ex)
            {
                StatusBrush = Brushes.Red;
                UpdateFilterMessage($"【{IP}:{Port}/{PortName}】客户端模式出现错误：{ex.Message}");
                return "";

            }
        }


        public async void Dispose()
        {
            cts?.Cancel();
            await Task.Delay(500);
            try
            {
                cts.Dispose();
            }
            catch (Exception) { }

            cts = new CancellationTokenSource();
        }

        string lastMsg = "";

        protected void UpdateFilterMessage(string msg)
        {
            if (msg != lastMsg)
            {
                UpdateMessageEvent?.Invoke(msg);
                lastMsg = msg;
            }
        }

        protected void UpdateMessage(string msg)
        {
            UpdateMessageEvent?.Invoke(msg);
        }
    }

}
