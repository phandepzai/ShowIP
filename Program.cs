using System;
using System.Windows.Forms;
using System.Threading; // Thêm using này để sử dụng Mutex

namespace Show_Infomation
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Tạo Mutex với tên unique (global)
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "ShowInformationSingleInstanceMutex", out createdNew))
            {
                if (createdNew)
                {
                    // Nếu Mutex mới được tạo (chưa có instance nào chạy), tiếp tục chạy ứng dụng
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm());
                }
                else
                {
                    // Nếu Mutex đã tồn tại (instance khác đang chạy), hiển thị thông báo và thoát
                    MessageBox.Show(
                        "Ứng dụng đã đang chạy. Không thể mở thêm phiên bản mới.",
                        "Thông báo",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    // Thoát ứng dụng ngay lập tức
                    Environment.Exit(0);
                }
            }
        }
    }
}
