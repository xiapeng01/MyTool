using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; 
using System;
using System.Collections.Concurrent;
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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static 模拟扫码枪.CommunicationModelBase;
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
        #region Enums
        public enum IInterfaceType { 串口, TCP客户端, TCP服务器 }
        /// <summary>
        /// 主动触发=先发后收；被动接收=先收后发；
        /// </summary>
        public enum IWorkModel { 主动触发, 被动接收, 仅发送, 仅接收 }

        public enum IResponseModel { 常规, 附加流水号, 附加随机数, 解析发送 }//响应模式
        public enum IAppendContent { 无, 回车, 换行, 回车换行 }//附加分割符号
        #endregion

        #region Fields
        private static readonly string CmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

        private readonly ConcurrentBag<TcpClient> clientList = new();
        private TcpClient? pubClient;//TCP客户端 
        private CancellationTokenSource? ctsServer;
        CancellationTokenSource ctsAll = new CancellationTokenSource();
        private readonly object clientListLock = new object();

        private Task? t1, t2, t3, t4;
        private static int Count = 0;
        private readonly int index = 0;
        private string lastMsg = "";
        private bool disposed = false;
        #endregion

        #region Events
        public event Action<string>? UpdateMessageEvent;
        public event Action<string>? ReceiveCodeEvent;
        #endregion

        #region Properties
        [ObservableProperty]
        string name = "默认通信设备";

        [ObservableProperty]
        private bool isEnable = false;

        [ObservableProperty]
        private bool isUseTriggerString = false;//使用触发字符串

        [ObservableProperty]
        private string sendString = "SendString";

        [ObservableProperty]
        private int orderIndex = 0;//流水号 

        [JsonIgnore]
        public IInterfaceType[] DeviceTypeValues => Enum.GetValues(typeof(IInterfaceType)).Cast<IInterfaceType>().ToArray();

        [JsonIgnore]
        public IWorkModel[] WorkModelValues => Enum.GetValues(typeof(IWorkModel)).Cast<IWorkModel>().ToArray();

        [JsonIgnore]
        public IResponseModel[] ResponseModelValues => Enum.GetValues(typeof(IResponseModel)).Cast<IResponseModel>().ToArray();

        [JsonIgnore]
        public IAppendContent[] AppendContentValues => Enum.GetValues(typeof(IAppendContent)).Cast<IAppendContent>().ToArray();

        [JsonIgnore]
        public string[] PortNames => new string[1] { "" }.Concat(SerialPort.GetPortNames()).ToArray();

        [JsonIgnore]
        public Parity[] ParityValues => Enum.GetValues(typeof(Parity)).Cast<Parity>().ToArray();

        [JsonIgnore]
        public StopBits[] StopBitsValues => Enum.GetValues(typeof(StopBits)).Cast<StopBits>().ToArray();

        [ObservableProperty]
        private IInterfaceType deviceType = IInterfaceType.TCP客户端;

        [ObservableProperty]
        private IWorkModel workModel = IWorkModel.主动触发;//默认是主动触发方式

        [ObservableProperty]
        private IResponseModel responseModel = IResponseModel.常规;//响应内容

        [ObservableProperty]
        private string portName = "COM1";

        [ObservableProperty]
        private int baudRate = 9600;

        [ObservableProperty]
        private Parity parity = Parity.None;

        [ObservableProperty]
        private int dataBits = 8;

        [ObservableProperty]
        private StopBits stopBits = StopBits.One;

        [ObservableProperty]
        private string iP = "127.0.0.1";

        [ObservableProperty]
        private int port = 8080;

        [ObservableProperty]
        private string triggerString = "T";

        [ObservableProperty]
        private string receiveString = "";

        [ObservableProperty]
        private int appendContentLength = 6;

        [ObservableProperty]
        private bool appendHexString = false;

        [ObservableProperty]
        private IAppendContent appendContent = IAppendContent.回车换行;

        [ObservableProperty]
        private bool isTransmitReceiveDataToStandardInput = false;//转发接收内容到标准输入设备

        [ObservableProperty]
        private int autoSendInterval = 0;//大于0，且仅发送才会生效

        [ObservableProperty]
        private int bufferSize = 1024;//缓冲区大小

        [ObservableProperty]
        private int readTimeout = 500;//读取超时

        [ObservableProperty]
        private int writeTimeout = 500;//写入超时

        [ObservableProperty]
        private int checkInterval = 500;//检测间隔

        [JsonIgnore]
        public int ClientCount =>clientList!=null ?clientList.Count:0;

        [property: JsonIgnore]
        [ObservableProperty]
        private SolidColorBrush statusBrush = Brushes.Red;

        [JsonIgnore]
        public bool CanTrigger => IsEnable && (WorkModel == IWorkModel.主动触发 || WorkModel == IWorkModel.仅发送);

        [JsonIgnore]
        public bool IsSerialPort => DeviceType == IInterfaceType.串口;

        [JsonIgnore]
        public bool IsNetwork => DeviceType == IInterfaceType.TCP服务器 || DeviceType == IInterfaceType.TCP客户端;

        [JsonIgnore]
        public bool IsOnlyReceiveModel => WorkModel == IWorkModel.仅接收;
        #endregion

        #region Constructors
        public CommunicationModelBase()
        {
            index = Count++;
            //检测连接是否是否正常
            t2 = Task.Run(CheckClientTask);
        }
        #endregion

        #region Public Methods
        public async void Dispose()
        {
            await Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Protected Methods
        protected string GetResponseString(string _receiveString)
        {
            if (IsUseTriggerString && !_receiveString.Trim().Equals(TriggerString)) return "";

            return ResponseModel switch
            {
                IResponseModel.常规 => SendString,
                IResponseModel.附加流水号 => GenerateSequentialNumberString(),
                IResponseModel.附加随机数 => GenerateRandomNumberString(),
                IResponseModel.解析发送 => ParseAndSelectString(),
                _ => SendString
            };
        }

        protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e?.PropertyName == null) return;

            if (e.PropertyName.Equals(nameof(WorkModel))
                || e.PropertyName.Equals(nameof(DeviceType))
                || e.PropertyName.Equals(nameof(IsEnable))
                || e.PropertyName.Equals(nameof(IP))
                || e.PropertyName.Equals(nameof(Port))
                || e.PropertyName.Equals(nameof(PortName))
                || e.PropertyName.Equals(nameof(BaudRate))
                || e.PropertyName.Equals(nameof(DataBits))
                || e.PropertyName.Equals(nameof(Parity))
                || e.PropertyName.Equals(nameof(StopBits)))
            {
                SendCommand.NotifyCanExecuteChanged();
                TriggerCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsSerialPort));
                OnPropertyChanged(nameof(IsNetwork));
                OnPropertyChanged(nameof(IsOnlyReceiveModel));

                CleanupConnections();
                ctsServer?.Cancel();
            }

            if (e.PropertyName.Equals(nameof(IsEnable)))
            {
                if (IsEnable)
                {
                    if (t1 != null) await t1;
                    if (t3 != null) await t3;
                    if (t4 != null) await t4;

                    t1 = null;
                    t2 = null;
                    t3 = null;
                    t4 = null;

                    if (!IsEnable) return;
                    await StartWorkTask();
                }
                else
                {
                    ctsServer?.Cancel();
                }
            }
        }

        protected virtual async Task<bool> ReceiveWork(Stream s)
        {
            try
            {
                StatusBrush = Brushes.DarkGreen;
                if (!IsEnable || s is null || !s.CanRead || !s.CanWrite) return false;
                s.ReadTimeout = ReadTimeout;
                s.WriteTimeout = WriteTimeout;
                var buffer = new byte[BufferSize];
                int n = await s.ReadAsync(buffer, 0, buffer.Length);

                if (n <= 0) return false;

                var str = Encoding.UTF8.GetString(buffer, 0, n).Trim();
                if (string.IsNullOrWhiteSpace(str)) return false;
                UpdateMessage($"收到内容：{str}");
                ReceiveString = str;
                return WorkModel switch
                {
                    IWorkModel.仅接收 => HandleReceiveOnlyMode(str),
                    IWorkModel.被动接收 => await HandlePassiveReceiveMode(s, str),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                HandleException(ex, "接收数据时发生错误");
                return false;
            }
        }

        protected virtual async Task<string> DoClientWork(Stream s)
        {
            try
            {
                s.ReadTimeout = ReadTimeout;

                return WorkModel switch
                {
                    IWorkModel.仅发送 => await HandleSendOnlyMode(s),
                    IWorkModel.主动触发 => await HandleActiveTriggerMode(s),
                    _ => ""
                };
            }
            catch (Exception ex)
            {
                HandleException(ex, "客户端工作时发生错误");
                return "";
            }
        }

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

        protected virtual async Task Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                IsEnable = false;
                await Task.Delay(500);

                ctsServer?.Cancel();
                ctsAll.Cancel();
                pubClient?.Dispose();

                foreach (var client in clientList)
                {
                    client?.Dispose();
                }
            }
            disposed = true;
        }
        #endregion

        #region Private Methods
        private async Task StartWorkTask()
        {
            await Task.Delay(1000, ctsAll.Token);//延时1秒
            //服务器响应客户端连接线程
            t1 = Task.Run(AcceptClientTask);

            //服务器模式主动接收线程
            t3 = Task.Run(NetClientTask);

            //被动接收线程
            t4 = Task.Run(ReceiveTask);
        }

        private string GenerateSequentialNumberString()
        {
            var n1 = OrderIndex++;
            var str1 = (AppendHexString ? n1.ToString("X") : n1.ToString());
            if (str1.Length < AppendContentLength)
                str1 = str1.PadLeft(AppendContentLength, '0');
            else if (str1.Length > AppendContentLength)
                str1 = str1.Substring(str1.Length - AppendContentLength, AppendContentLength);
            return $"{SendString}{str1}";
        }

        private string GenerateRandomNumberString()
        {
            var n2 = new Random(Guid.NewGuid().GetHashCode()).Next(0, int.MaxValue);
            var str2 = (AppendHexString ? n2.ToString("X") : n2.ToString());
            if (str2.Length < AppendContentLength)
                str2 = str2.PadLeft(AppendContentLength, '0');
            else if (str2.Length > AppendContentLength)
                str2 = str2.Substring(str2.Length - AppendContentLength, AppendContentLength);
            return $"{SendString}{str2}";
        }

        private string ParseAndSelectString()
        {
            var group = SendString.Split("|");
            var n3 = new Random(Guid.NewGuid().GetHashCode()).Next(0, group.Length);
            return group[n3];
        }

        private bool HandleReceiveOnlyMode(string receivedString)
        {
            ReceiveCodeEvent?.Invoke(ReceiveString);
            if (IsTransmitReceiveDataToStandardInput) SendKeys.SendWait(ReceiveString);
            return true;
        }

        private async Task<bool> HandlePassiveReceiveMode(Stream stream, string receivedString)
        {
            var responseString = GetResponseString(ReceiveString);
            if (string.IsNullOrWhiteSpace(responseString)) return false;

            var sendData = Encoding.UTF8.GetBytes(responseString + GetAppendContent(AppendContent));
            await stream.WriteAsync(sendData);
            await stream.FlushAsync();
            return true;
        }

        private async Task<string> HandleSendOnlyMode(Stream stream)
        {
            var str = GetResponseString(TriggerString);
            if (string.IsNullOrEmpty(str)) return "";

            var sendData = Encoding.UTF8.GetBytes(str + GetAppendContent(AppendContent));
            await stream.WriteAsync(sendData);
            await stream.FlushAsync();
            StatusBrush = Brushes.DarkGreen;
            return "";
        }

        private async Task<string> HandleActiveTriggerMode(Stream stream)
        {
            ReceiveString = "";
            var writeData = Encoding.UTF8.GetBytes(TriggerString + GetAppendContent(AppendContent));
            stream.Write(writeData);
            await stream.FlushAsync();

            if (DeviceType == IInterfaceType.串口) await Task.Delay(50, ctsAll.Token);

            var buffer = new byte[BufferSize];
            int n = stream.Read(buffer, 0, buffer.Length);

            if (n > 0)
            {
                ReceiveString = Encoding.UTF8.GetString(buffer, 0, n).Trim();
                ReceiveCodeEvent?.Invoke(ReceiveString);
                StatusBrush = Brushes.DarkGreen;
                return ReceiveString;
            }
            return "";
        }

        private string GetAppendContent(IAppendContent append)
        {
            return append switch
            {
                IAppendContent.无 => "",
                IAppendContent.回车 => "\r",
                IAppendContent.换行 => "\n",
                IAppendContent.回车换行 => "\r\n",
                _ => ""
            };
        }

        private void CleanupConnections()
        {
            pubClient?.Dispose();
            pubClient = null;
        }

        private void HandleException(Exception ex, string context)
        {
            StatusBrush = Brushes.Red;
            UpdateFilterMessage($"【{IP}:{Port}/{PortName}】{context}：{ex.Message}");
        }

        private bool IsConnected(Socket s)
        {
            return s != null && !(s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) && s.Connected;
        }

        private bool IsConnected(TcpClient client)
        {
            return client != null && IsConnected(client.Client);
        }

        private async Task CheckClientTask()
        {
            await Task.Delay(1000, ctsAll.Token);
            
            while (!disposed && !ctsAll.Token.IsCancellationRequested)
            {
                try
                {
                    OnPropertyChanged(nameof(ClientCount));
                    await Task.Delay(CheckInterval, ctsAll.Token);

                    if (DeviceType == IInterfaceType.TCP客户端)
                    {
                        if (IsEnable && pubClient == null)
                        {
                            pubClient = new TcpClient();
                            await pubClient.ConnectAsync(IP, Port);
                        }

                        var flag = pubClient != null && IsConnected(pubClient);
                        StatusBrush = flag ? Brushes.DarkGreen : Brushes.Red;
                        if (!flag) pubClient = null;
                    }

                    if (DeviceType == IInterfaceType.TCP服务器)
                    {
                        var removeClientList = new List<TcpClient>();
                        foreach (var client in clientList)
                        {
                            if (client.Client is null) removeClientList.Add(client);
                            if (!IsConnected(client)) client.Dispose();
                        }

                        foreach (var client in removeClientList)
                        {
                            clientList.TryTake(out _);
                        }

                        StatusBrush = clientList.Any(a => IsConnected(a)) ? Brushes.DarkGreen : Brushes.Red;
                    }
                }
                catch (Exception)
                {
                    StatusBrush = Brushes.Red;
                }
            }
        }

        private void SafelyUnsubscribeDataReceived(SerialPort? sp)
        {
            if (sp == null) return;

            try
            {
                // 移除事件订阅（即使多次调用也是安全的）
                sp.DataReceived -= Sp_DataReceived;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
                // 这些异常是可以预期的，不需要特殊处理
                // 例如串口已关闭或已被处置
                Debug.WriteLine($"安全地忽略串口事件取消订阅异常: {ex.GetType().Name}");
            }
            catch (Exception ex)
            {
                // 记录意外的异常，但不中断程序
                Debug.WriteLine($"取消订阅串口事件时发生异常: {ex.Message}");
            }
        }

        //被动模式
        private async Task ReceiveTask()
        {
            SerialPort? _sp = null;
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            while (IsEnable && !disposed && !ctsAll.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(ctsAll.Token);

                    if (!IsEnable || DeviceType != IInterfaceType.串口 || (WorkModel != IWorkModel.被动接收 && WorkModel != IWorkModel.仅接收))
                    {
                        SafelyUnsubscribeDataReceived(_sp);
                        _sp?.Dispose();
                        _sp = null;
                    }

                    if (IsEnable && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                    {
                        if (DeviceType == IInterfaceType.串口)
                        {
                            if (_sp == null)
                            {
                                _sp = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits);
                                _sp.ReadTimeout = ReadTimeout;
                                _sp.WriteTimeout = WriteTimeout;
                                _sp.Open();
                                _ = Task.Run(async () => await SpWork(_sp));
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
                                        await tmp.ConnectAsync(IP, Port);
                                        tmp.ReceiveTimeout = ReadTimeout;
                                        tmp.SendTimeout = WriteTimeout;
                                        pubClient = tmp;
                                    }
                                    catch
                                    {
                                        // Ignore connection errors
                                    }
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

        private async Task SpWork(SerialPort _sp)
        {
            try
            {
                _sp.DataReceived += Sp_DataReceived;
                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
                while (IsEnable && !disposed && !ctsAll.Token.IsCancellationRequested)
                {
                    if (IsEnable && DeviceType == IInterfaceType.串口 && (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                    {
                        await timer.WaitForNextTickAsync(ctsAll.Token);
                    }
                    else
                    {
                        SafelyUnsubscribeDataReceived(_sp);
                        _sp?.Dispose();
                    }
                }
            }
            catch (Exception)
            {
                SafelyUnsubscribeDataReceived(_sp);
                _sp?.Dispose();
            }
        }

        private async Task NetClientTask()
        {
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            while (IsEnable && !disposed && !ctsAll.Token.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(ctsAll.Token);
                if (IsEnable && (DeviceType == IInterfaceType.TCP客户端 || DeviceType == IInterfaceType.TCP服务器) &&
                    (WorkModel == IWorkModel.被动接收 || WorkModel == IWorkModel.仅接收))
                {
                    try
                    {
                        if (DeviceType == IInterfaceType.TCP客户端 && pubClient != null)
                            await ReceiveWork(pubClient);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"NetClient:{ex.Message}");
                        pubClient?.Dispose();
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Sp_DataReceived:{ex.Message}");
                // Ignore exceptions in event handler
            }
        }

        private async Task<bool> ReceiveWork(TcpClient client)
        {
            if (client?.Connected == true)
                return await ReceiveWork(client.GetStream());
            return false;
        }

        private static string RunCmdCommand(string command)
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

        private static async Task<string> RunCmdCommandAsync(string command)
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

        /// <summary>
        /// 用于响应从站的连接请求
        /// </summary>
        /// <returns></returns>
        private async Task AcceptClientTask()
        {
            await Task.Delay(1000, ctsAll.Token);
            TcpListener? server = null;
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
            while (IsEnable && !disposed && !ctsAll.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(ctsAll.Token);
                    if (IsEnable && DeviceType == IInterfaceType.TCP服务器)
                    {
                        if (server == null)
                        {
                            server = new TcpListener(IPAddress.Any, Port);
                            ctsServer = new CancellationTokenSource();
                        }
                        server.Start();

                        if (server != null)
                        {
                            while (IsEnable && !ctsAll.Token.IsCancellationRequested)
                            {
                                var client = await server.AcceptTcpClientAsync(ctsServer.Token);
                                clientList.Add(client);

                                _ = Task.Run(() => DoServerWork(client));
                            }

                            ctsServer?.Cancel();
                            ctsServer?.Dispose();
                            //ctsServer = null;
                        }
                    }
                    else if (server != null)
                    {
                        server.Stop();
                        server.Dispose();
                        server = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AcceptClientTask:{ex.Message}");
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
                    ctsServer?.Cancel();
                }
            }

            server?.Stop();
            server?.Dispose();
            server = null;

            foreach (var client in clientList)
            {
                if (client != null && IsConnected(client))
                {
                    client?.Dispose();
                }
            }
        }

        async Task DoServerWork(TcpClient client)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
            while (IsEnable && client.Client != null &&
                   DeviceType == IInterfaceType.TCP服务器 && IsConnected(client)
                   && !ctsAll.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(ctsAll.Token);
                    var res = await ReceiveWork(client.GetStream());
                    if (res)
                    {
                        pubClient = client;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AcceptClientTask:{ex.Message}");
                    // Ignore exceptions in loop
                }
            }
        }


        [property: JsonIgnore]
        [RelayCommand(CanExecute = nameof(CanTrigger))]
        public async Task<string> Trigger()
        {
            try
            {
                if (!IsEnable) return "";
                return DeviceType switch
                {

                    IInterfaceType.TCP客户端 => await HandleTcpClientTrigger(),
                    IInterfaceType.TCP服务器 => await HandleTcpServerTrigger(),
                    IInterfaceType.串口 => await HandleSerialPortTrigger(),
                    _ => ""
                };
            }
            catch (TimeoutException)
            {
                StatusBrush = Brushes.Red;
                return "";
            }
            catch (IOException)
            {
                StatusBrush = Brushes.Red;
                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Trigger:{ex.Message}");
                StatusBrush = Brushes.Red;
                return "";
            }
        }

        private async Task<string> HandleTcpClientTrigger()
        {
            if (pubClient is null)
            {
                if (DeviceType == IInterfaceType.TCP服务器)
                {
                    UpdateFilterMessage("当前模式为TCP服务器，且无有效客户端连接");
                    return "";
                }

                if (DeviceType == IInterfaceType.TCP客户端)
                {
                    pubClient = new TcpClient();
                    await pubClient.ConnectAsync(IP, Port);
                    _ = IsConnected(pubClient);
                }
            }

            if (pubClient != null)
            {
                return await DoClientWork(pubClient.GetStream());
            }

            return "";
        }

        private async Task<string> HandleTcpServerTrigger()
        {
            if (clientList.Any())
            {
                var tasks = clientList
                    .Where(client => client.Connected)
                    .Select(client => DoClientWork(client.GetStream()))
                    .ToList();

                if (tasks.Any())
                {
                    await Task.WhenAll(tasks);
                }
            }

            return "";
        }

        private async Task<string> HandleSerialPortTrigger()
        {
            try
            {
                using (var sp1 = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits))
                {
                    sp1.ReadTimeout = ReadTimeout;
                    sp1.Open();
                    return await DoClientWork(sp1.BaseStream);
                }
            }
            catch (Exception ex)
            {
                UpdateFilterMessage($"串口模式触发时发生错误：{ex.Message}");
                return "";
            }
        }

        [RelayCommand(CanExecute = nameof(CanTrigger))]
        private async Task Send()
        {
            try
            {
                do
                {
                    await Trigger();
                    await Task.Delay(AutoSendInterval, ctsAll.Token);
                } while (IsEnable && AutoSendInterval > 0 && !ctsAll.IsCancellationRequested);
            }
            catch (TimeoutException)
            {
                StatusBrush = Brushes.Red;
            }
            catch (IOException)
            {
                StatusBrush = Brushes.Red;
            }
            catch (Exception)
            {
                StatusBrush = Brushes.Red;
            }
        }

        [property: JsonIgnore]
        [RelayCommand]
        private async Task Ping()
        {
            try
            {
                if (DeviceType == IInterfaceType.TCP客户端)
                {
                    if (string.IsNullOrWhiteSpace(IP))
                        throw new Exception("IP地址不正确或无法获取IP地址");

                    using (Ping ping = new Ping())
                    {
                        var res = await ping.SendPingAsync(IP);
                        UpdateMessage($"PING【{IP}】:{res.Status}");
                    }
                }
            }
            catch (Exception)
            {
                StatusBrush = Brushes.Red;
            }
        }

        private async Task<string> DoClientWork(TcpClient client)
        {
            try
            {
                if (client?.Connected == true)
                    return await DoClientWork(client.GetStream());
                return "";
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }


}
 