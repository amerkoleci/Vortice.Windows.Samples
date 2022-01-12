// Direct3D 11 Video Processor Enumerator Sample [https://github.com/SuRGeoNix]
using SharpGen.Runtime;
using System.Reflection;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

using static System.Console;
using static Vortice.Direct3D11.D3D11;

ID3D11Device device;

FeatureLevel[] featureLevels;
List<FeatureLevel> featureLevelsWin10 = new()
{
    FeatureLevel.Level_12_1,
    FeatureLevel.Level_12_0
};

List<FeatureLevel> featureLevelsWin8 = new()
{
    FeatureLevel.Level_11_1
};

List<FeatureLevel> featureLevelsWin7 = new()
{
    FeatureLevel.Level_11_0,
    FeatureLevel.Level_10_1,
    FeatureLevel.Level_10_0
};

ID3D11VideoDevice1 videoDevice;
ID3D11VideoContext videoContext;
ID3D11VideoProcessorEnumerator1 videoProcessorEnumerator1;
VideoProcessorContentDescription videoProcessorContentDescription = new VideoProcessorContentDescription()
{
    InputFrameFormat = VideoFrameFormat.InterlacedTopFieldFirst,
    InputWidth  = 800,
    InputHeight = 600,
    OutputWidth = 800,
    OutputHeight= 600
};

ID3D11VideoProcessor videoProcessor;

Result res;
Version osVer = Environment.OSVersion.Version;

if (osVer.Major > 6)
{
    featureLevelsWin10.AddRange(featureLevelsWin8);
    featureLevelsWin10.AddRange(featureLevelsWin7);
    featureLevels = featureLevelsWin10.ToArray();
}
else if (osVer.Major > 6 || (osVer.Major == 6 && osVer.Minor > 1))
{
    featureLevelsWin8.AddRange(featureLevelsWin7);
    featureLevels = featureLevelsWin8.ToArray();
}
else
{
    featureLevels = featureLevelsWin7.ToArray();
}

res = D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.VideoSupport, featureLevels, out device);
if (!res.Success)
{
    WriteLine("Failed to create D3D11 Device");
    return;
}

WriteLine($"D3D11 Device created successfully [{device.FeatureLevel}]");

videoDevice = device.QueryInterface<ID3D11VideoDevice1>();
videoContext = device.ImmediateContext.QueryInterface<ID3D11VideoContext1>();
res = videoDevice.CreateVideoProcessorEnumerator(videoProcessorContentDescription, out ID3D11VideoProcessorEnumerator videoProcessorEnumerator);
if (!res.Success)
{
    WriteLine("Failed to create D3D11 Video Processor Enumerator");
    videoContext.Dispose();
    videoDevice.Dispose();
    return;
}

WriteLine($"D3D11 Video Processor Enumerator created successfully");

videoProcessorEnumerator1 = videoProcessorEnumerator.QueryInterface<ID3D11VideoProcessorEnumerator1>();
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
    WriteLine($"{cap.ToString().PadRight(25, ' ')} {((vpCaps.DeviceCaps & cap) != 0 ? "yes" : "no")}");

WriteLine($"\n[Video Processor Feature Caps]");
foreach (VideoProcessorFeatureCaps cap in Enum.GetValues(typeof(VideoProcessorFeatureCaps)))
    WriteLine($"{cap.ToString().PadRight(25, ' ')} {((vpCaps.FeatureCaps & cap) != 0 ? "yes" : "no")}");

WriteLine($"\n[Video Processor Stereo Caps]");
foreach (VideoProcessorStereoCaps cap in Enum.GetValues(typeof(VideoProcessorStereoCaps)))
    WriteLine($"{cap.ToString().PadRight(25, ' ')} {((vpCaps.StereoCaps & cap) != 0 ? "yes" : "no")}");

WriteLine($"\n[Video Processor Input Format Caps]");
foreach (VideoProcessorFormatCaps cap in Enum.GetValues(typeof(VideoProcessorFormatCaps)))
    WriteLine($"{cap.ToString().PadRight(25, ' ')} {((vpCaps.InputFormatCaps & cap) != 0 ? "yes" : "no")}");

