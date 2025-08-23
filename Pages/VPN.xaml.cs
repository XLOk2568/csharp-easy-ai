using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Windows;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace NavigationViewExample.Pages
{
    /// <summary>
    /// Interaction logic for HomePage.xaml
    /// </summary>
    public partial class VPN : Page
    {
        public VPN()
        {
            InitializeComponent();
        }
        private int CeSuof = 0;
        private List<string> listA = new();   // 服务器信息（备注 + host:port）
        private List<int> listB = new();   // 延迟 (ms)
        private async void BtnGrab_Click(object sender, RoutedEventArgs e)
        {
            if (CeSuof == 0)
            {
                CeSuof = 1;
                string subUrl = "https://7T5Qb8YyJ4.prosubnet02.eu:8443/api/v1/client/418c720d9e91e0504c2fb28e7786b3bc";   // 订阅地址
                string infor = "";           // 最终要在界面展示的长字符串
                try
                {
                    using var http = new System.Net.Http.HttpClient();
                    http.DefaultRequestHeaders.Add("X-Verge-Client", "clash-verge");
                    http.DefaultRequestHeaders.Add("X-Sing-Compat", "mihomo/1.19.9");
                    http.DefaultRequestHeaders.Add("X-App-Variant", "ClashMeta");
                    string raw = await http.GetStringAsync(subUrl);
                    // 订阅通常是 Base64；解不出来就按原文
                    string txt;
                    try { txt = Encoding.UTF8.GetString(Convert.FromBase64String(raw.Trim())); }
                    catch { txt = raw; }
                    foreach (var l in txt.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
                    {
                        string line = l.Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        /* --- VMess --- */
                        if (line.StartsWith("vmess://"))
                        {
                            string json = Encoding.UTF8.GetString(Convert.FromBase64String(line[8..]));
                            var node = JsonSerializer.Deserialize<VmessNode>(json);
                            if (node == null) continue;
                            string hostPort = $"{node.add}:{node.port}";
                            listA.Add($"{node.ps} | {hostPort}");
                        }
                        /* --- 其它协议 --- */
                        else if (line.StartsWith("vless://") || line.StartsWith("trojan://") || line.StartsWith("ss://"))
                        {
                            var uri = new Uri(line);       // System.Uri 自动解析
                            string remark = line.Contains('#')
                                            ? Uri.UnescapeDataString(line[(line.LastIndexOf('#') + 1)..])
                                            : uri.Host;
                            string hostPort = $"{uri.Host}:{uri.Port}";
                            listA.Add($"{remark} | {hostPort}");
                        }
                        else if (line.StartsWith("anytls://"))
                        {
                            var uri = new Uri(line);  
                            string remark = line.Contains('#')
                                ? Uri.UnescapeDataString(line[(line.IndexOf('#') + 1)..])
                                : uri.Host;
                            string hostPort = $"{uri.Host}:{uri.Port}";
                            listA.Add($"{remark} | {hostPort}");
                        }
                        else if (line.StartsWith("hy://") || line.StartsWith("hy2://"))
                        {
                            var uri = new Uri(line);
                            string remark = line.Contains('#')
                                ? Uri.UnescapeDataString(line[(line.IndexOf('#') + 1)..])
                                : uri.Host;
                            string hostPort = $"{uri.Host}:{uri.Port}";
                            listA.Add($"{remark} | {hostPort}");
                        }
                    }
                    async Task<int> PingAsync(string host, int port, int timeout = 3000)
                    {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            using var s = new Socket(SocketType.Stream, ProtocolType.Tcp);
                            var t = s.ConnectAsync(host, port);
                            var done = await Task.WhenAny(t, Task.Delay(timeout));
                            if (done == t && s.Connected)
                            {
                                watch.Stop();
                                return (int)watch.ElapsedMilliseconds;
                            }
                        }
                        catch { }
                        return -1;   // 超时 失败
                    }
                    foreach (var item in listA)
                    {
                        var hp = item.Split('|')[1].Trim().Split(':');
                        int delay = await PingAsync(hp[0], int.Parse(hp[1]));
                        listB.Add(delay);
                    }
                    var sb = new StringBuilder();
                    for (int i = 0; i < listA.Count; i++)
                    {
                        sb.AppendLine($"{listA[i]}  ->  {listB[i]} ms");
                    }
                    infor = sb.ToString();
                    MessageBox.Show(infor, $"共 {listA.Count} 个节点");
                }
                catch (Exception ex)
                {
                    Infor.Text = ex.ToString();
                    MessageBox.Show(ex.ToString(), "出错了");
                }
                CeSuof = 0;
            }
        }
        record VmessNode
        {
            public string? ps { get; set; }
            public string? add { get; set; }
            public int port { get; set; }
        }
    }
}
    

