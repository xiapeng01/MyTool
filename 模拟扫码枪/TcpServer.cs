using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.IO.Pipelines;
using CommunityToolkit.Mvvm.ComponentModel;

namespace 模拟扫码枪
{
   public interface IService
    {
        InterfaceType DeviceType { get; set; }//接口类型
        int Port { get; set; }//端口
        bool IsEnableServer { get; set; }//开启或关闭服务器

        string GetResponseString(string receiveString);//处理消息
    }

    public partial class TcpServer:ObservableObject
    {
        IService service;
              

        public TcpServer(IService _service)
        {
            service= _service;
            
            _ = Task.Run(Server);
        }  

        async Task Server()
        {
            var listener = new TcpListener(IPAddress.Any, service.Port);
            listener.Start();
            Console.WriteLine($"[TCP Server] Listening on port {service.Port}...");

            try
            {
                while (true)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleConnectionAsync(tcpClient));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Server error: {ex}");
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task HandleConnectionAsync(TcpClient client)
        {
            using (client)
            {
                Console.WriteLine($"[INFO] Client connected: {client.Client.RemoteEndPoint}");

                var pipe = new Pipe();
                var readTask = FillPipeAsync(client.GetStream(), pipe.Writer);
                var writeTask = ReadPipeAsync(pipe.Reader, client.GetStream());

                try
                {
                    await Task.WhenAll(readTask, writeTask);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Connection error: {ex.Message}");
                }

                Console.WriteLine($"[INFO] Client disconnected: {client.Client.RemoteEndPoint}");
            }
        }

        private async Task FillPipeAsync(NetworkStream stream, PipeWriter writer)
        {
            try
            {
                while (true)
                {
                    Memory<byte> memory = writer.GetMemory(4096);
                    int bytesRead = await stream.ReadAsync(memory);
                    if (bytesRead == 0) break; // Client closed connection

                    writer.Advance(bytesRead);
                    FlushResult result = await writer.FlushAsync();

                    if (result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            catch
            {
                // Ignore
            }
            finally
            {
                await writer.CompleteAsync();
            }
        }

        private async Task ReadPipeAsync(PipeReader reader, NetworkStream stream)
        {
            try
            {
                while (true)
                {

                    if (service.DeviceType != InterfaceType.以太网) continue;
                    if (!service.IsEnableServer) continue;

                    ReadResult result = await reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    // 只要收到任意数据就响应一次（可根据需求调整）
                    if (buffer.Length > 0)
                    {
                        // 可选：打印收到的内容（调试用）
                        // string received = Encoding.UTF8.GetString(buffer.ToArray());
                        // Console.WriteLine($"[RECV] {received}");
                        var receiveString=Encoding.UTF8.GetString(buffer);
                        // 回复固定内容 
                        var responseString = service.GetResponseString(receiveString);
                        if (responseString != null && responseString.Length > 0)
                        {
                            await stream.WriteAsync(Encoding.UTF8.GetBytes(responseString));
                        }

                        // 消费整个缓冲区
                        buffer = buffer.Slice(buffer.Length);
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                        break;
                }
            }
            catch
            {
                // Ignore
            }
            finally
            {
                await reader.CompleteAsync();
                await Task.Delay(10);
            }
        }
    }
}
