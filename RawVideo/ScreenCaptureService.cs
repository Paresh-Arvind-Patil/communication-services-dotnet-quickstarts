﻿using Azure.Communication.Calling.WindowsClient;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;

namespace CallingTestApp
{
    internal class ScreenCaptureService : CaptureService
    {
        private GraphicsCaptureItem captureItem;
        private Direct3D11CaptureFramePool framePool;
        private CanvasDevice canvasDevice;
        private GraphicsCaptureSession session;

        public ScreenCaptureService(RawOutgoingVideoStream rawOutgoingVideoStream, 
            GraphicsCaptureItem captureItem) : 
            base(rawOutgoingVideoStream)
        {
            this.captureItem = captureItem;
        }

        public void Start()
        {
            canvasDevice = new CanvasDevice();

            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(canvasDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                captureItem.Size);
            framePool.FrameArrived += FrameArrived;

            session = framePool.CreateCaptureSession(captureItem);
            session.StartCapture();
        }

        private new async void FrameArrived(Direct3D11CaptureFramePool framePool, object sender)
        {
            using (Direct3D11CaptureFrame direct3D11VideoFrame = framePool.TryGetNextFrame())
            {
                if (direct3D11VideoFrame != null)
                {
                    IDirect3DSurface surface = direct3D11VideoFrame.Surface;
                    SoftwareBitmap bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);

                    await SendRawVideoFrame(bitmap);
                }
            }
        }

        public void Stop()
        {
            if (framePool != null)
            {
                framePool.FrameArrived -= FrameArrived;
            }

            session?.Dispose();
            session = null;

            framePool?.Dispose();
            framePool = null;
        }

        public static List<GraphicsCaptureItem> GetDisplayList()
        {
            DisplayId[] displayIds = DisplayServices.FindAll();
            var displayList = new List<GraphicsCaptureItem>();

            foreach (DisplayId displayId in displayIds)
            {
                try
                {
                    GraphicsCaptureItem captureItem = GraphicsCaptureItem.TryCreateFromDisplayId(displayId);
                    displayList.Add(captureItem);
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                }
            }

            return displayList.OrderBy(item => item.DisplayName).ToList();
        }
    }
}
