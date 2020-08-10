using DxgiScreenCapture;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        readonly ScreenCapture _screenCapture;

        public Form1()
        {
            InitializeComponent();
            _screenCapture = new ScreenCapture(_viewBox, new System.Drawing.Rectangle(200, 200, 500, 500));
            _screenCapture.Captured += _screenCapture_Captured;
        }

        private void _screenCapture_Captured(object sender, CapturedEventArgs e)
        {
            IntPtr srcPtr = e.Data.DataPointer;

            // 캡처한 이미지의 BGR 데이터 추출
            for (int i = 0; i < e.Data.Length; i += 4)
            {
                byte b = Marshal.ReadByte(srcPtr);
                byte g = Marshal.ReadByte(srcPtr + 1);
                byte r = Marshal.ReadByte(srcPtr + 2);
            }

            // BMP 데이터로 저장
            // System.Drawing.Bitmap bitmap = e.GetAsBitmap();
            // bitmap.Save("test.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _screenCapture.Dispose();
            base.OnFormClosing(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Cancel) // Ctrl + C
            {
                _screenCapture.SignalToCapture();
            }

            base.OnKeyPress(e);
        }
    }
}
