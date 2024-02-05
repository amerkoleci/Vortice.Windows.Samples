// Copyright (c) Amer Koleci and contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using SharpGen.Runtime;
using System.Reflection;
using Vortice.Direct3D;
using Vortice.DXGI;

using static System.Console;
using static Vortice.Direct3D11.D3D11;

FeatureLevel[] featureLevelsWin10 =
[
    FeatureLevel.Level_12_1,
    FeatureLevel.Level_12_0
];

FeatureLevel[] featureLevelsWin8 =
[
    FeatureLevel.Level_11_1
];

FeatureLevel[] featureLevelsWin7 =
[
    FeatureLevel.Level_11_0,
    FeatureLevel.Level_10_1,
    FeatureLevel.Level_10_0
];

VideoProcessorContentDescription videoProcessorContentDescription = new VideoProcessorContentDescription()
{
    InputFrameFormat = VideoFrameFormat.InterlacedTopFieldFirst,
    InputWidth = 800,
    InputHeight = 600,
    OutputWidth = 800,
    OutputHeight = 600
};

ID3D11VideoProcessor videoProcessor;

List<FeatureLevel> featureLevels = new();
if (OperatingSystem.IsWindowsVersionAtLeast(10))
{
    featureLevels.AddRange(featureLevelsWin10);
    featureLevels.AddRange(featureLevelsWin8);
    featureLevels.AddRange(featureLevelsWin7);
}
else if (OperatingSystem.IsWindowsVersionAtLeast(8))
{
    featureLevels.AddRange(featureLevelsWin8);
    featureLevels.AddRange(featureLevelsWin7);
}
else
{
    featureLevels.AddRange(featureLevelsWin7);
}

Result result = D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.VideoSupport, featureLevels.ToArray(), out ID3D11Device? device);
if (!result.Success)
{
    WriteLine("Failed to create D3D11 Device");
    return;
}

WriteLine($"D3D11 Device created successfully [{device!.FeatureLevel}]");

ID3D11VideoDevice1 videoDevice = device.QueryInterface<ID3D11VideoDevice1>();
ID3D11VideoContext videoContext = device.ImmediateContext.QueryInterface<ID3D11VideoContext1>();
result = videoDevice.CreateVideoProcessorEnumerator(videoProcessorContentDescription, out ID3D11VideoProcessorEnumerator videoProcessorEnumerator);
if (!result.Success)
{
    WriteLine("Failed to create D3D11 Video Processor Enumerator");
    videoContext.Dispose();
    videoDevice.Dispose();
    return;
}

WriteLine($"D3D11 Video Processor Enumerator created successfully");

ID3D11VideoProcessorEnumerator1 videoProcessorEnumerator1 = videoProcessorEnumerator.QueryInterface<ID3D11VideoProcessorEnumerator1>();
videoProcessorEnumerator.Dispose();

bool supportHLG = videoProcessorEnumerator1.CheckVideoProcessorFormatConversion(Format.P010, ColorSpaceType.YcbcrStudioGhlgTopLeftP2020, Format.B8G8R8A8_UNorm, ColorSpaceType.RgbFullG22NoneP709);
bool supportHDR10Limited = videoProcessorEnumerator1.CheckVideoProcessorFormatConversion(Format.P010, ColorSpaceType.YcbcrStudioG2084TopLeftP2020, Format.B8G8R8A8_UNorm, ColorSpaceType.RgbStudioG2084NoneP2020);

VideoProcessorCaps vpCaps = videoProcessorEnumerator1.VideoProcessorCaps;

WriteLine($"=====================================================");
WriteLine($"MaxInputStreams           {vpCaps.MaxInputStreams}");
WriteLine($"MaxStreamStates           {vpCaps.MaxStreamStates}");
WriteLine($"HDR10 Limited             {(supportHDR10Limited ? "yes" : "no")}");
WriteLine($"HLG                       {(supportHLG ? "yes" : "no")}");

WriteLine($"\n[Video Processor Device Caps]");
foreach (VideoProcessorDeviceCaps cap in Enum.GetValues(typeof(VideoProcessorDeviceCaps)))
{
    WriteLine($"{cap,-25} {((vpCaps.DeviceCaps & cap) != 0 ? "yes" : "no")}");
}

WriteLine($"\n[Video Processor Feature Caps]");
foreach (VideoProcessorFeatureCaps cap in Enum.GetValues(typeof(VideoProcessorFeatureCaps)))
    WriteLine($"{cap,-25} {((vpCaps.FeatureCaps & cap) != 0 ? "yes" : "no")}");

WriteLine($"\n[Video Processor Stereo Caps]");
foreach (VideoProcessorStereoCaps cap in Enum.GetValues(typeof(VideoProcessorStereoCaps)))
{
    WriteLine($"{cap,-25} {((vpCaps.StereoCaps & cap) != 0 ? "yes" : "no")}");
}

