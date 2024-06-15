// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.WIC;

namespace Vortice.Framework;

public sealed class Image
{
    private static readonly Lazy<IWICImagingFactory> s_imagingFactory = new(() => new IWICImagingFactory());

    private Image(int width, int height, Format format, Memory<byte> data)
    {
        Width = width;
        Height = height;
        Format = format;
        Data = data;
        BytesPerPixel = format.GetBitsPerPixel() / 8;
    }

    public int Width { get; }
    public int Height { get; }
    public Format Format { get; }
    public Memory<byte> Data { get; }

    public int BytesPerPixel { get; }
    public int RowPitch => Width * BytesPerPixel;

    public static Image? FromFile(string filePath, int width = 0, int height = 0)
    {
        using FileStream stream = new(filePath, FileMode.Open);
        return FromStream(stream, width, height);
    }

    public static unsafe Image? FromStream(Stream stream, int width = 0, int height = 0)
    {
        using IWICBitmapDecoder decoder = ImagingFactory().CreateDecoderFromStream(stream);
        using IWICBitmapFrameDecode frame = decoder.GetFrame(0);

        SizeI size = frame.Size;

        // Determine format
        Guid pixelFormat = frame.PixelFormat;
        Guid convertGUID = pixelFormat;

        bool useWIC2 = true;
        Format format = PixelFormat.ToDXGIFormat(pixelFormat);
        int bpp = 0;
        if (format == Format.Unknown)
        {
            if (pixelFormat == PixelFormat.Format96bppRGBFixedPoint)
            {
                if (useWIC2)
                {
                    convertGUID = PixelFormat.Format96bppRGBFixedPoint;
                    format = Format.R32G32B32_Float;
                    bpp = 96;
                }
                else
                {
                    convertGUID = PixelFormat.Format128bppRGBAFloat;
                    format = Format.R32G32B32A32_Float;
                    bpp = 128;
                }
            }
            else
            {
                foreach (KeyValuePair<Guid, Guid> item in s_WICConvert)
                {
                    if (item.Key == pixelFormat)
                    {
                        convertGUID = item.Value;

                        format = PixelFormat.ToDXGIFormat(item.Value);
                        Debug.Assert(format != Format.Unknown);
                        bpp = PixelFormat.WICBitsPerPixel(ImagingFactory(), convertGUID);
                        break;
                    }
                }
            }

            if (format == Format.Unknown)
            {
                throw new InvalidOperationException("WICTextureLoader does not support all DXGI formats");
                //Debug.WriteLine("ERROR: WICTextureLoader does not support all DXGI formats (WIC GUID {%8.8lX-%4.4X-%4.4X-%2.2X%2.2X-%2.2X%2.2X%2.2X%2.2X%2.2X%2.2X}). Consider using DirectXTex.\n",
                //    pixelFormat.Data1, pixelFormat.Data2, pixelFormat.Data3,
                //    pixelFormat.Data4[0], pixelFormat.Data4[1], pixelFormat.Data4[2], pixelFormat.Data4[3],
                //    pixelFormat.Data4[4], pixelFormat.Data4[5], pixelFormat.Data4[6], pixelFormat.Data4[7]);
            }
        }
        else
        {
            // Convert BGRA8UNorm to RGBA8Norm
            if (pixelFormat == PixelFormat.Format32bppBGRA)
            {
                format = PixelFormat.ToDXGIFormat(PixelFormat.Format32bppRGBA);
                convertGUID = PixelFormat.Format32bppRGBA;
            }

            bpp = PixelFormat.WICBitsPerPixel(ImagingFactory(), pixelFormat);
        }

#if TODO
        if (format == Format.R32G32B32_Float)
        {
            // Special case test for optional device support for autogen mipchains for R32G32B32_FLOAT
            FormatSupport fmtSupport = Device.CheckFormatSupport(Format.R32G32B32_Float);
            if (!fmtSupport.HasFlag(FormatSupport.MipAutogen))
            {
                // Use R32G32B32A32_FLOAT instead which is required for Feature Level 10.0 and up
                convertGUID = PixelFormat.Format128bppRGBAFloat;
                format = Format.R32G32B32A32_Float;
                bpp = 128;
            }
        }

        // Verify our target format is supported by the current device
        // (handles WDDM 1.0 or WDDM 1.1 device driver cases as well as DirectX 11.0 Runtime without 16bpp format support)
        FormatSupport support = Device.CheckFormatSupport(format);
        if (!support.HasFlag(FormatSupport.Texture2D))
        {
            // Fallback to RGBA 32-bit format which is supported by all devices
            convertGUID = PixelFormat.Format32bppRGBA;
            format = Format.R8G8B8A8_UNorm;
            bpp = 32;
        } 
#endif

        int rowPitch = (size.Width * bpp + 7) / 8;
        int sizeInBytes = rowPitch * size.Height;

        byte[] pixels = new byte[sizeInBytes];

        if (width == 0)
            width = size.Width;

        if (height == 0)
            height = size.Height;

        // Load image data
        if (convertGUID == pixelFormat && size.Width == width && size.Height == height)
        {
            // No format conversion or resize needed
            frame.CopyPixels(rowPitch, pixels);
        }
        else if (size.Width != width || size.Height != height)
        {
            // Resize
            using IWICBitmapScaler scaler = ImagingFactory().CreateBitmapScaler();
            scaler.Initialize(frame, width, height, BitmapInterpolationMode.Fant);

            Guid pixelFormatScaler = scaler.PixelFormat;

            if (convertGUID == pixelFormatScaler)
            {
                // No format conversion needed
                scaler.CopyPixels(rowPitch, pixels);
            }
            else
            {
                using IWICFormatConverter converter = ImagingFactory().CreateFormatConverter();

                bool canConvert = converter.CanConvert(pixelFormatScaler, convertGUID);
                if (!canConvert)
                {
                    return null;
                }

                converter.Initialize(scaler, convertGUID, BitmapDitherType.ErrorDiffusion, null, 0, BitmapPaletteType.MedianCut);
                converter.CopyPixels(rowPitch, pixels);
            }
        }
        else
        {
            // Format conversion but no resize
            using IWICFormatConverter converter = ImagingFactory().CreateFormatConverter();

            bool canConvert = converter.CanConvert(pixelFormat, convertGUID);
            if (!canConvert)
            {
                return null;
            }

            converter.Initialize(frame, convertGUID, BitmapDitherType.ErrorDiffusion, null, 0, BitmapPaletteType.MedianCut);
            converter.CopyPixels(rowPitch, pixels);
        }

        return new(width, height, format, pixels);
    }

