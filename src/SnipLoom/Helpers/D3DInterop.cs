using System;
using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.DirectX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace SnipLoom.Helpers;

/// <summary>
/// COM interface for accessing DXGI interfaces from WinRT Direct3D objects.
/// This interface is implemented by IDirect3DSurface and IDirect3DDevice.
/// </summary>
[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
public interface IDirect3DDxgiInterfaceAccess
{
    IntPtr GetInterface([In] ref Guid iid);
}

/// <summary>
/// Helper class for Direct3D interop between Windows.Graphics.Capture and SharpDX
/// </summary>
public static class D3DInterop
{
    [DllImport(
        "d3d11.dll",
        EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
        SetLastError = true,
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);

    private static readonly Guid IID_ID3D11Texture2D = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
    private static readonly Guid IID_ID3D11Device = new("db6f6ddb-ac77-4e88-8253-819df9bbf140");

    /// <summary>
    /// Creates a SharpDX D3D11 device
    /// </summary>
    public static Device CreateD3DDevice()
    {
        var device = new Device(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport);
        return device;
    }

    /// <summary>
    /// Creates a WinRT IDirect3DDevice from a SharpDX device.
    /// Uses WinRT.MarshalInterface for proper .NET 8 interop.
    /// </summary>
    public static IDirect3DDevice CreateDirect3DDevice(Device d3dDevice)
    {
        // Query for the DXGI device (use Device3 for better compatibility)
        using var dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device3>();
        
        // Create the WinRT device from the DXGI device
        uint hr = CreateDirect3D11DeviceFromDXGIDevice(
            dxgiDevice.NativePointer,
            out IntPtr pUnknown);

        if (hr != 0)
        {
            Marshal.ThrowExceptionForHR((int)hr);
        }

        try
        {
            // Use WinRT.MarshalInterface for .NET 8 compatible marshaling
            var device = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(pUnknown);
            
            if (device == null)
            {
                throw new Exception("Failed to create IDirect3DDevice");
            }

            return device;
        }
        finally
        {
            Marshal.Release(pUnknown);
        }
    }

    /// <summary>
    /// Gets the underlying D3D11 texture from a Direct3DSurface.
    /// Uses direct COM cast which works with .NET's WinRT projections.
    /// </summary>
    public static Texture2D GetTextureFromSurface(IDirect3DSurface surface, Device device)
    {
        // Direct cast to the COM interface - this is the pattern used by Microsoft's official samples
        // The WinRT IDirect3DSurface implements IDirect3DDxgiInterfaceAccess
        var access = (IDirect3DDxgiInterfaceAccess)surface;
        var textureGuid = IID_ID3D11Texture2D;
        var d3dPointer = access.GetInterface(ref textureGuid);
        
        if (d3dPointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to get ID3D11Texture2D from surface");
        }
        
        return new Texture2D(d3dPointer);
    }

    /// <summary>
    /// Copies a GPU texture to a CPU-accessible byte array
    /// </summary>
    public static byte[] CopyTextureToBytes(Texture2D sourceTexture, Device device)
    {
        var desc = sourceTexture.Description;
        
        // Create staging texture for CPU read
        var stagingDesc = new Texture2DDescription
        {
            Width = desc.Width,
            Height = desc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = desc.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            OptionFlags = ResourceOptionFlags.None
        };

        using var stagingTexture = new Texture2D(device, stagingDesc);
        
        // Copy to staging texture
        device.ImmediateContext.CopyResource(sourceTexture, stagingTexture);

        // Map and read pixels
        var dataBox = device.ImmediateContext.MapSubresource(
            stagingTexture, 
            0, 
            MapMode.Read, 
            SharpDX.Direct3D11.MapFlags.None);

        try
        {
            int bytesPerPixel = 4; // BGRA
            int rowPitch = dataBox.RowPitch;
            int imageSize = desc.Width * desc.Height * bytesPerPixel;
            var result = new byte[imageSize];

            // Copy row by row to handle row pitch differences
            for (int y = 0; y < desc.Height; y++)
            {
                var sourceOffset = y * rowPitch;
                var destOffset = y * desc.Width * bytesPerPixel;
                Marshal.Copy(dataBox.DataPointer + sourceOffset, result, destOffset, desc.Width * bytesPerPixel);
            }

            return result;
        }
        finally
        {
            device.ImmediateContext.UnmapSubresource(stagingTexture, 0);
        }
    }

    /// <summary>
    /// Crops a region from a texture and returns the cropped bytes
    /// </summary>
    public static byte[] CropTexture(Texture2D sourceTexture, Device device, int x, int y, int width, int height)
    {
        var desc = sourceTexture.Description;

        // Create a smaller texture for the cropped region
        var croppedDesc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = desc.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.None,
            OptionFlags = ResourceOptionFlags.None
        };

        using var croppedTexture = new Texture2D(device, croppedDesc);

        // Copy the sub-region
        var sourceRegion = new ResourceRegion(x, y, 0, x + width, y + height, 1);
        device.ImmediateContext.CopySubresourceRegion(sourceTexture, 0, sourceRegion, croppedTexture, 0);

        // Now copy to CPU
        return CopyTextureToBytes(croppedTexture, device);
    }
}
