using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxgiScreenCapture
{
    public enum CaptureSource
    {
        Undefined,
        Monitor1,
        Monitor2,
        Monitor3,
        Desktop
    };

    public static class DXGIError
    {
        public const uint E_ACCESS_DENIED = 0x80070005;
        public const uint DXGI_ERROR_ACCESS_LOST = 0x887A0026;
        public const uint DXGI_ERROR_WAIT_TIMEOUT = 0x887A0027;
    }
}
