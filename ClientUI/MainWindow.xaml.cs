using ChatRoom.Packet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Instrumentation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ChatRoom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static TcpClient TcpClient { get; set; }

        public MainWindow()
        {
            _ = new Mutex(true, Assembly.GetExecutingAssembly().GetName().Name, out bool isNotRunning);
            if (!isNotRunning)
            {
                _ = MessageBox.Show("你只能同时运行一个聊天室实例！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InstanceNotFoundException("你只能同时运行一个聊天室实例！");
            }
            InitializeComponent();
        }

        private void Connect(object sender, RoutedEventArgs e)
        {
            string ip = IPBox.Text;
            LoginGrid.IsEnabled = false;
            TcpClient = new TcpClient();
            try
            {
                TcpClient.Connect(ip, 19132);
            }
            catch (SocketException ex)
            {
                _ = MessageBox.Show($"连接失败：{ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                LoginGrid.IsEnabled = true;
                return;
            }
            LoginGrid.Visibility = Visibility.Hidden;
            RoomGrid.IsEnabled = true;
            RoomGrid.Visibility = Visibility.Visible;
            Dictionary<int, string> lastMessage = new Dictionary<int, string>();
            int lastOne = 0;
            _ = ThreadPool.QueueUserWorkItem((_) =>
            {
                while (true)
                {
                    byte[] bytes = new byte[ushort.MaxValue];
                    try
                    {
                        NetworkStream stream = TcpClient.GetStream();
                        if (!stream.CanRead)
                        {
                            continue;
                        }
                        _ = stream.Read(bytes, 0, bytes.Length);
                    }
                    catch (IOException ex)
                    {
                        _ = Dispatcher.Invoke((Action)(() =>
                        {
                            ChatBox.Text += $"{Environment.NewLine}已断开连接：{ex.Message}";
                            SendButton.IsEnabled = false;
                        }));
                        int count = 0;
                        while (!TcpClient.Connected)
                        {
                            TcpClient = new TcpClient();
                            _ = Dispatcher.Invoke((Action)(() =>
                            {
                                ip = IPBox.Text;
                                ChatBox.Text += $"{Environment.NewLine}重连中：{++count}";
                                ChatBox.ScrollToEnd();
                            }));
                            try
                            {
                                TcpClient.Connect(ip, 19132);
                                break;
                            }
                            catch (SocketException ex1)
                            {
                                _ = Dispatcher.Invoke((Action)(() =>
                                {
                                    ChatBox.Text += $"{Environment.NewLine}重连失败：{ex1.Message}";
                                    ChatBox.ScrollToEnd();
                                }));
                            }
                            Thread.Sleep(1000);
                        }
                        _ = Dispatcher.Invoke((Action)(() =>
                        {
                            ChatBox.Text += $"{Environment.NewLine}已重连";
                            ChatBox.ScrollToEnd();
                            SendButton.IsEnabled = true;
                        }));
                        continue;
                    }
                    string receivedString = Encoding.UTF8.GetString(bytes).Replace("\0", string.Empty);
                    if (string.IsNullOrEmpty(receivedString))
                    {
                        continue;
                    }
                    Response data = JsonConvert.DeserializeObject<Response>(receivedString);
                    if (lastMessage.ContainsKey(data.UUID) && lastMessage[data.UUID] == data.Message)
                    {
                        continue;
                    }
                    _ = Dispatcher.Invoke((Action)(() =>
                    {
                        if (!string.IsNullOrEmpty(ChatBox.Text))
                        {
                            ChatBox.Text += Environment.NewLine;
                        }
                        if (data.UUID != lastOne)
                        {
                            if (!string.IsNullOrEmpty(ChatBox.Text))
                            {
                                ChatBox.Text += Environment.NewLine;
                            }
                            ChatBox.Text += $"{data.UserName}（{data.UUID}） ";
                        }
                        ChatBox.Text += $"{data.DateTime}{Environment.NewLine}{data.Message}";
                        ChatBox.ScrollToEnd();
                    }));
                    lastMessage[data.UUID] = data.Message;
                    lastOne = data.UUID;
                }
            });
        }

        private void Send(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(InputBox.Text))
            {
                return;
            }
            NetworkStream stream = TcpClient.GetStream();
            if (!stream.CanWrite)
            {
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new Request()
            {
                Message = InputBox.Text,
                UserName = NameBox.Text
            }));
            stream.Write(bytes, 0, bytes.Length);
            InputBox.Text = string.Empty;
        }

        private void EnterButtonDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter && SendButton.IsEnabled)
            {
                Send(default, default);
            }
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                ChatBox.Width = e.NewSize.Width - 36 < 0 ? 0 : e.NewSize.Width - 36;
                InputBox.Width = e.NewSize.Width - 141 < 0 ? 0 : e.NewSize.Width - 141;
            }
            if (e.HeightChanged)
            {
                ChatBox.Height = e.NewSize.Height - 140 < 0 ? 0 : e.NewSize.Height - 140;
            }
        }
    }
}