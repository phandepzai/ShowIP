using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;


namespace Show_Infomation
{
    #region CODE ỨNG DỤNG HIỂN THỊ THÔNG TIN PC VÀ MẠNG
    public class DraggableText
    {
        public string Text { get; set; }
        public Point Location { get; set; }
        public Font Font { get; set; }
        public Color Color { get; set; }
        public bool IsDragging { get; set; }
        public Point DragStartPoint { get; set; }
        public Point DragStartLocation { get; set; }
        // Thêm thuộc tính mới
        public Color OutlineColor { get; set; } // Màu viền chữ
        public float OutlineWidth { get; set; } // Độ dày viền
        public Color BackgroundColor { get; set; } // Màu nền bán trong suốt
        public int Padding { get; set; } // Khoảng cách lề cho nền

        public DraggableText(string text, Point location, Font font, Color color,
            Color outlineColor, float outlineWidth, Color backgroundColor, int padding)
        {
            Text = text;
            Location = location;
            Font = font;
            Color = color;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
            BackgroundColor = backgroundColor;
            Padding = padding;
            IsDragging = false;
        }

        public bool ContainsPoint(Graphics g, Point point)
        {
            Size textSize = GetTextSize(g);
            if (textSize.IsEmpty) return false;

            // Tăng kích thước vùng click để bao gồm cả padding
            Rectangle textRect = new Rectangle(
                Location.X - Padding,
                Location.Y - Padding,
                textSize.Width + 2 * Padding,
                textSize.Height + 2 * Padding);
            return textRect.Contains(point);
        }

        public void Draw(Graphics g)
        {
            if (g == null || string.IsNullOrEmpty(Text)) return;

            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; // Chống nhòe chữ
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Size textSize = GetTextSize(g);
            if (textSize.IsEmpty) return;

            // Vẽ nền bán trong suốt
            if (BackgroundColor != Color.Empty)
            {
                using (Brush bgBrush = new SolidBrush(BackgroundColor))
                {
                    Rectangle bgRect = new Rectangle(
                        Location.X - Padding,
                        Location.Y - Padding,
                        textSize.Width + 2 * Padding,
                        textSize.Height + 2 * Padding);
                    g.FillRectangle(bgBrush, bgRect);
                }
            }

            // Vẽ viền chữ
            if (OutlineWidth > 0 && OutlineColor != Color.Empty)
            {
                using (GraphicsPath path = new GraphicsPath())
                using (Pen outlinePen = new Pen(OutlineColor, OutlineWidth))
                {
                    path.AddString(
                        Text,
                        Font.FontFamily,
                        (int)Font.Style,
                        Font.Size,
                        Location,
                        StringFormat.GenericDefault);
                    g.DrawPath(outlinePen, path);
                }
            }

            // Vẽ văn bản chính
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

        public Size GetTextSize(Graphics g)
        {
            if (string.IsNullOrEmpty(Text) || g == null)
                return Size.Empty;

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
            return new Size(maxWidth, totalHeight);
        }
    }


    public partial class MainForm : Form
    {
        private DraggableText pcInfoText;
        private DraggableText netInfoText;
        private Timer refreshTimer;
        private ToolTip toolTip;
        private readonly string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Show IP", "config.ini");

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

            // Thêm lời gọi kiểm tra cập nhật
            string exeName = "Show IP.exe"; // Thay bằng tên thực tế của file thực thi
            string[] updateServers = new[]
            {
                "http://107.125.221.79:8888/update/ShowIP/",
                "http://107.126.41.111:8888/update/ShowIP/" // Thay bằng URL server của bạn
            };
            UpdateManager.CheckForUpdates(exeName, updateServers);

            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (int)Keys.Q);
        }

        // Cập nhật SetupUI để tính toán vị trí Y cho netInfoText
        private void SetupUI()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;

            Font textFont = new Font("Arial", 11F, FontStyle.Bold);
            Color textColor = ColorTranslator.FromHtml("#00ff00"); // Màu chữ sáng
            Color outlineColor = Color.Black; // Viền đen
            float outlineWidth = 1.5f; // Độ dày viền
            Color backgroundColor = Color.FromArgb(128, 0, 0, 0); // Nền đen bán trong suốt
            int padding = 5; // Khoảng cách lề

            Rectangle screen = Screen.PrimaryScreen.Bounds;

            // Khởi tạo PC Info
            pcInfoText = new DraggableText(
                GetPCInfo(),
                new Point(screen.Width - 400, 50),
                textFont,
                textColor,
                outlineColor,
                outlineWidth,
                backgroundColor,
                padding
            );