WriteLine($"\n[Video Processor Filter Caps]");
foreach (VideoProcessorFilterCaps filter in Enum.GetValues(typeof(VideoProcessorFilterCaps)))
    if ((vpCaps.FilterCaps & filter) != 0)
    {
        videoProcessorEnumerator1.GetVideoProcessorFilterRange(ConvertFromVideoProcessorFilterCaps(filter), out VideoProcessorFilterRange range);
        WriteLine($"{filter.ToString().PadRight(25, ' ')} [{range.Minimum.ToString().PadLeft(6, ' ')} - {range.Maximum.ToString().PadLeft(4, ' ')}] | x{range.Multiplier.ToString().PadLeft(4, ' ')} | *{range.Default}");
    }
    else
        WriteLine($"{filter.ToString().PadRight(25, ' ')} no");

WriteLine($"\n[Video Processor Input Format Caps]");
foreach (VideoProcessorAutoStreamCaps cap in Enum.GetValues(typeof(VideoProcessorAutoStreamCaps)))
    WriteLine($"{cap.ToString().PadRight(25, ' ')} {((vpCaps.AutoStreamCaps & cap) != 0 ? "yes" : "no")}");

int bobRate = -1;
int lastRate = -1;

for (int i = 0; i < vpCaps.RateConversionCapsCount; i++)
{
    WriteLine($"\n[Video Processor Rate Conversion Caps #{i+1}]");

    WriteLine($"\n\t[Video Processor Rate Conversion Caps]");
    videoProcessorEnumerator1.GetVideoProcessorRateConversionCaps(i, out VideoProcessorRateConversionCaps rcCap);
    var todo = typeof(VideoProcessorRateConversionCaps).GetFields();
    foreach (FieldInfo field in todo)
        WriteLine($"\t{field.Name.PadRight(35, ' ')} {field.GetValue(rcCap)}");

    WriteLine($"\n\t[Video Processor Processor Caps]");
    foreach (VideoProcessorProcessorCaps cap in Enum.GetValues(typeof(VideoProcessorProcessorCaps)))
        WriteLine($"\t{cap.ToString().PadRight(35, ' ')} {(((VideoProcessorProcessorCaps) rcCap.ProcessorCaps & cap) != 0 ? "yes" : "no")}");

    if (((VideoProcessorProcessorCaps)rcCap.ProcessorCaps & VideoProcessorProcessorCaps.DeinterlaceBob) != 0)
        bobRate = i;

    lastRate = i;
}

if (bobRate == -1)
{
    WriteLine("DeinterlaceBob not found");
}

int usedRate = bobRate == -1 ? lastRate : bobRate;
res = videoDevice.CreateVideoProcessor(videoProcessorEnumerator1, usedRate, out videoProcessor);
WriteLine($"\n=====================================================");
if (!res.Success)
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
    switch (filter)
    {
        case VideoProcessorFilterCaps.Brightness:
            return VideoProcessorFilter.Brightness;
        case VideoProcessorFilterCaps.Contrast:
            return VideoProcessorFilter.Contrast;
        case VideoProcessorFilterCaps.Hue:
            return VideoProcessorFilter.Hue;
        case VideoProcessorFilterCaps.Saturation:
            return VideoProcessorFilter.Saturation;
        case VideoProcessorFilterCaps.EdgeEnhancement:
            return VideoProcessorFilter.EdgeEnhancement;
        case VideoProcessorFilterCaps.NoiseReduction:
            return VideoProcessorFilter.NoiseReduction;
        case VideoProcessorFilterCaps.AnamorphicScaling:
            return VideoProcessorFilter.AnamorphicScaling;
        case VideoProcessorFilterCaps.StereoAdjustment:
            return VideoProcessorFilter.StereoAdjustment;

        default:
            return VideoProcessorFilter.StereoAdjustment;
    }
}