    public static IWICImagingFactory ImagingFactory() => s_imagingFactory.Value;

    private static readonly Dictionary<Guid, Guid> s_WICConvert = new()
    {
        // Note target GUID in this conversion table must be one of those directly supported formats (above).

        { PixelFormat.FormatBlackWhite,            PixelFormat.Format8bppGray }, // DXGI_FORMAT_R8_UNORM

        { PixelFormat.Format1bppIndexed,           PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format2bppIndexed,           PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format4bppIndexed,           PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format8bppIndexed,           PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM

        { PixelFormat.Format2bppGray,              PixelFormat.Format8bppGray }, // DXGI_FORMAT_R8_UNORM
        { PixelFormat.Format4bppGray,              PixelFormat.Format8bppGray }, // DXGI_FORMAT_R8_UNORM

        { PixelFormat.Format16bppGrayFixedPoint,   PixelFormat.Format16bppGrayHalf }, // DXGI_FORMAT_R16_FLOAT
        { PixelFormat.Format32bppGrayFixedPoint,   PixelFormat.Format32bppGrayFloat }, // DXGI_FORMAT_R32_FLOAT

        { PixelFormat.Format16bppBGR555,           PixelFormat.Format16bppBGRA5551 }, // DXGI_FORMAT_B5G5R5A1_UNORM

        { PixelFormat.Format32bppBGR101010,        PixelFormat.Format32bppRGBA1010102 }, // DXGI_FORMAT_R10G10B10A2_UNORM

        { PixelFormat.Format24bppBGR,              PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format24bppRGB,              PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format32bppPBGRA,            PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format32bppPRGBA,            PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM

        { PixelFormat.Format48bppRGB,              PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM
        { PixelFormat.Format48bppBGR,              PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM
        { PixelFormat.Format64bppBGRA,             PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM
        { PixelFormat.Format64bppPRGBA,            PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM
        { PixelFormat.Format64bppPBGRA,            PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM

        { PixelFormat.Format48bppRGBFixedPoint,    PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT
        { PixelFormat.Format48bppBGRFixedPoint,    PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT
        { PixelFormat.Format64bppRGBAFixedPoint,   PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT
        { PixelFormat.Format64bppBGRAFixedPoint,   PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT
        { PixelFormat.Format64bppRGBFixedPoint,    PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT
        { PixelFormat.Format64bppRGBHalf,          PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT
        { PixelFormat.Format48bppRGBHalf,          PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT

        { PixelFormat.Format128bppPRGBAFloat,      PixelFormat.Format128bppRGBAFloat }, // DXGI_FORMAT_R32G32B32A32_FLOAT
        { PixelFormat.Format128bppRGBFloat,        PixelFormat.Format128bppRGBAFloat }, // DXGI_FORMAT_R32G32B32A32_FLOAT
        { PixelFormat.Format128bppRGBAFixedPoint,  PixelFormat.Format128bppRGBAFloat }, // DXGI_FORMAT_R32G32B32A32_FLOAT
        { PixelFormat.Format128bppRGBFixedPoint,   PixelFormat.Format128bppRGBAFloat }, // DXGI_FORMAT_R32G32B32A32_FLOAT
        { PixelFormat.Format32bppRGBE,             PixelFormat.Format128bppRGBAFloat }, // DXGI_FORMAT_R32G32B32A32_FLOAT

        { PixelFormat.Format32bppCMYK,             PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format64bppCMYK,             PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM
        { PixelFormat.Format40bppCMYKAlpha,        PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format80bppCMYKAlpha,        PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM

        { PixelFormat.Format32bppRGB,              PixelFormat.Format32bppRGBA }, // DXGI_FORMAT_R8G8B8A8_UNORM
        { PixelFormat.Format64bppRGB,              PixelFormat.Format64bppRGBA }, // DXGI_FORMAT_R16G16B16A16_UNORM
        { PixelFormat.Format64bppPRGBAHalf,        PixelFormat.Format64bppRGBAHalf }, // DXGI_FORMAT_R16G16B16A16_FLOAT

        // We don't support n-channel formats
    };
}
