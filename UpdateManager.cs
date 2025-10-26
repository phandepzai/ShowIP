using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

#region THÔNG BÁO PHIÊN BẢN MỚI
public static class UpdateManager
{
    // 🔹 Gọi hàm kiểm tra cập nhật
    public static async void CheckForUpdates(string exeName, string[] updateServers)
    {
        try
        {
            string currentVersion = Application.ProductVersion;
            string latestVersion = null;
            string changelog = "";
            string workingServer = null;

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                foreach (var server in updateServers)
                {
                    try
                    {
                        string versionUrl = server + "version.txt";
                        latestVersion = (await client.GetStringAsync(versionUrl)).Trim();
                        workingServer = server;
                        break;
                    }
                    catch { }
                }

                if (workingServer == null)
                    return;

                if (string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    try { changelog = await client.GetStringAsync(workingServer + "changelog.txt"); } catch { }
                    ShowUpdatePrompt(latestVersion, changelog, workingServer, exeName);
                }
            }
        }
        catch { }
    }

    // 🔹 Hiển thị form mini có bo tròn + đổ bóng
    private static void ShowUpdatePrompt(string latestVersion, string changelog, string workingServer, string exeName)
    {
        int cornerRadius = 20;
        var updateForm = new Form
        {
            Text = "Cập nhật phần mềm",
            Size = new Size(400, 230), // Tăng chiều cao để chứa đủ nội dung
            StartPosition = FormStartPosition.Manual,
            FormBorderStyle = FormBorderStyle.None,
            TopMost = true,
            BackColor = Color.White,
            Icon = Application.OpenForms.Count > 0 ? Application.OpenForms[0].Icon : SystemIcons.Application
        };
        updateForm.Location = new Point(
            Screen.PrimaryScreen.WorkingArea.Right - updateForm.Width - 20,
            Screen.PrimaryScreen.WorkingArea.Bottom - updateForm.Height - 20
        );

        // Bo góc form
        IntPtr hRgn = CreateRoundRectRgn(0, 0, updateForm.Width, updateForm.Height, cornerRadius, cornerRadius);
        updateForm.Region = Region.FromHrgn(hRgn);

        // Hiệu ứng đổ bóng
        int val = 2;
        DwmSetWindowAttribute(updateForm.Handle, 2, ref val, 4);
        MARGINS margins = new MARGINS() { cxLeftWidth = 1, cxRightWidth = 1, cyTopHeight = 1, cyBottomHeight = 1 };
        DwmExtendFrameIntoClientArea(updateForm.Handle, ref margins);

        // Viền nhẹ quanh form
        updateForm.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(128, Color.LightGray)))
            {
                pen.Width = 1f;
                e.Graphics.DrawRectangle(pen, 0.5f, 0.5f, updateForm.Width - 1, updateForm.Height - 1);
            }
        };

        // Icon bên trái
        var picIcon = new PictureBox
        {
            Size = new Size(40, 40),
            Location = new Point(20, 20),
            Image = SystemIcons.Shield.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        // Lấy tên ứng dụng (bỏ phần mở rộng .exe)
        string appName = exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? exeName.Substring(0, exeName.Length - 4)
            : exeName;

        // Label hiển thị tên phiên bản mới (chữ đậm)
        var lblVersion = new Label
        {
            Text = $"{appName} đã có phiên bản mới: {latestVersion}",
            Location = new Point(70, 25),
            Width = updateForm.Width - 90,
            Height = 20,
            TextAlign = ContentAlignment.TopLeft,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        // RichTextBox hiển thị changelog (có cuộn)
        var rtbChangelog = new RichTextBox
        {
            Text = changelog,
            Location = new Point(70, 50),
            Width = updateForm.Width - 90,
            Height = 100, // Chiều cao cố định, nếu nội dung dài sẽ tự động cuộn
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9),
            ScrollBars = RichTextBoxScrollBars.Vertical, // Cho phép cuộn dọc
            ReadOnly = true, // Không cho phép chỉnh sửa
            WordWrap = true, // Tự động xuống dòng
        };

        // Panel chứa nút (đảm bảo không bị đè)
        var panelButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.White
        };

        // Nút "Cập nhật"
        var btnUpdate = new Button
        {
            Text = "Cập nhật",
            Width = 90,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            BackColor = Color.FromArgb(0, 120, 0),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnUpdate.FlatAppearance.BorderSize = 0;
        btnUpdate.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 255);
        btnUpdate.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, btnUpdate.Width, btnUpdate.Height, 12, 12));

        // Nút "Bỏ qua"
        var btnSkip = new Button
        {
            Text = "Bỏ qua",
            Width = 90,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            BackColor = Color.FromArgb(200, 200, 200),
            ForeColor = Color.Black,
            Cursor = Cursors.Hand
        };
        btnSkip.FlatAppearance.BorderSize = 0;
        btnSkip.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
        btnSkip.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, btnSkip.Width, btnSkip.Height, 12, 12));

        // Đặt vị trí cho các nút
        btnUpdate.Location = new Point(70, 10);
        btnSkip.Location = new Point(panelButtons.Width - btnSkip.Width - 70, 10);
        btnSkip.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Thêm sự kiện
        btnSkip.Click += (s, e) => updateForm.Close();
        btnUpdate.Click += async (s, e) =>
        {
            btnUpdate.Enabled = false;
            btnSkip.Enabled = false;
            await DownloadAndUpdateAsync(workingServer, exeName, btnUpdate, updateForm);
        };

        // Thêm các control vào form
        panelButtons.Controls.Add(btnUpdate);
        panelButtons.Controls.Add(btnSkip);

        updateForm.Controls.Add(picIcon);
        updateForm.Controls.Add(lblVersion);
        updateForm.Controls.Add(rtbChangelog);
        updateForm.Controls.Add(panelButtons);

        updateForm.Show();
    }

    // 🔹 Tải và cập nhật
    private static async Task DownloadAndUpdateAsync(string workingServer, string exeName, Button btnUpdate, Form updateForm)
    {
        try
        {
            using (var dlClient = new HttpClient())
            using (var response = await dlClient.GetAsync(workingServer + exeName, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = total != -1;

                string tempFile = Path.Combine(Path.GetTempPath(), exeName.Replace(".exe", "_Update.exe"));
                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (canReportProgress)
                        {
                            int percent = (int)(totalRead * 100 / total);
                            btnUpdate.Text = $"Đang tải... {percent}%";
                            btnUpdate.Refresh();
                        }
                    }
                }

                string currentExe = Application.ExecutablePath;
                string appDir = Application.StartupPath;
                string newExePath = Path.Combine(appDir, Path.GetFileName(currentExe));
                string oldExePath = newExePath + ".old";
                string batFile = Path.Combine(Path.GetTempPath(), "update.bat");

                string batContent = $@"
                @echo off
                timeout /t 1 > nul
                :loop
                tasklist | find /i ""{Path.GetFileName(currentExe)}"" >nul
                if not errorlevel 1 (
                    timeout /t 1 > nul
                    goto loop
                )

                :: Xóa file .old cũ (nếu có) trước khi tạo file .old mới
                if exist ""{oldExePath}"" del /f /q ""{oldExePath}""

                :: Đổi tên file EXE cũ thành .old
                rename ""{currentExe}"" ""{Path.GetFileName(oldExePath)}""

                :: Copy file EXE mới (từ tempFile) vào vị trí file chạy chính
                copy /y ""{tempFile}"" ""{currentExe}""
                del /f /q ""{tempFile}""

                :: Khởi động ứng dụng mới
                start """" ""{currentExe}""

                :: 🚨 THÊM: Xoá TẤT CẢ các file có đuôi .old trong thư mục hiện tại
                DEL /f /q ""*.old""

                exit
                ";

                await WriteAllTextAsyncCompat(batFile, batContent, System.Text.Encoding.UTF8);

                btnUpdate.Text = "Cập nhật xong ✔";
                await Task.Delay(800);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C start \"\" \"{batFile}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                updateForm.Close();
                Application.Exit();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể tải bản cập nhật: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Task WriteAllTextAsyncCompat(string path, string contents, System.Text.Encoding encoding)
    {
        return Task.Run(() => File.WriteAllText(path, contents, encoding));
    }

    // 🪟 Win32 API
    [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
        int nWidthEllipse, int nHeightEllipse);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }
}
#endregion