            // Tính toán vị trí Y cho Network Info
            int netInfoY = 150;
            using (Graphics g = this.CreateGraphics())
            {
                int pcInfoHeight = pcInfoText.GetTextSize(g).Height;
                netInfoY = pcInfoText.Location.Y + pcInfoHeight + 10 + 2 * padding;
            }

            // Khởi tạo Network Info
            netInfoText = new DraggableText(
                GetNetworkInfo(),
                new Point(screen.Width - 400, netInfoY),
                textFont,
                textColor,
                outlineColor,
                outlineWidth,
                backgroundColor,
                padding
            );

            toolTip = new ToolTip();
            refreshTimer = new Timer
            {
                Interval = 30000
            };
            refreshTimer.Tick += (s, e) => RefreshInfo();
            refreshTimer.Start();

            LoadConfig();

            // Thêm kiểm tra cập nhật
            string exeName = "Show IP.exe"; // Thay bằng tên file thực thi thực tế
            string[] updateServers = new[]
            {
                "http://107.125.221.79:8888/update/ShowIP/",
                "http://107.126.41.111:8888/update/ShowIP/"  // Thay bằng URL server thực tế
            };
            UpdateManager.CheckForUpdates(exeName, updateServers);
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

        private string GetPCInfo()
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("❖PC Information❖");

            // Device Name
            string deviceName = Environment.MachineName;
            info.AppendLine($"Device Name: {deviceName}");

            // Windows + Build
            try
            {
                var osVersion = Environment.OSVersion;
                string windowsVersion = GetWindowsVersion();
                info.AppendLine($"OS: {windowsVersion} ({osVersion.Version} Build {Environment.OSVersion.Version.Build})");
            }
            catch
            {
                info.AppendLine("OS: Unknown");
            }

