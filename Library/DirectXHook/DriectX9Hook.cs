﻿using EasyHook;
using RoeHack.Library.Core;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RoeHack.Library.DirectXHook
{
    public class Hook<TDelegate> : IDisposable where TDelegate : class
    {
        private LocalHook localHook;

        public Hook(IntPtr target, Delegate proxy, DriectX9Hook callback)
        {
            Target = Marshal.GetDelegateForFunctionPointer<TDelegate>(target);

            localHook = LocalHook.Create(target, proxy, callback);
            localHook.ThreadACL.SetInclusiveACL(new int[] { 0 });
        }

        public TDelegate Target { get; private set; }

        public void Dispose()
        {
            localHook.ThreadACL.SetExclusiveACL(new int[] { 0 });
            localHook.Dispose();
        }
    }

    public class DriectX9Hook : IDirectXHook, IDisposable
    {
        private Parameter parameter;
        private Hook<Direct3D9Device_DrawIndexedPrimitiveDelegate> drawIndexedPrimitiveHook;
        private Hook<Direct3D9Device_PresentDelegate> presentHook;

        private bool firsted = true;
        private Font font;

        public DriectX9Hook(Parameter parameter)
        {
            this.parameter = parameter;
        }


        // STDMETHOD(DrawIndexedPrimitive)(
        //     THIS_ D3DPRIMITIVETYPE,
        //     INT BaseVertexIndex,
        //     UINT MinVertexIndex,
        //     UINT NumVertices,
        //     UINT startIndex,
        //     UINT primCount
        // )
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        public delegate int Direct3D9Device_DrawIndexedPrimitiveDelegate(IntPtr devicePtr, PrimitiveType arg0, int baseVertexIndex, int minVertexIndex, int numVertices, int startIndex, int primCount);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int Direct3D9Device_SetStreamSourceDelegate(IntPtr devicePtr, uint StreamNumber, IntPtr pStreamData, uint OffsetInBytes, uint sStride);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int Direct3D9Device_SetTextureDelegate(IntPtr devicePtr, uint Sampler, IntPtr pTexture);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        unsafe delegate int Direct3D9Device_PresentDelegate(IntPtr devicePtr, SharpDX.Rectangle* pSourceRect, SharpDX.Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion);


        public void Hooking()
        {
            var address = GetAddress();

            drawIndexedPrimitiveHook = new Hook<Direct3D9Device_DrawIndexedPrimitiveDelegate>(
                address[82], new Direct3D9Device_DrawIndexedPrimitiveDelegate(DrawIndexedPrimitiveHook), this);

            unsafe
            {
                presentHook = new Hook<Direct3D9Device_PresentDelegate>(
                    address[17],
                    new Direct3D9Device_PresentDelegate(PresentHook),
                    this);
            }
        }


        private int DrawIndexedPrimitiveHook(IntPtr devicePtr, PrimitiveType arg0, int baseVertexIndex, int minVertexIndex, int numVertices, int startIndex, int primCount)
        {
            var device = new Device(devicePtr);

            if (device != null)
            {
                device.GetStreamSource(0, out var pStreamData, out var iOffsetInBytes, out var iStride);
                if (pStreamData != null)
                {
                    pStreamData.Dispose();
                }

                if (IsPlayers(iStride, iOffsetInBytes, numVertices, primCount))
                {
                    //设置墙后颜色
                    //device.SetRenderState(RenderState.Lighting, false);
                    //device.SetRenderState(RenderState.ZEnable, false);
                    //device.SetRenderState(RenderState.FillMode, FillMode.Solid);
                    //device.SetTexture(0, textureBack);
                    //drawIndexedPrimitiveHook.Target(devicePtr, arg0, baseVertexIndex, minVertexIndex, numVertices, startIndex, primCount);

                    //device.SetRenderState(RenderState.ZEnable, true);
                    //device.SetRenderState(RenderState.FillMode, FillMode.Solid);
                    //device.SetTexture(0, textureFront);
                    return ResultCode.Success.Code;
                }
            }

            return drawIndexedPrimitiveHook.Target(devicePtr, arg0, baseVertexIndex, minVertexIndex, numVertices, startIndex, primCount);
        }

        private unsafe int PresentHook(IntPtr devicePtr, SharpDX.Rectangle* pSourceRect, SharpDX.Rectangle* pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion)
        {
            //_isUsingPresent = true;
            Device device = (Device)devicePtr;

            //DoCaptureRenderTarget(device, "PresentHook");
            //SetColor(devicePtr, 1);

            if (firsted)
            {
                this.font = new Font(device, new FontDescription()
                {
                    Height = 40,
                    FaceName = "Arial",
                    Italic = false,
                    Width = 0,
                    MipLevels = 1,
                    CharacterSet = FontCharacterSet.Default,
                    OutputPrecision = FontPrecision.Default,
                    Quality = FontQuality.Antialiased,
                    PitchAndFamily = FontPitchAndFamily.Default | FontPitchAndFamily.DontCare,
                    Weight = FontWeight.Bold
                });

                firsted = false;
            }

            this.font.DrawText(null, "挂载成功", 50, 50, SharpDX.Color.Red);

            //foreach (var item in WeaponEspInfoList)
            //{
            //    this.Font.DrawText(null, item.RealDistance + "mi", (int)item.pOutX, (int)item.pOutY - 20, SharpDX.Color.Red);
            //}
            //WeaponEspInfoList.Clear();

            return presentHook.Target(devicePtr, pSourceRect, pDestRect, hDestWindowOverride, pDirtyRegion);
        }

        private bool IsPlayers(int stride, int vSize, int numVertices, int primCount)
        {
            if (stride == 72)
            {
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            drawIndexedPrimitiveHook.Dispose();
        }

        #region Moved

        private List<IntPtr> GetAddress()
        {
            var address = new List<IntPtr>();

            using (var d3d = new Direct3D())
            using (var renderForm = new Form())
            using (var device = new Device(d3d, 0, DeviceType.NullReference, IntPtr.Zero, CreateFlags.HardwareVertexProcessing, new PresentParameters() { BackBufferWidth = 1, BackBufferHeight = 1, DeviceWindowHandle = renderForm.Handle }))
            {
                IntPtr vTable = Marshal.ReadIntPtr(device.NativePointer);
                for (int i = 0; i < 119; i++)
                    address.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size));
            }

            return address;
        }

        #endregion
    }
}