using SharpDX;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DxgiScreenCapture
{
    public class DXGIManager : IDisposable
    {
        Factory4 _factory;
        List<DXGIOutputDuplication> _outputs;
        readonly CaptureSource _captureSource;
        Rectangle _outputRect;

        public bool Initialized
        {
            get
            {
                return _outputs != null && _outputs.Count != 0;
            }
        }

        public int FrameSize
        {
            get { return _outputRect.Width * _outputRect.Height * 4; }
        }

        public int Width
        {
            get { return _outputRect.Width; }
        }

        public int Height
        {
            get { return _outputRect.Height; }
        }

        public DXGIManager(CaptureSource captureSource)
        {
            _captureSource = captureSource;
            _factory = new Factory4();
            _outputs = new List<DXGIOutputDuplication>();

            foreach (Adapter adapter in _factory.Adapters)
            {
                List<Output> outputs = new List<Output>();

                foreach (Output output in adapter.Outputs)
                {
                    OutputDescription desc = output.Description;
                    if (desc.IsAttachedToDesktop == false)
                    {
                        continue;
                    }

                    outputs.Add(output);
                }

                if (outputs.Count == 0)
                {
                    continue;
                }

                SharpDX.Direct3D11.Device device = new SharpDX.Direct3D11.Device(adapter);

                try
                {
                    foreach (Output output in outputs)
                    {
                        using (Output1 output1 = output.QueryInterface<Output1>())
                        {
                            OutputDuplication outputDuplication = output1.DuplicateOutput(device);

                            if (outputDuplication == null)
                            {
                                continue;
                            }

                            _outputs.Add(
                                new DXGIOutputDuplication(adapter, device, outputDuplication, output1.Description));
                        }
                    }
                }
                catch (SharpDXException e)
                {
                    // desktop이 바뀐 후 복원되었을 때 곧바로 output1.DuplicateOutput을 호출하는 경우
                    // 일부 output에서 E_ACCESS_DENIED가 발생함.
                    if (e.ResultCode == DXGIError.E_ACCESS_DENIED)
                    {
                        Dispose();
                    }
                }
            }

            if (this.Initialized == true)
            {
                CalcOutputRect();
            }
        }

        public bool Capture(Action<IntPtr, int, Rectangle> copyFrameBuffer, int timeout)
        {
            foreach (DXGIOutputDuplication dupOutput in GetOutputDuplicationByCaptureSource())
            {
                Rectangle desktopBounds = dupOutput.DesktopCoordinates;
                if (dupOutput.AcquireNextFrame(timeout, copyBuffer, copyFrameBuffer) == false)
                {
                    return false;
                }
            }

            return true;
        }

        private void copyBuffer(Surface1 surface1, Rectangle desktopBounds, Action<IntPtr, int, Rectangle> copyFrameBuffer)
        {
            if (surface1 == null)
            {
                return;
            }

            DataRectangle map = surface1.Map(MapFlags.Read);
            IntPtr srcPtr = map.DataPointer;

            Rectangle offsetBounds = desktopBounds;
            offsetBounds.Offset(-this._outputRect.Left, -this._outputRect.Top);

            try
            {
                copyFrameBuffer(srcPtr, map.Pitch, offsetBounds);
            }
            catch { }

            surface1.Unmap();
        }

        private void CalcOutputRect()
        {
            Rectangle rcShare = new Rectangle();

            foreach (DXGIOutputDuplication dupOutput in GetOutputDuplicationByCaptureSource())
            {
                Rectangle desktopBounds = dupOutput.DesktopCoordinates;
                rcShare = UnionRect(rcShare, desktopBounds);
            }

            _outputRect = rcShare;
        }

        private static Rectangle UnionRect(Rectangle rect1, Rectangle rect2)
        {
            if (rect1.Width == 0 || rect1.Height == 0)
            {
                if (rect2.Width == 0 || rect2.Height == 0)
                {
                    return Rectangle.Empty;
                }

                return rect2;
            }

            if (rect2.Width == 0 || rect2.Height == 0)
            {
                return rect1;
            }

            return Rectangle.Union(rect1, rect2);
        }

        private List<DXGIOutputDuplication> GetOutputDuplicationByCaptureSource()
        {
            List<DXGIOutputDuplication> list = new List<DXGIOutputDuplication>();
            int nthMonitor = 0;

            foreach (DXGIOutputDuplication output in _outputs)
            {
                switch (_captureSource)
                {
                    case CaptureSource.Monitor1:
                        if (output.IsPrimary() == true)
                        {
                            list.Add(output);
                        }
                        break;

                    case CaptureSource.Monitor2:
                        if (output.IsPrimary() == false)
                        {
                            list.Add(output);
                        }
                        break;

                    case CaptureSource.Monitor3:
                        if (output.IsPrimary() == false)
                        {
                            nthMonitor++;
                        }

                        if (nthMonitor == ((int)CaptureSource.Monitor3) - 1)
                        {
                            list.Add(output);
                        }
                        break;

                    case CaptureSource.Desktop:
                        list.Add(output);
                        break;
                }

                if (_captureSource != CaptureSource.Desktop && list.Count == 1)
                {
                    break;
                }
            }

            return list;
        }

        public void Dispose()
        {
            ReleaseResource();
        }

        void ReleaseResource()
        {
            if (_outputs != null)
            {
                foreach (DXGIOutputDuplication output in _outputs)
                {
                    output.Dispose();
                }

                _outputs = null;
            }

            if (_factory != null)
            {
                _factory.Dispose();
                _factory = null;
            }
        }
    }
}
