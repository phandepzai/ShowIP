using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics; // cho Process
using System.Text.RegularExpressions; // cho Regex





namespace ShowIP
{
    public partial class MainForm : Form
    {
        private Point pcInfoLocation;
        private Point netInfoLocation;
        private Timer refreshTimer;
        private ToolTip toolTip;
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private string draggingLabel = null;
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
            InitializeComponent();
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

            // Mặc định vị trí
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            pcInfoLocation = new Point(screen.Width - 400, 50);
            netInfoLocation = new Point(screen.Width - 400, 150);

            // Tooltip
            toolTip = new ToolTip();
            toolTip.SetToolTip(this, "@Nông Văn Phấn");

            // Timer refresh
            refreshTimer = new Timer();
            refreshTimer.Interval = 30000;
            refreshTimer.Tick += (s, e) => this.Invalidate();
            refreshTimer.Start();

            LoadConfig();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Vẽ PC Info
            TextRenderer.DrawText(
                e.Graphics,
                GetPCInfo(),
                new Font("Roboto", 13, FontStyle.Bold),
                pcInfoLocation,
                ColorTranslator.FromHtml("#FFF"),
                Color.Transparent,
                TextFormatFlags.Left | TextFormatFlags.WordBreak
            );

            // Vẽ Network Info
            TextRenderer.DrawText(
                e.Graphics,
                GetNetworkInfo(),
                new Font("Roboto", 13, FontStyle.Bold),
                netInfoLocation,
                ColorTranslator.FromHtml("#FFF"),
                Color.Transparent,
                TextFormatFlags.Left | TextFormatFlags.WordBreak
            );
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (IsPointInTextArea(e.Location, pcInfoLocation, GetPCInfo()))
                {
                    dragging = true;
                    draggingLabel = "PCInfo";
                    dragCursorPoint = Cursor.Position;
                    dragFormPoint = pcInfoLocation;
                }
                else if (IsPointInTextArea(e.Location, netInfoLocation, GetNetworkInfo()))
                {
                    dragging = true;
                    draggingLabel = "NetInfo";
                    dragCursorPoint = Cursor.Position;
                    dragFormPoint = netInfoLocation;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                if (draggingLabel == "PCInfo")
                    pcInfoLocation = Point.Add(dragFormPoint, new Size(diff));
                else if (draggingLabel == "NetInfo")
                    netInfoLocation = Point.Add(dragFormPoint, new Size(diff));
                this.Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            draggingLabel = null;
            SaveConfig();
        }

        private bool IsPointInTextArea(Point point, Point textLocation, string text)
        {
            using (Graphics g = this.CreateGraphics())
            {
                Size textSize = TextRenderer.MeasureText(g, text, new Font("Segoe UI", 14, FontStyle.Bold));
                Rectangle textRect = new Rectangle(textLocation, textSize);
                return textRect.Contains(point);
            }
        }


        // --- PC INFO ---
        private string GetPCInfo()
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("❖❖❖ PC Information ❖❖❖");

            // Device Name
            string deviceName = Environment.MachineName;
            //string userName = Environment.UserName;
            info.AppendLine($"Device Name: {deviceName}");
            //info.AppendLine($"User Name: {userName}");

            // Windows + Build
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


            // CPU
            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.AppendLine($"CPU: {obj["Name"]}");
                    break;
                }
            }

            // RAM
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

            // VGA
            using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    info.AppendLine($"VGA: {obj["Name"]}");
                }
            }

            // SSD / HDD
            using (var searcher = new ManagementObjectSearcher("SELECT Model, MediaType FROM Win32_DiskDrive"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string model = obj["Model"]?.ToString();
                    string mediaType = obj["MediaType"]?.ToString();

                    if (!string.IsNullOrEmpty(mediaType) && mediaType.ToLower().Contains("ssd"))
                        info.AppendLine($"SSD: {model}");
                    else
                        info.AppendLine($"HDD: {model}");
                }
            }

            return info.ToString();
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

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                    ni.OperationalStatus == OperationalStatus.Up)
                {
                    string mac = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());

                    foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            //string remoteHost = ResolveHostName(addr.Address.ToString());

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

        // lấy IP và MAC của máy local
        private string GetLocalIPv4(out string macAddress)
        {
            macAddress = "N/A";
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
                            return addr.Address.ToString();
                        }
                    }
                }
            }
            return "N/A";
        }



        // --- CONFIG ---
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
                    writer.WriteLine($"X={pcInfoLocation.X}");
                    writer.WriteLine($"Y={pcInfoLocation.Y}");
                    writer.WriteLine("[NetInfo]");
                    writer.WriteLine($"X={netInfoLocation.X}");
                    writer.WriteLine($"Y={netInfoLocation.Y}");
                }
            }
            catch { }
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
                                    if (line.StartsWith("X=")) pcInfoLocation = new Point(val, pcInfoLocation.Y);
                                    else pcInfoLocation = new Point(pcInfoLocation.X, val);
                                }
                                else if (section == "[NetInfo]")
                                {
                                    if (line.StartsWith("X=")) netInfoLocation = new Point(val, netInfoLocation.Y);
                                    else netInfoLocation = new Point(netInfoLocation.X, val);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
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