WriteLine($"\n[Video Processor Input Format Caps]");
foreach (VideoProcessorFormatCaps cap in Enum.GetValues(typeof(VideoProcessorFormatCaps)))
{
    WriteLine($"{cap,-25} {((vpCaps.InputFormatCaps & cap) != 0 ? "yes" : "no")}");
}

WriteLine($"\n[Video Processor Filter Caps]");
foreach (VideoProcessorFilterCaps filter in Enum.GetValues(typeof(VideoProcessorFilterCaps)))
{
    if ((vpCaps.FilterCaps & filter) != 0)
    {
        videoProcessorEnumerator1.GetVideoProcessorFilterRange(ConvertFromVideoProcessorFilterCaps(filter), out VideoProcessorFilterRange range);
        WriteLine($"{filter.ToString().PadRight(25, ' ')} [{range.Minimum.ToString().PadLeft(6, ' ')} - {range.Maximum.ToString().PadLeft(4, ' ')}] | x{range.Multiplier.ToString().PadLeft(4, ' ')} | *{range.Default}");
    }
    else
    {
        WriteLine($"{filter.ToString().PadRight(25, ' ')} no");
    }
}

WriteLine($"\n[Video Processor Input Format Caps]");
foreach (VideoProcessorAutoStreamCaps cap in Enum.GetValues(typeof(VideoProcessorAutoStreamCaps)))
{
    WriteLine($"{cap.ToString().PadRight(25, ' ')} {((vpCaps.AutoStreamCaps & cap) != 0 ? "yes" : "no")}");
}

int bobRate = -1;
int lastRate = -1;

for (int i = 0; i < vpCaps.RateConversionCapsCount; i++)
{
    WriteLine($"\n[Video Processor Rate Conversion Caps #{i + 1}]");

    WriteLine($"\n\t[Video Processor Rate Conversion Caps]");
    videoProcessorEnumerator1.GetVideoProcessorRateConversionCaps(i, out VideoProcessorRateConversionCaps rcCap);
    var todo = typeof(VideoProcessorRateConversionCaps).GetFields();
    foreach (FieldInfo field in todo)
        WriteLine($"\t{field.Name.PadRight(35, ' ')} {field.GetValue(rcCap)}");

    WriteLine($"\n\t[Video Processor Processor Caps]");
    foreach (VideoProcessorProcessorCaps cap in Enum.GetValues(typeof(VideoProcessorProcessorCaps)))
        WriteLine($"\t{cap.ToString().PadRight(35, ' ')} {(((VideoProcessorProcessorCaps)rcCap.ProcessorCaps & cap) != 0 ? "yes" : "no")}");

    if (((VideoProcessorProcessorCaps)rcCap.ProcessorCaps & VideoProcessorProcessorCaps.DeinterlaceBob) != 0)
        bobRate = i;

    lastRate = i;
}

if (bobRate == -1)
{
    WriteLine("DeinterlaceBob not found");
}

int usedRate = bobRate == -1 ? lastRate : bobRate;
result = videoDevice.CreateVideoProcessor(videoProcessorEnumerator1, usedRate, out videoProcessor);
WriteLine($"\n=====================================================");
if (!result.Success)
{
    WriteLine($"Failed to create D3D11 Video Processor [#{usedRate}]");
}
else
{
    WriteLine($"D3D11 Video Processor created successfully {(bobRate != -1 ? "[bob method]" : "")}");
    videoProcessor.Dispose();
}

videoProcessorEnumerator1.Dispose();
videoContext.Dispose();
videoDevice.Dispose();

static VideoProcessorFilter ConvertFromVideoProcessorFilterCaps(VideoProcessorFilterCaps filter)
{
    return filter switch
    {
        VideoProcessorFilterCaps.Brightness => VideoProcessorFilter.Brightness,
        VideoProcessorFilterCaps.Contrast => VideoProcessorFilter.Contrast,
        VideoProcessorFilterCaps.Hue => VideoProcessorFilter.Hue,
        VideoProcessorFilterCaps.Saturation => VideoProcessorFilter.Saturation,
        VideoProcessorFilterCaps.EdgeEnhancement => VideoProcessorFilter.EdgeEnhancement,
        VideoProcessorFilterCaps.NoiseReduction => VideoProcessorFilter.NoiseReduction,
        VideoProcessorFilterCaps.AnamorphicScaling => VideoProcessorFilter.AnamorphicScaling,
        VideoProcessorFilterCaps.StereoAdjustment => VideoProcessorFilter.StereoAdjustment,
        _ => VideoProcessorFilter.StereoAdjustment,
    };
}
