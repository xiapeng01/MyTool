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


    public partial class CommunicationModelBase : ObservableObject
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
        bool isEnable = false;

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

        //CancellationTokenSource cts = new CancellationTokenSource();

        TcpClient? pubClient;//TCP客户端 

        [JsonIgnore]
        public bool CanTrigger => IsEnable && (WorkModel == IWorkModel.主动触发 || WorkModel == IWorkModel.仅发送);

        [JsonIgnore]
        public bool IsSerialPort => DeviceType == IInterfaceType.串口;

        [JsonIgnore]
        public bool IsNetwork => DeviceType == IInterfaceType.TCP服务器 || DeviceType == IInterfaceType.TCP客户端;

        [JsonIgnore]
        public bool IsOnlyReceiveModel => WorkModel == IWorkModel.仅接收;

        protected static int Count = 0;
        int index = 0;

        public CommunicationModelBase()
        {
            index = Count++;
        }

        async Task StartWorkTask()
        {
            await Task.Delay(1000);//延时1秒
            //服务器响应客户端连接线程
            _ = Task.Run(AcceptClientTask);

            //检测连接状态线程
            //_ = Task.Run(AutoPingTask, cts.Token);

            //检测连接是否是否正常
            _ = Task.Run(CheckClientTask);

            //服务器模式主动接收线程
            _ = Task.Run(NetClientTask);

            //被动接收线程
            _ = Task.Run(ReceiveTask);
        }

        protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
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

                if (e.PropertyName.Equals(nameof(IsEnable)))
                {
                    if(IsEnable)
                    {
                        await StartWorkTask();
                    }
                    else
                    {
                        ctsServer?.Cancel();
                    }
                }
            }
        }

        async Task AutoPingTask()
        {
            await Task.Delay(1000);
            while (IsEnable)
            {
                try
                {
                    await Task.Delay(1000);
                    await Ping();
                }
                catch { }
            }
        }

        async Task CheckClientTask()
        {
            int lastValue = 0;
            await Task.Delay(1000);
            while (IsEnable)
            {
                try
                {
                    await Task.Delay(500);
                    Debug.WriteLine($"{index}:{Port}");
                    var index2 = index;
                    var count2 = Count;
                    if (DeviceType == IInterfaceType.TCP客户端)
                    {
                        if (IsEnable && pubClient == null)
                        {
                            pubClient = new TcpClient();
                            pubClient.Connect(IP, Port);
                        }

                        var flag = pubClient != null && IsConnected(pubClient);
                        StatusBrush = flag ? Brushes.DarkGreen : Brushes.Red;
                        if (!flag) pubClient = null;
                    }
                    if (DeviceType == IInterfaceType.TCP服务器)
                    {
                        StatusBrush = (connectCounter == lastValue) ? Brushes.Red : Brushes.DarkGreen;
                        lastValue = connectCounter;
                    }

                    //else if (DeviceType == IInterfaceType.TCP服务器)
                    //{
                    //    if (clientList != null && clientList.Count > 0)
                    //    {
                    //        var res = clientList.Where(a => !IsConnected(a)).ToArray();//筛选出断开连接的设备
                    //        res.Select(a => 
                    //        {
                    //            if(a!=null) a.Dispose(); 
                    //            clientList.Remove(a);
                    //            return true;
                    //        }).ToArray();//移除断开连接的设备
                    //    }
                    //    var flag = clientList != null && clientList.Count > 0 && clientList.Any(a => IsConnected(a));
                    //    StatusBrush = flag ? Brushes.DarkGreen : Brushes.Red; 
                    //} 
                }
                catch (Exception ex)
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
            while (IsEnable)
            {
                try
                {
                    await Task.Delay(50);

                    //if(!IsEnable || DeviceType != IInterfaceType.TCP客户端)
                    //{
                    //    pubClient?.Dispose();
                    //    pubClient = null;
                    //}

                    if (!IsEnable || DeviceType != IInterfaceType.串口 || (WorkModel != IWorkModel.被动接收 && WorkModel != IWorkModel.仅接收))
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
                while (IsEnable)
                {
                    if (IsEnable && DeviceType == IInterfaceType.串口 && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
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
            while (IsEnable)
            {
                await Task.Delay(10);
                if (IsEnable && (DeviceType == IInterfaceType.TCP客户端 || DeviceType == IInterfaceType.TCP服务器) && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                {
                    try
                    {
                        if (DeviceType == IInterfaceType.TCP客户端 && pubClient != null) await ReceiveWork(pubClient);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            pubClient?.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                        pubClient = null;
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

        protected async Task<bool> ReceiveWork(TcpClient client)
        {
            if (client != null && client.Connected) return await ReceiveWork(client.GetStream());
            return false;
        }

        /// <summary>
        /// 被动接收时的作业，被动模式的串口接收，Tcp服务器,Tcp客户端都汇集到这里
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected virtual async Task<bool> ReceiveWork(Stream s)
        {
            try
            {
                StatusBrush = Brushes.DarkGreen;
                if (!IsEnable || s is null || !s.CanRead || !s.CanWrite) return false;
                var buffer = new byte[4096];
                int n = await s.ReadAsync(buffer, 0, buffer.Length);
                if (n > 0)
                {
                    if (WorkModel == IWorkModel.仅接收)
                    {
                        var str = Encoding.UTF8.GetString(buffer, 0, n).Trim();
                        if (string.IsNullOrWhiteSpace(str)) return false;
                        UpdateMessage($"收到内容：{str}");
                        ReceiveString = str;
                        ReceiveCodeEvent?.Invoke(ReceiveString);
                        if (IsTransmitReceiveDataToStandardInput) SendKeys.SendWait(ReceiveString);
                        return true;
                    }
                    else if (WorkModel == IWorkModel.被动接收)
                    {
                        var str = Encoding.UTF8.GetString(buffer, 0, n).Trim();
                        if (string.IsNullOrWhiteSpace(str)) return false;
                        UpdateMessage($"收到内容：{str}");
                        ReceiveString = str;
                        var str2 = GetResponseString(ReceiveString);
                        if (string.IsNullOrWhiteSpace(str2)) return false;
                        var sendData = Encoding.UTF8.GetBytes(str2);
                        await s.WriteAsync(sendData);
                        await s.FlushAsync();
                        return true;
                    }
                }

            }
            catch { }
            return false;
        }

        CancellationTokenSource? ctsServer = null;

        List<TcpClient> clientList = new();

        int connectCounter = 0;//只要有连接，计数就一直变化

        /// <summary>
        /// 用于响应从站的连接请求
        /// </summary>
        /// <returns></returns>
        async Task AcceptClientTask()
        {
            await Task.Delay(1000);
            TcpListener? server = null;
            while (IsEnable)
            {
                try
                {
                    await Task.Delay(10);
                    if (IsEnable && DeviceType == IInterfaceType.TCP服务器)
                    {
                        if (server == null)
                        {
                            server = new TcpListener(IPAddress.Any, Port); 
                            ctsServer = new CancellationTokenSource();
                        }
                        server?.Start(); 

                        if (server != null)
                        { 
                            while (IsEnable)
                            {
                                var client = await server.AcceptTcpClientAsync(ctsServer.Token);
                                clientList.Add(client);
                                var tmpCts = new CancellationTokenSource();
                                _ = Task.Run(async () =>
                                {
                                    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
                                    while (IsEnable && IsConnected(client))
                                    {
                                        await timer.WaitForNextTickAsync();
                                        connectCounter++;//只要有连接，计数就一直变化
                                    }
                                    clientList.Remove(client);
                                    tmpCts.Cancel();
                                });
                                _ = Task.Run(async () =>
                                {
                                    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
                                    while (IsEnable && !tmpCts.IsCancellationRequested && DeviceType == IInterfaceType.TCP服务器 && IsConnected(client))
                                    {
                                        try
                                        {
                                            await timer.WaitForNextTickAsync();
                                            var res = await ReceiveWork(client.GetStream());
                                            if (res)
                                            {
                                                pubClient = client;
                                            }
                                        }
                                        catch (Exception)
                                        {

                                        }
                                    }
                                });
                            }
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
            if (server != null)
            {
                try
                {
                    server.Stop();
                    server.Dispose();
                    server = null;
                }
                catch (Exception)
                {
                }
                server = null;
            }
        }

        readonly object lckCreatePubClient = new object();


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
            var index2 = index;
            var count2 = Count;

            try
            {
                if (DeviceType == IInterfaceType.TCP客户端)
                {
                    if (pubClient is null)
                    {
                        if (DeviceType == IInterfaceType.TCP服务器)
                        {
                            UpdateFilterMessage("当前模式为TCP服务器，且无有效客户端连接");
                        }
                        else if (DeviceType == IInterfaceType.TCP客户端)
                        {
                            pubClient = new TcpClient();
                            pubClient.Connect(IP, Port);
                            var status = IsConnected(pubClient);
                        }
                    }
                    if (pubClient != null)
                    {
                        return await DoClientWork(pubClient.GetStream());
                    }
                }
                else if (DeviceType == IInterfaceType.TCP服务器)
                {
                    if (clientList != null && clientList.Count > 0)
                    {
                        clientList.Select(a => Task.Run(async () => await DoClientWork(a))).ToArray();//ToArray消费Linq
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
        async Task Send()
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
                    UpdateMessage($"PING【{IP}】:{res.Status.ToString()}");
                    //if (res.Status == IPStatus.Success)
                    //{
                    //    StatusBrush = Brushes.DarkGreen;
                    //}
                    //else
                    //{
                    //    throw new Exception(res.Status.ToString());
                    //}

                }
            }
            catch (Exception ex)
            {
                StatusBrush = Brushes.Red;
            }
        }

        protected async Task<string> DoClientWork(TcpClient client)
        {
            try
            {
                if (client != null && client.Connected) return await DoClientWork(client.GetStream());
                return "";
            }
            catch (Exception ex)
            {
                return "";
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
                    var writeData = Encoding.UTF8.GetBytes(AppendCRLF ? TriggerString + Environment.NewLine : TriggerString);
                    s.Write(writeData);
                    await s.FlushAsync();
                    if (DeviceType == IInterfaceType.串口) await Task.Delay(50);

                    var buffer = new byte[4096];
                    int n = s.Read(buffer, 0, buffer.Length);
                    if (n > 0)
                    {
                        ReceiveString = Encoding.UTF8.GetString(buffer, 0, n).Trim();
                        ReceiveCodeEvent?.Invoke(ReceiveString);
                        StatusBrush = Brushes.DarkGreen;
                        return ReceiveString;
                    }
                    return "";
                }
                return "";
            }
            catch (IOException ex)
            {
                //pubClient?.Dispose();
                //pubClient = null;
                return "";
            }
            catch (Exception ex)
            {
                StatusBrush = Brushes.Red;
                UpdateFilterMessage($"【{IP}:{Port}/{PortName}】客户端模式出现错误：{ex.Message}");
                return "";
            }

        }

        string lastMsg = "";

        protected void UpdateFilterMessage(string msg)
        {
            if (msg != lastMsg)
            {
                //Service.UpdateMessage(msg);
                UpdateMessageEvent?.Invoke(msg);
                lastMsg = msg;
            }
        }

        protected void UpdateMessage(string msg)
        {
            //Service.UpdateMessage(msg);
            UpdateMessageEvent?.Invoke(msg);
        }
    }


}
 