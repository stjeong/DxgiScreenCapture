using SharpDX;
using SharpDX.Direct2D1;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading;

namespace DxgiScreenCapture
{
    public class CapturedEventArgs : EventArgs
    {
        readonly DataStream _dataStream;
        public DataStream Data => _dataStream;

        public CapturedEventArgs(DataStream dataStream)
        {
            _dataStream = dataStream;
        }
    }

    public sealed class ScreenCapture : IDisposable
    {
        readonly EventWaitHandle _exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
        readonly EventWaitHandle _captureEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        readonly EventWaitHandle[] _waitSignals;
        readonly Thread _captureThread;

        readonly RenderTarget2D _renderTarget;
        readonly CaptureSource _captureSource;

        readonly BlockingCollection<SharpDX.Direct2D1.Bitmap> _queue = new BlockingCollection<SharpDX.Direct2D1.Bitmap>();

        DataStream _dataStream;
        SharpDX.Direct2D1.Bitmap _lastBitmap;
        readonly IntPtr _drawingWindowHandle;

        readonly Rectangle _clipRect;
        public event EventHandler<CapturedEventArgs> Captured;

        public ScreenCapture(System.Windows.Forms.Control controlToDraw, Rectangle clipRect, CaptureSource captureSource = CaptureSource.Monitor1)
        {
            _clipRect = clipRect;

            _captureSource = captureSource;
            _waitSignals = new[] { _exitEvent, _captureEvent };

            _drawingWindowHandle = controlToDraw.Handle;
            _renderTarget = new RenderTarget2D();

            _captureThread = new Thread(captureThreadFunc)
            {
                IsBackground = true,
            };
            _captureThread.Start(controlToDraw);

            controlToDraw.Top = 0;
            controlToDraw.Left = 0;
            controlToDraw.Width = _clipRect.Width;
            controlToDraw.Height = _clipRect.Height;
        }

        public void Dispose()
        {
            _exitEvent.Set();

            _renderTarget.Dispose();

        }

        public void SignalToCapture()
        {
            _captureEvent.Set();
        }

        void DrawToWindow()
        {
            while (_queue.Count != 0)
            {
                if (_queue.TryTake(out SharpDX.Direct2D1.Bitmap bitmap) == true)
                {
                    if (_lastBitmap != null)
                    {
                        _lastBitmap.Dispose();
                    }

                    _lastBitmap = bitmap;
                }

                if (_lastBitmap == null)
                {
                    return;
                }

                _renderTarget.Render(
                    (renderer) =>
                    {
                        renderer.DrawBitmap(_lastBitmap, 1.0f, BitmapInterpolationMode.Linear);
                    });

                Captured?.Invoke(this, new CapturedEventArgs(_dataStream));
            }
        }

        private void captureThreadFunc(object arg)
        {
            DXGIManager manager = null;
            System.Windows.Forms.Control controlToDraw = arg as System.Windows.Forms.Control;

            while (true)
            {
                using (manager = new DXGIManager(_captureSource))
                {
                    if (manager.Initialized == false)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    _renderTarget.Initialize(_drawingWindowHandle, _clipRect.Width, _clipRect.Height);

                    // Initialize 호출 후 너무 빠르게 Capture를 호출하면 (검은 색의) 빈 화면이 나올 수 있음.
                    // 필요하다면 Sleep을 호출
                    // Thread.Sleep(500);

                    try
                    {
                        CaptureLoop(manager, controlToDraw);
                        break;
                    }
                    catch (SharpDXException e)
                    {
                        if (e.ResultCode != DXGIError.DXGI_ERROR_ACCESS_LOST)
                        {
                            // 예를 들어, Ctrl + Shift + Alt 키를 눌러 데스크탑 전환을 한 경우
                            manager.Dispose();
                        }
                    }
                }
            }
        }

        private void CaptureLoop(DXGIManager manager, System.Windows.Forms.Control controlToDraw)
        {
            _dataStream = new DataStream(_clipRect.Width * _clipRect.Height * 4, true, true);

            while (true)
            {
                int signalId = EventWaitHandle.WaitAny(_waitSignals, Timeout.Infinite);

                if (signalId == 0)
                {
                    break;
                }

                if (manager.Capture(copyFrameBuffer, 1000) == true)
                {
                    SharpDX.Direct2D1.Bitmap bitmap = _renderTarget.CreateBitmap(_dataStream);
                    if (bitmap != null)
                    {
                        _queue.Add(bitmap);
                    }
                }

                if (controlToDraw.Created)
                {
                    controlToDraw.BeginInvoke((Action)(() => this.DrawToWindow()));
                }
            }
        }

        void copyFrameBuffer(IntPtr srcPtr, int srcPitch, Rectangle offsetBounds)
        {
            IntPtr dstPtr = _dataStream.PositionPointer;

            srcPtr = IntPtr.Add(srcPtr, srcPitch * _clipRect.Y + _clipRect.X * 4);

            for (int y = 0; y < _clipRect.Height; y ++)
            {
                Utilities.CopyMemory(dstPtr, srcPtr, _clipRect.Width * 4);

                srcPtr = IntPtr.Add(srcPtr, srcPitch);
                dstPtr = IntPtr.Add(dstPtr, _clipRect.Width * 4);
            }
        }

        //void copyFrameBufferBGRAtoRGBA(IntPtr srcPtr, int srcPitch, Rectangle offsetBounds)
        //{
        //    IntPtr dstPtr = _dataStream.PositionPointer;

        //    for (int y = 0; y < _renderTarget.Height; y++)
        //    {
        //        for (int x = 0; x < _renderTarget.Width; x++)
        //        {
        //            IntPtr dstPixel = dstPtr + x * 4;
        //            IntPtr srcPixel = srcPtr + x * 4;

        //            byte B = Marshal.ReadByte(srcPixel + 0);
        //            byte G = Marshal.ReadByte(srcPixel + 1);
        //            byte R = Marshal.ReadByte(srcPixel + 2);
        //            byte A = Marshal.ReadByte(srcPixel + 3);

        //            // int rgba = R | (G << 8) | (B << 16) | (A << 24);
        //            // Marshal.WriteInt32(dstPixel, rgba);

        //            Marshal.WriteByte(dstPixel + 0, R);
        //            Marshal.WriteByte(dstPixel + 1, G);
        //            Marshal.WriteByte(dstPixel + 2, B);
        //            Marshal.WriteByte(dstPixel + 3, A);
        //        }

        //        srcPtr = IntPtr.Add(srcPtr, srcPitch);
        //        dstPtr = IntPtr.Add(dstPtr, _renderTarget.Width * 4);
        //    }
        //}
    }
}
