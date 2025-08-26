using System;
using System.Linq;
using System.Diagnostics; // cho Process
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions; // cho Regex
using System.Windows.Forms;





namespace ShowIP
{
    public class DraggableText
    {
        public string Text { get; set; }
        public Point Location { get; set; }
        public Font Font { get; set; }
        public Color Color { get; set; }
        public bool IsDragging { get; set; }
        public Point DragStartPoint { get; set; }
        public Point DragStartLocation { get; set; }

        public DraggableText(string text, Point location, Font font, Color color)
        {
            Text = text;
            Location = location;
            Font = font;
            Color = color;
            IsDragging = false;
        }

        public bool ContainsPoint(Graphics g, Point point)
        {
            if (string.IsNullOrEmpty(Text) || g == null)
                return false;

            string[] lines = Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            int maxWidth = 0;
            int totalHeight = 0;

            foreach (string line in lines)
            {
                Size lineSize = TextRenderer.MeasureText(g, line, Font);
                if (lineSize.Width > maxWidth)
                    maxWidth = lineSize.Width;
                totalHeight += lineSize.Height;
            }

            Rectangle textRect = new Rectangle(Location, new Size(maxWidth, totalHeight));
            return textRect.Contains(point);
        }

        public void Draw(Graphics g)
        {
            if (g != null)
            {
                TextRenderer.DrawText(
                    g,
                    Text,
                    Font,
                    Location,
                    Color,
                    Color.Transparent,
                    TextFormatFlags.Left | TextFormatFlags.WordBreak
                );
            }
        }
    }


    public partial class MainForm : Form
    {
        private DraggableText pcInfoText;
        private DraggableText netInfoText;
        private Timer refreshTimer;
        private ToolTip toolTip;
        private string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShowIP", "config.ini");

        // Hotkey
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private const int MOD_CONTROL = 0x2;
        private const int MOD_SHIFT = 0x4;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 1;

