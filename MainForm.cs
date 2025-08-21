using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management; // để dùng WMI
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;


namespace ShowIP
{
    public partial class MainForm : Form
    {
        private Label lblPCInfo;
        private Label lblNetInfo;
        private Timer refreshTimer;
        private ToolTip toolTip;

        // kéo thả
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private Label draggingLabel = null;

        // Đường dẫn AppData ẩn
        private string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShowIP", "config.ini");

        // --- Hotkey ---
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
            SetupUI();

            // Đăng ký hotkey Ctrl + Shift + Q để thoát
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.Q);
        }

        private void SetupUI()
        {
            // Form overlay
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;

            // Label PC Info
            lblPCInfo = new Label()
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#BD6B09"),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(lblPCInfo);

            // Label Network Info
            lblNetInfo = new Label()
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#BD6B09"),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(lblNetInfo);

            // Tooltip
            toolTip = new ToolTip();
            toolTip.SetToolTip(lblPCInfo, "@Nông Văn Phấn");
            toolTip.SetToolTip(lblNetInfo, "@Nông Văn Phấn");

            // Cho phép kéo thả
            EnableDrag(lblPCInfo);
            EnableDrag(lblNetInfo);

            // ⚡ Mặc định đặt ở bên phải màn hình
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            lblPCInfo.Location = new Point(screen.Width - 400, 50);
            lblNetInfo.Location = new Point(screen.Width - 400, lblPCInfo.Bottom + 40);

            // Load config nếu có
            LoadConfig();

            // Timer refresh
            refreshTimer = new Timer();
            refreshTimer.Interval = 5000;
            refreshTimer.Tick += (s, e) => RefreshInfo();
            refreshTimer.Start();

            RefreshInfo();
        }


        private void EnableDrag(Label label)
        {
            label.MouseDown += (s, e) =>
            {
                dragging = true;
                draggingLabel = label;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = label.Location;
            };
            label.MouseMove += (s, e) =>
            {
                if (dragging && draggingLabel != null)
                {
                    Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                    draggingLabel.Location = Point.Add(dragFormPoint, new Size(diff));
                }
            };
            label.MouseUp += (s, e) =>
            {
                dragging = false;
                draggingLabel = null;
                SaveConfig();
            };
        }

        private void RefreshInfo()
        {
            lblPCInfo.Text = GetPCInfo();
            lblNetInfo.Text = GetNetworkInfo();

            Rectangle screen = Screen.PrimaryScreen.Bounds;

            if (!File.Exists(configPath))
            {
                // Tính chiều rộng lớn nhất
                int maxWidth = Math.Max(lblPCInfo.Width, lblNetInfo.Width);

                // Căn lề phải, lấy cùng 1 Left cho cả 2 label
                int left = screen.Width - maxWidth - 20;

                lblPCInfo.Location = new Point(left, 50);
                lblNetInfo.Location = new Point(left, lblPCInfo.Bottom + 40);
            }
            else
            {
                // Tránh tràn màn hình nếu vị trí đã lưu quá rộng
                if (lblPCInfo.Right > screen.Width)
                    lblPCInfo.Left = screen.Width - lblPCInfo.Width - 20;

                if (lblNetInfo.Right > screen.Width)
                    lblNetInfo.Left = screen.Width - lblNetInfo.Width - 20;
            }
        }



        // --- PC INFO ---
        private string GetPCInfo()
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("❖❖❖ PC Information ❖❖❖");

            // Device Name
            string deviceName = Environment.MachineName;
            string userName = Environment.UserName;
            info.AppendLine($"Device Name: {deviceName}");
            info.AppendLine($"User Name: {userName}");

            // Windows + Build
            using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption = obj["Caption"]?.ToString();
                    string version = obj["Version"]?.ToString();
                    string build = obj["BuildNumber"]?.ToString();

                    info.AppendLine($"Windows: {caption} ({version} Build {build})");
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
                // Thử DNS lookup
                var entry = Dns.GetHostEntry(ipAddress);
                if (!string.IsNullOrEmpty(entry.HostName))
                    return entry.HostName;
            }
            catch { }

            try
            {
                // Nếu DNS không được thì thử ARP
                Process p = new Process();
                p.StartInfo.FileName = "arp";
                p.StartInfo.Arguments = "-a";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();

                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                string pattern = $@"\s*{ipAddress}\s+([a-f0-9:-]+)";
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
                            string remoteHost = ResolveHostName(addr.Address.ToString());

                            sb.AppendLine($"Interface: {ni.Name}");
                            sb.AppendLine($"Connected To: {remoteHost}");
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
                    writer.WriteLine($"X={lblPCInfo.Location.X}");
                    writer.WriteLine($"Y={lblPCInfo.Location.Y}");

                    writer.WriteLine("[NetInfo]");
                    writer.WriteLine($"X={lblNetInfo.Location.X}");
                    writer.WriteLine($"Y={lblNetInfo.Location.Y}");
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
                                    if (line.StartsWith("X=")) lblPCInfo.Location = new Point(val, lblPCInfo.Location.Y);
                                    else lblPCInfo.Location = new Point(lblPCInfo.Location.X, val);
                                }
                                else if (section == "[NetInfo]")
                                {
                                    if (line.StartsWith("X=")) lblNetInfo.Location = new Point(val, lblNetInfo.Location.Y);
                                    else lblNetInfo.Location = new Point(lblNetInfo.Location.X, val);
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