            // CPU
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results.Cast<ManagementObject>())
                    {
                        using (obj)
                        {
                            info.AppendLine($"CPU: {obj["Name"]}");
                            break;
                        }
                    }
                }
            }
            catch
            {
                info.AppendLine("CPU: Unknown");
            }

            // RAM
            try
            {
                ulong totalMemory = GetTotalPhysicalMemory();
                if (totalMemory > 0)
                {
                    double totalGB = Math.Round(totalMemory / (1024.0 * 1024 * 1024), 1);
                    info.AppendLine($"RAM: {totalGB} GB");
                }
                else
                {
                    info.AppendLine($"RAM: Unknown");
                }
            }
            catch
            {
                info.AppendLine("RAM: Unknown");
            }

            // VGA
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results.Cast<ManagementObject>())
                    {
                        using (obj)
                        {
                            info.AppendLine($"VGA: {obj["Name"]}");
                        }
                    }
                }
            }
            catch
            {
                info.AppendLine("VGA: Unknown");
            }

            // Ổ CỨNG - Sử dụng WMI để lấy thông tin chính xác cho ổ >2TB
            try
            {
                string diskInfo = GetDiskDrivesInfo();
                info.Append(diskInfo);
            }
            catch (Exception ex)
            {
                info.AppendLine($"Drives: Error - {ex.Message}");
            }

            return info.ToString();
        }

        private string GetDiskDrivesInfo()
        {
            StringBuilder diskInfo = new StringBuilder();

            try
            {
                // Phương án 1: Sử dụng WMI để lấy thông tin ổ vật lý
                using (var searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject drive in results.Cast<ManagementObject>())
                    {
                        using (drive)
                        {
                            string model = drive["Model"]?.ToString()?.Trim() ?? "Unknown Model";
                            string mediaType = drive["MediaType"]?.ToString() ?? "Unknown";
                            ulong sizeBytes = 0;

                            // Xử lý dung lượng lớn với ulong
                            if (drive["Size"] != null)
                            {
                                try
                                {
                                    sizeBytes = Convert.ToUInt64(drive["Size"]);
                                }
                                catch
                                {
                                    // Thử parse như string nếu convert trực tiếp thất bại
                                    ulong.TryParse(drive["Size"].ToString(), out sizeBytes);
                                }
                            }

                            string sizeFormatted = FormatDiskSize(sizeBytes);
                            string driveType = GetDriveTypeFromMediaType(mediaType);

                            diskInfo.AppendLine($"{driveType}: {model} ({sizeFormatted})");
                        }
                    }
                }

                // Phương án 2: Hiển thị thông tin ổ logic (partition)
                diskInfo.AppendLine("--- Logical Drives ---");
                string logicalDrives = GetLogicalDrivesInfo();
                diskInfo.Append(logicalDrives);
            }
            catch (Exception ex)
            {
                diskInfo.AppendLine($"Thông tin đĩa bị lỗi: {ex.Message}");
            }

            return diskInfo.ToString();
        }

        private string FormatDiskSize(ulong bytes)
        {
            if (bytes == 0) return "0 GB";

            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = (decimal)bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            // Hiển thị 2 số thập phân cho TB và PB
            string format = (counter >= 4) ? "N2" : "N0";
            return $"{number.ToString(format)} {suffixes[counter]}";
        }
        private string GetLogicalDrivesInfo()
        {
            StringBuilder logicalInfo = new StringBuilder();

            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo drive in allDrives)
                {
                    if (drive.IsReady)
                    {
                        try
                        {
                            // Sử dụng ulong để tránh overflow với ổ >2TB
                            ulong totalSize = (ulong)drive.TotalSize;
                            ulong availableFreeSpace = (ulong)drive.AvailableFreeSpace;
                            ulong usedSpace = totalSize - availableFreeSpace;

                            double usedPercent = totalSize > 0 ?
                                (double)usedSpace / totalSize * 100 : 0;

                            string totalFormatted = FormatDiskSize(totalSize);
                            string freeFormatted = FormatDiskSize(availableFreeSpace);

                            logicalInfo.AppendLine(
                                $"{drive.Name} {drive.VolumeLabel} - " +
                                $"{freeFormatted} free of {totalFormatted} " +
                                $"(Used: {usedPercent:F1}%)");
                        }
                        catch (Exception ex)
                        {
                            logicalInfo.AppendLine($"{drive.Name} - Error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logicalInfo.AppendLine($"Logical drives error: {ex.Message}");
            }

            return logicalInfo.ToString();
        }
        private string GetDriveTypeFromMediaType(string mediaType)
        {
            if (string.IsNullOrEmpty(mediaType)) return "Disk";

            mediaType = mediaType.ToLower();
            if (mediaType.Contains("ssd") || mediaType.Contains("solid")) return "SSD";
            if (mediaType.Contains("hdd") || mediaType.Contains("disk")) return "HDD";
            if (mediaType.Contains("nvme")) return "NVMe";
            if (mediaType.Contains("flash") || mediaType.Contains("usb")) return "USB";

            return "Disk";
        }

        private ulong GetTotalPhysicalMemory()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results.Cast<ManagementObject>())
                    {
                        using (obj)
                        {
                            if (obj["TotalPhysicalMemory"] != null)
                            {
                                return Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting RAM info: {ex.Message}");
            }

            // Return giá trị mặc định nếu có lỗi
            return 0;
        }

        private string GetWindowsVersion()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results.Cast<ManagementObject>())
                    {
                        using (obj)
                        {
                            if (obj["Caption"] != null)
                            {
                                return obj["Caption"].ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting OS info: {ex.Message}");
            }

            // Return giá trị mặc định nếu có lỗi
            return "Unknown Windows Version";
        }

        // --- NETWORK INFO ---
        private string GetNetworkInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("»Network Information«");
            string localUser = Environment.UserName;

            // [THAY ĐỔI QUAN TRỌNG]: Cập nhật lời gọi GetLocalIPv4 và thêm dòng hiển thị
            string localIP = GetLocalIPv4(out string localMac, out string localInterfaceName);

            sb.AppendLine($"Local User: {localUser}");
            sb.AppendLine($"Local Interface: {localInterfaceName}"); // Tên cổng mạng kết nối đến internet hiện tại
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
                            //sb.AppendLine($"Connected To: {remoteHost}"); //Hiển thị tên host đang kết nối
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

        // [THAY ĐỔI QUAN TRỌNG]: Cập nhật chữ ký GetLocalIPv4 để trả về tên giao diện mạng
        private string GetLocalIPv4(out string macAddress, out string interfaceName)
        {
            macAddress = "N/A";
            interfaceName = "N/A"; // Thêm tham số out mới để lấy tên giao diện
            string preferredIp = "N/A";
            string defaultIp = "N/A";

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Chỉ xét các giao diện Ethernet đang hoạt động
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                    ni.OperationalStatus == OperationalStatus.Up)
                {
                    string currentMac = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());
                    string currentInterfaceName = ni.Name;

                    foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = addr.Address.ToString();
                            // Kiểm tra dải IP ưu tiên
                            if (ip.StartsWith("107.125.") || ip.StartsWith("107.126.") || ip.StartsWith("107.115."))
                            {
                                preferredIp = ip;
                                macAddress = currentMac; // Cập nhật MAC và Interface Name chính thức
                                interfaceName = currentInterfaceName;
                                // Nếu tìm thấy IP ưu tiên, ta có đủ thông tin và có thể thoát
                                return preferredIp;
                            }
                            else if (defaultIp == "N/A") // Gán IP mặc định (cho giao diện đầu tiên tìm thấy)
                            {
                                defaultIp = ip;
                                macAddress = currentMac;
                                interfaceName = currentInterfaceName;
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
    #endregion
}