        public MainForm()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetupUI();
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.Q);
        }

        private void SetupUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;

            Font textFont = new Font("Cambria", 13, FontStyle.Bold);
            Color textColor = ColorTranslator.FromHtml("#FFF000");

            Rectangle screen = Screen.PrimaryScreen.Bounds;
            pcInfoText = new DraggableText(GetPCInfo(), new Point(screen.Width - 400, 50), textFont, textColor);
            netInfoText = new DraggableText(GetNetworkInfo(), new Point(screen.Width - 400, 150), textFont, textColor);

            toolTip = new ToolTip();
            refreshTimer = new Timer();
            refreshTimer.Interval = 30000;
            refreshTimer.Tick += (s, e) => RefreshInfo();
            refreshTimer.Start();

            LoadConfig();
        }

        private void RefreshInfo()
        {
            pcInfoText.Text = GetPCInfo();
            netInfoText.Text = GetNetworkInfo();
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            pcInfoText.Draw(e.Graphics);
            netInfoText.Draw(e.Graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                using (Graphics g = this.CreateGraphics())
                {
                    if (pcInfoText.ContainsPoint(g, e.Location))
                    {
                        pcInfoText.IsDragging = true;
                        pcInfoText.DragStartPoint = Cursor.Position;
                        pcInfoText.DragStartLocation = pcInfoText.Location;
                    }
                    else if (netInfoText.ContainsPoint(g, e.Location))
                    {
                        netInfoText.IsDragging = true;
                        netInfoText.DragStartPoint = Cursor.Position;
                        netInfoText.DragStartLocation = netInfoText.Location;
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (pcInfoText.IsDragging)
            {
                int deltaX = Cursor.Position.X - pcInfoText.DragStartPoint.X;
                int deltaY = Cursor.Position.Y - pcInfoText.DragStartPoint.Y;
                pcInfoText.Location = new Point(pcInfoText.DragStartLocation.X + deltaX, pcInfoText.DragStartLocation.Y + deltaY);
                this.Invalidate();
            }
            else if (netInfoText.IsDragging)
            {
                int deltaX = Cursor.Position.X - netInfoText.DragStartPoint.X;
                int deltaY = Cursor.Position.Y - netInfoText.DragStartPoint.Y;
                netInfoText.Location = new Point(netInfoText.DragStartLocation.X + deltaX, netInfoText.DragStartLocation.Y + deltaY);
                this.Invalidate();
            }
            else
            {
                using (Graphics g = this.CreateGraphics())
                {
                    if (pcInfoText.ContainsPoint(g, e.Location) || netInfoText.ContainsPoint(g, e.Location))
                    {
                        toolTip.SetToolTip(this, "@Nông Văn Phấn");
                    }
                    else
                    {
                        toolTip.SetToolTip(this, "");
                    }
                }
            }
        }


        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (pcInfoText.IsDragging)
            {
                pcInfoText.IsDragging = false;
                SaveConfig();
            }
            else if (netInfoText.IsDragging)
            {
                netInfoText.IsDragging = false;
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                string folder = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                using (StreamWriter writer = new StreamWriter(configPath, false))
                {
                    writer.WriteLine("[PCInfo]");
                    writer.WriteLine($"X={pcInfoText.Location.X}");
                    writer.WriteLine($"Y={pcInfoText.Location.Y}");
                    writer.WriteLine("[NetInfo]");
                    writer.WriteLine($"X={netInfoText.Location.X}");
                    writer.WriteLine($"Y={netInfoText.Location.Y}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi lưu cấu hình: {ex.Message}");
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    string section = "";
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("["))
                        {
                            section = line;
                        }
                        else if (line.StartsWith("X=") || line.StartsWith("Y="))
                        {
                            if (int.TryParse(line.Substring(2), out int val))
                            {
                                if (section == "[PCInfo]")
                                {
                                    if (line.StartsWith("X=")) pcInfoText.Location = new Point(val, pcInfoText.Location.Y);
                                    else pcInfoText.Location = new Point(pcInfoText.Location.X, val);
                                }
                                else if (section == "[NetInfo]")
                                {
                                    if (line.StartsWith("X=")) netInfoText.Location = new Point(val, netInfoText.Location.Y);
                                    else netInfoText.Location = new Point(netInfoText.Location.X, val);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tải cấu hình: {ex.Message}");
            }
        }

        // --- PC INFO ---
        private string GetPCInfo()
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("❖❖❖ PC Information ❖❖❖");
            // Device Name
            string deviceName = Environment.MachineName;
            info.AppendLine($"Device Name: {deviceName}");
            // Windows + Build
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string caption = obj["Caption"]?.ToString();
                        string version = obj["Version"]?.ToString();
                        string build = obj["BuildNumber"]?.ToString();
                        info.AppendLine($"OS: {caption} ({version} Build {build})");
                    }
                }
            }
            catch { }
            // CPU
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        info.AppendLine($"CPU: {obj["Name"]}");
                        break;
                    }
                }
            }
            catch { }
            // RAM
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        double ramBytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                        double ramMB = Math.Round(ramBytes / (1024 * 1024), 0);
                        double ramGB = Math.Round(ramBytes / (1024 * 1024 * 1024), 0);
                        info.AppendLine($"RAM: {ramMB:N0} MB ({ramGB} GB)");
                    }
                }
            }
            catch { }
            // VGA
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        info.AppendLine($"VGA: {obj["Name"]}");
                    }
                }
            }
            catch { }
            // SSD / HDD
            try
            {
                var diskInfo = new StringBuilder();
                Process p = new Process();
                p.StartInfo.FileName = "powershell.exe";
                p.StartInfo.Arguments = "Get-PhysicalDisk | Select-Object Model, MediaType";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("---") || line.Contains("Model")) continue;
                    string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string model = parts[0];
                        string mediaType = parts[1];
                        for (int i = 2; i < parts.Length - 1; i++)
                        {
                            model += " " + parts[i];
                        }
                        mediaType = parts[parts.Length - 1];
                        if (mediaType.ToLower().Contains("ssd"))
                        {
                            diskInfo.AppendLine($"SSD: {model}");
                        }
                        else if (mediaType.ToLower().Contains("hdd"))
                        {
                            diskInfo.AppendLine($"HDD: {model}");
                        }
                    }
                }
                info.Append(diskInfo.ToString());
            }
            catch (Exception ex)
            {
                info.AppendLine($"Lỗi khi lấy thông tin ổ cứng: {ex.Message}");
            }
            return info.ToString();
        }

        // --- NETWORK INFO ---
        private string GetNetworkInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("»» Network Information ««");
            string localUser = Environment.UserName;
            string localIP = GetLocalIPv4(out string localMac);
            sb.AppendLine($"Local User: {localUser}");
            sb.AppendLine($"Local IP: {localIP}");
            sb.AppendLine($"Local MAC: {localMac}");
            sb.AppendLine();
            // Lấy danh sách các giao diện mạng
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet && ni.OperationalStatus == OperationalStatus.Up)
                .ToList();
            // Sắp xếp giao diện mạng để ưu tiên các IP có dải mong muốn
            var preferredInterfaces = interfaces.Where(ni =>
                ni.GetIPProperties().UnicastAddresses.Any(addr =>
                    addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                    (addr.Address.ToString().StartsWith("107.125.") || addr.Address.ToString().StartsWith("107.126.") || addr.Address.ToString().StartsWith("107.115."))
                )).ToList();
            var otherInterfaces = interfaces.Except(preferredInterfaces).ToList();
            foreach (var ni in preferredInterfaces.Concat(otherInterfaces))
            {
                string mac = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());
                foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        // Kiểm tra nếu IP của giao diện không trùng với IP Local
                        if (addr.Address.ToString() != localIP)
                        {
                            string remoteHost = ResolveHostName(addr.Address.ToString());
                            sb.AppendLine($"Interface: {ni.Name}");
                            //sb.AppendLine($"Connected To: {remoteHost}");
                            sb.AppendLine($"IP: {addr.Address}");
                            sb.AppendLine($"MAC: {mac}");
                            sb.AppendLine();
                        }
                    }
                }
            }
            return sb.ToString();
        }

        private string ResolveHostName(string ipAddress)
        {
            try
            {
                var entry = Dns.GetHostEntry(ipAddress);
                if (!string.IsNullOrEmpty(entry.HostName))
                    return entry.HostName;
            }
            catch { }
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "arp";
                p.StartInfo.Arguments = "-a";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                string pattern = $@"\s*{ipAddress}\s+([a-f0-9-]+)";
                var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string mac = match.Groups[1].Value;
                    return $"UnknownHost (MAC: {mac})";
                }
            }
            catch { }
            return "UnknownHost";
        }

        private string GetLocalIPv4(out string macAddress)
        {
            macAddress = "N/A";
            string preferredIp = "N/A";
            string defaultIp = "N/A";
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                    ni.OperationalStatus == OperationalStatus.Up)
                {
                    macAddress = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());
                    foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = addr.Address.ToString();
                            // Kiểm tra dải IP ưu tiên
                            if (ip.StartsWith("107.125.") || ip.StartsWith("107.126.") || ip.StartsWith("107.115."))
                            {
                                preferredIp = ip;
                            }
                            else
                            {
                                defaultIp = ip;
                            }
                        }
                    }
                }
            }
            // Trả về IP ưu tiên nếu tìm thấy, ngược lại trả về IP mặc định
            return preferredIp != "N/A" ? preferredIp : defaultIp;
        }

        // --- HOTKEY ---
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                Application.Exit();
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosed(e);
        }
    }
}

