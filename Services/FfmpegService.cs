using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using TORQUE.Models;

namespace TORQUE.Services;

public sealed class FfmpegService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FfmpegService(string appBaseDirectory)
    {
        var toolRoot = Path.Combine(appBaseDirectory, "tools", "ffmpeg", "bin");
        _ffmpegPath = Path.Combine(toolRoot, "ffmpeg.exe");
        _ffprobePath = Path.Combine(toolRoot, "ffprobe.exe");
    }

    public bool IsAvailable => File.Exists(_ffmpegPath) && File.Exists(_ffprobePath);

    public async Task<TimeSpan> GetDurationAsync(string inputPath)
    {
        EnsureAvailable();

        var output = await RunProcessForTextAsync(_ffprobePath, new[]
        {
            "-v",
            "error",
            "-show_entries",
            "format=duration",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            inputPath,
        });

        if (!double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(Math.Max(0, seconds));
    }

    public async Task<(int Width, int Height)> GetVideoDimensionsAsync(string inputPath)
    {
        EnsureAvailable();

        var output = await RunProcessForTextAsync(_ffprobePath, new[]
        {
            "-v",
            "error",
            "-select_streams",
            "v:0",
            "-show_entries",
            "stream=width,height",
            "-of",
            "csv=p=0:s=x",
            inputPath,
        });

        var parts = output
            .Trim()
            .Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return (1920, 1080);
        }

        return (Math.Max(1, width), Math.Max(1, height));
    }

    public async Task CreateImageThumbnailAsync(MediaFileItem item, string outputPath)
    {
        EnsureAvailable();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var filters = new List<string>();
        var cropFilter = BuildCropFilter(item);
        if (!string.IsNullOrWhiteSpace(cropFilter))
        {
            filters.Add(cropFilter);
        }

        filters.Add("scale=320:320:force_original_aspect_ratio=decrease");
        filters.Add("pad=320:320:(ow-iw)/2:(oh-ih)/2:color=0x242424");

        await RunProcessAsync(_ffmpegPath, new[]
        {
            "-y",
            "-i",
            item.SourcePath,
            "-vf",
            string.Join(",", filters),
            "-frames:v",
            "1",
            outputPath,
        });
    }

    public async Task CreateAudioWaveformAsync(string inputPath, string outputPath, string? waveformColorHex)
    {
        EnsureAvailable();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var waveformColor = NormalizeColorForFfmpeg(waveformColorHex, AppInfo.DefaultThemeTextColorHex);

        await RunProcessAsync(_ffmpegPath, new[]
        {
            "-y",
            "-i",
            inputPath,
            "-filter_complex",
            $"color=c=0x1f1f1f:s=1400x360[bg];[0:a]aformat=channel_layouts=mono,showwavespic=s=1400x360:colors={waveformColor}[fg];[bg][fg]overlay=format=auto",
            "-frames:v",
            "1",
            outputPath,
        });
    }

    public async Task CreateVideoPreviewAsync(MediaFileItem item, string outputPath, TimeSpan time, bool includeCrop)
    {
        EnsureAvailable();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var filters = new List<string>();
        var cropFilter = includeCrop ? BuildCropFilter(item) : null;
        if (!string.IsNullOrWhiteSpace(cropFilter))
        {
            filters.Add(cropFilter);
        }

        if (includeCrop)
        {
            filters.Add("scale=960:540:force_original_aspect_ratio=decrease");
            filters.Add("pad=960:540:(ow-iw)/2:(oh-ih)/2:color=0x242424");
        }
        else
        {
            var previewWidth = Math.Max(1, (int)Math.Round(item.EditorCanvasWidth));
            var previewHeight = Math.Max(1, (int)Math.Round(item.EditorCanvasHeight));
            filters.Add($"scale={previewWidth}:{previewHeight}");
        }

        await RunProcessAsync(_ffmpegPath, new[]
        {
            "-y",
            "-ss",
            FormatFfmpegTime(time),
            "-i",
            item.SourcePath,
            "-frames:v",
            "1",
            "-vf",
            string.Join(",", filters),
            outputPath,
        });
    }

    public async Task<IReadOnlyList<string>> CreateVideoTimelineAsync(MediaFileItem item, string targetFolder, int frameCount)
    {
        EnsureAvailable();
        Directory.CreateDirectory(targetFolder);

        var frames = new List<string>();
        var count = Math.Max(4, frameCount);
        var durationSeconds = Math.Max(item.Duration.TotalSeconds, 1);

        for (var index = 0; index < count; index++)
        {
            var timestamp = TimeSpan.FromSeconds(durationSeconds * ((index + 0.5) / count));
            var outputPath = Path.Combine(targetFolder, $"frame-{index:D2}.png");

            await RunProcessAsync(_ffmpegPath, new[]
            {
                "-y",
                "-ss",
                FormatFfmpegTime(timestamp),
                "-i",
                item.SourcePath,
                "-frames:v",
                "1",
                "-vf",
                "scale=220:124:force_original_aspect_ratio=increase,crop=220:124",
                outputPath,
            });

            frames.Add(outputPath);
        }

        return frames;
    }

    public async Task ConvertImageAsync(MediaFileItem item, string outputPath, string outputFormat, int compressionPercent, bool removeMetadata)
    {
        EnsureAvailable();

        var arguments = new List<string>
        {
            "-y",
            "-i",
            item.SourcePath,
            "-frames:v",
            "1",
        };

        var cropFilter = BuildCropFilter(item);
        if (!string.IsNullOrWhiteSpace(cropFilter))
        {
            arguments.Add("-vf");
            arguments.Add(cropFilter);
        }

        switch (outputFormat.ToLowerInvariant())
        {
            case "jpg":
            case "jpeg":
                arguments.AddRange(new[]
                {
                    "-q:v",
                    MapJpegQuality(compressionPercent).ToString(CultureInfo.InvariantCulture),
                });
                break;
            case "png":
                AppendPngEncodingArguments(arguments, compressionPercent);
                break;
            case "webp":
                arguments.AddRange(new[]
                {
                    "-q:v",
                    Math.Round((100 - compressionPercent) * 0.63d).ToString(CultureInfo.InvariantCulture),
                });
                break;
        }

        AppendMetadataRemovalArguments(arguments, removeMetadata);
        arguments.Add(outputPath);
        await RunProcessAsync(_ffmpegPath, arguments);
    }

    public async Task ConvertStillImageFileAsync(string inputPath, string outputPath, string outputFormat, int compressionPercent, bool removeMetadata)
    {
        EnsureAvailable();

        var arguments = new List<string>
        {
            "-y",
            "-i",
            inputPath,
            "-frames:v",
            "1",
        };

        switch (outputFormat.ToLowerInvariant())
        {
            case "jpg":
            case "jpeg":
                arguments.AddRange(new[]
                {
                    "-q:v",
                    MapJpegQuality(compressionPercent).ToString(CultureInfo.InvariantCulture),
                });
                break;
            case "png":
                AppendPngEncodingArguments(arguments, compressionPercent);
                break;
            case "webp":
                arguments.AddRange(new[]
                {
                    "-q:v",
                    Math.Round((100 - compressionPercent) * 0.63d).ToString(CultureInfo.InvariantCulture),
                });
                break;
        }

        AppendMetadataRemovalArguments(arguments, removeMetadata);
        arguments.Add(outputPath);
        await RunProcessAsync(_ffmpegPath, arguments);
    }

    public async Task ConvertAudioAsync(MediaFileItem item, string outputPath, string outputFormat, bool removeMetadata)
    {
        EnsureAvailable();
        var arguments = CreateTrimmedInputArguments(item);
        arguments.AddRange(new[] { "-map", "0:a:0", "-vn", "-sn", "-dn" });

        switch (outputFormat.ToLowerInvariant())
        {
            case "mp3":
                arguments.AddRange(new[] { "-codec:a", "libmp3lame", "-q:a", "2" });
                break;
            case "wav":
                arguments.AddRange(new[] { "-codec:a", "pcm_s16le" });
                break;
            case "flac":
                arguments.AddRange(new[] { "-codec:a", "flac" });
                break;
            case "ogg":
                arguments.AddRange(new[] { "-codec:a", "libvorbis", "-q:a", "5" });
                break;
            case "aac":
                arguments.AddRange(new[] { "-codec:a", "aac", "-b:a", "192k" });
                break;
            case "m4a":
                arguments.AddRange(new[] { "-codec:a", "aac", "-b:a", "192k" });
                break;
            case "aiff":
                arguments.AddRange(new[] { "-codec:a", "pcm_s16be" });
                break;
            default:
                arguments.AddRange(new[] { "-codec:a", "copy" });
                break;
        }

        AppendMetadataRemovalArguments(arguments, removeMetadata);
        arguments.Add(outputPath);
        await RunProcessAsync(_ffmpegPath, arguments);
    }

    public async Task ConvertVideoAsync(MediaFileItem item, string outputPath, string outputFormat, int resolutionDivisor, double playbackSpeedMultiplier, bool removeMetadata)
    {
        EnsureAvailable();

        if (outputFormat.Equals("mp3", StringComparison.OrdinalIgnoreCase) ||
            outputFormat.Equals("wav", StringComparison.OrdinalIgnoreCase))
        {
            await ConvertVideoToAudioAsync(item, outputPath, outputFormat, playbackSpeedMultiplier, removeMetadata);
            return;
        }

        if (outputFormat.Equals("gif", StringComparison.OrdinalIgnoreCase))
        {
            await ConvertVideoToGifAsync(item, outputPath, resolutionDivisor, playbackSpeedMultiplier, removeMetadata);
            return;
        }

        // When playback speed changes, trim against source time before filtering.
        // Otherwise ffmpeg can interpret -t as output duration and the export runs longer than the preview.
        var arguments = Math.Abs(playbackSpeedMultiplier - 1d) > 0.001d
            ? CreateInputTrimmedArguments(item)
            : CreateTrimmedInputArguments(item);
        var videoFilters = BuildVideoConversionFilters(item, resolutionDivisor, playbackSpeedMultiplier);

        if (videoFilters.Count > 0)
        {
            arguments.Add("-vf");
            arguments.Add(string.Join(",", videoFilters));
        }

        var audioTempoFilter = BuildAudioTempoFilter(playbackSpeedMultiplier);
        if (!string.IsNullOrWhiteSpace(audioTempoFilter) && await HasAudioStreamAsync(item.SourcePath))
        {
            arguments.Add("-af");
            arguments.Add(audioTempoFilter);
        }

        switch (outputFormat.ToLowerInvariant())
        {
            case "webm":
                arguments.AddRange(new[] { "-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "31", "-c:a", "libopus", "-b:a", "128k" });
                break;
            case "avi":
                arguments.AddRange(new[] { "-c:v", "mpeg4", "-q:v", "4", "-c:a", "libmp3lame", "-q:a", "3" });
                break;
            default:
                arguments.AddRange(new[] { "-c:v", "libx264", "-preset", "medium", "-crf", "21", "-c:a", "aac", "-b:a", "192k" });
                if (outputFormat.Equals("mp4", StringComparison.OrdinalIgnoreCase) ||
                    outputFormat.Equals("mov", StringComparison.OrdinalIgnoreCase))
                {
                    arguments.AddRange(new[] { "-movflags", "+faststart" });
                }

                break;
        }

        AppendMetadataRemovalArguments(arguments, removeMetadata);
        arguments.Add(outputPath);
        await RunProcessAsync(_ffmpegPath, arguments);
    }

    private async Task ConvertVideoToAudioAsync(MediaFileItem item, string outputPath, string outputFormat, double playbackSpeedMultiplier, bool removeMetadata)
    {
        if (!await HasAudioStreamAsync(item.SourcePath))
        {
            throw new InvalidOperationException("This video does not contain an audio track to export.");
        }

        var arguments = Math.Abs(playbackSpeedMultiplier - 1d) > 0.001d
            ? CreateInputTrimmedArguments(item)
            : CreateTrimmedInputArguments(item);

        arguments.AddRange(new[] { "-map", "0:a:0", "-vn", "-sn", "-dn" });

        var audioTempoFilter = BuildAudioTempoFilter(playbackSpeedMultiplier);
        if (!string.IsNullOrWhiteSpace(audioTempoFilter))
        {
            arguments.Add("-af");
            arguments.Add(audioTempoFilter);
        }

        switch (outputFormat.ToLowerInvariant())
        {
            case "mp3":
                arguments.AddRange(new[] { "-codec:a", "libmp3lame", "-q:a", "2" });
                break;
            case "wav":
                arguments.AddRange(new[] { "-codec:a", "pcm_s16le" });
                break;
            default:
                throw new InvalidOperationException($"Unsupported audio-only video export format '{outputFormat}'.");
        }

        AppendMetadataRemovalArguments(arguments, removeMetadata);
        arguments.Add(outputPath);
        await RunProcessAsync(_ffmpegPath, arguments);
    }

    private async Task ConvertVideoToGifAsync(MediaFileItem item, string outputPath, int resolutionDivisor, double playbackSpeedMultiplier, bool removeMetadata)
    {
        var gifFilters = BuildVideoConversionFilters(item, resolutionDivisor, playbackSpeedMultiplier);
        gifFilters.Add("setsar=1");
        gifFilters.Add("fps=12");

        var baseFilter = string.Join(",", gifFilters);
        var palettePath = Path.Combine(Path.GetTempPath(), $"torque-gif-palette-{Guid.NewGuid():N}.png");

        try
        {
            var paletteArguments = CreateInputTrimmedArguments(item);
            paletteArguments.AddRange(new[]
            {
                "-an",
                "-vf",
                $"{baseFilter},palettegen=max_colors=256:reserve_transparent=0",
                palettePath,
            });

            await RunProcessAsync(_ffmpegPath, paletteArguments);

            var gifArguments = CreateInputTrimmedArguments(item);
            gifArguments.Add("-i");
            gifArguments.Add(palettePath);
            gifArguments.AddRange(new[]
            {
                "-filter_complex",
                $"[0:v]{baseFilter}[x];[x][1:v]paletteuse=dither=sierra2_4a[gif]",
                "-map",
                "[gif]",
                "-an",
                "-loop",
                "0",
            });
            AppendMetadataRemovalArguments(gifArguments, removeMetadata);
            gifArguments.Add(outputPath);

            await RunProcessAsync(_ffmpegPath, gifArguments);
        }
        finally
        {
            try
            {
                if (File.Exists(palettePath))
                {
                    File.Delete(palettePath);
                }
            }
            catch
            {
                // Temporary palette cleanup failure should not fail the export.
            }
        }
    }

    private List<string> CreateTrimmedInputArguments(MediaFileItem item)
    {
        var arguments = new List<string>
        {
            "-y",
            "-i",
            item.SourcePath,
        };

        if (item.Duration > TimeSpan.Zero && (item.TrimStart > TimeSpan.Zero || item.TrimEnd < item.Duration))
        {
            arguments.Add("-ss");
            arguments.Add(FormatFfmpegTime(item.TrimStart));
            arguments.Add("-t");
            arguments.Add(FormatFfmpegTime(item.TrimEnd - item.TrimStart));
        }

        return arguments;
    }

    private List<string> CreateInputTrimmedArguments(MediaFileItem item)
    {
        var arguments = new List<string>
        {
            "-y",
        };

        if (item.Duration > TimeSpan.Zero && (item.TrimStart > TimeSpan.Zero || item.TrimEnd < item.Duration))
        {
            arguments.Add("-ss");
            arguments.Add(FormatFfmpegTime(item.TrimStart));
            arguments.Add("-t");
            arguments.Add(FormatFfmpegTime(item.TrimEnd - item.TrimStart));
        }

        arguments.Add("-i");
        arguments.Add(item.SourcePath);
        return arguments;
    }

    private static string? BuildCropFilter(MediaFileItem item)
    {
        if (item.CropLeftRatio <= 0d &&
            item.CropTopRatio <= 0d &&
            item.CropRightRatio >= 1d &&
            item.CropBottomRatio >= 1d)
        {
            return null;
        }

        return FormattableString.Invariant(
            $"crop=iw*{item.CropWidthRatio:0.######}:ih*{item.CropHeightRatio:0.######}:iw*{item.CropLeftRatio:0.######}:ih*{item.CropTopRatio:0.######}");
    }

    private static List<string> BuildVideoConversionFilters(MediaFileItem item, int resolutionDivisor, double playbackSpeedMultiplier)
    {
        var filters = new List<string>();
        var cropFilter = BuildCropFilter(item);
        if (!string.IsNullOrWhiteSpace(cropFilter))
        {
            filters.Add(cropFilter);
        }

        if (resolutionDivisor > 1)
        {
            filters.Add($"scale=trunc(iw/{resolutionDivisor}/2)*2:trunc(ih/{resolutionDivisor}/2)*2");
        }

        if (Math.Abs(playbackSpeedMultiplier - 1d) > 0.001d)
        {
            filters.Add($"setpts=PTS/{playbackSpeedMultiplier.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        return filters;
    }

    private static void AppendMetadataRemovalArguments(List<string> arguments, bool removeMetadata)
    {
        if (!removeMetadata)
        {
            return;
        }

        arguments.AddRange(new[]
        {
            "-map_metadata",
            "-1",
            "-map_chapters",
            "-1",
        });
    }

    private async Task<bool> HasAudioStreamAsync(string inputPath)
    {
        EnsureAvailable();

        var output = await RunProcessForTextAsync(_ffprobePath, new[]
        {
            "-v",
            "error",
            "-select_streams",
            "a:0",
            "-show_entries",
            "stream=codec_type",
            "-of",
            "default=noprint_wrappers=1:nokey=1",
            inputPath,
        });

        return !string.IsNullOrWhiteSpace(output);
    }

    private static string? BuildAudioTempoFilter(double playbackSpeedMultiplier)
    {
        if (Math.Abs(playbackSpeedMultiplier - 1d) <= 0.001d)
        {
            return null;
        }

        var filters = new List<string>();
        var remainingSpeed = playbackSpeedMultiplier;

        while (remainingSpeed > 2.0d)
        {
            filters.Add("atempo=2.0");
            remainingSpeed /= 2.0d;
        }

        while (remainingSpeed < 0.5d)
        {
            filters.Add("atempo=0.5");
            remainingSpeed /= 0.5d;
        }

        filters.Add($"atempo={remainingSpeed.ToString("0.###", CultureInfo.InvariantCulture)}");
        return string.Join(",", filters);
    }

    private static int MapJpegQuality(int compressionPercent)
    {
        var clamped = Math.Clamp(compressionPercent, 0, 100);
        var value = 2 + (clamped / 100d * 29d);
        return (int)Math.Round(value);
    }

    private static void AppendPngEncodingArguments(List<string> arguments, int compressionPercent)
    {
        var clamped = Math.Clamp(compressionPercent, 0, 100);

        // PNG is lossless, so a 0% slider value should still use strong deflate compression
        // instead of effectively writing an almost uncompressed PNG.
        var compressionLevel = clamped switch
        {
            <= 25 => 9,
            <= 60 => 8,
            _ => 9,
        };

        arguments.AddRange(new[]
        {
            "-compression_level",
            compressionLevel.ToString(CultureInfo.InvariantCulture),
            "-pred",
            "mixed",
        });
    }

    private static string FormatFfmpegTime(TimeSpan value)
    {
        return value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string NormalizeColorForFfmpeg(string? value, string fallbackHex)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallbackHex : value.Trim();

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(candidate)!;
            return $"0x{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            var fallback = (Color)ColorConverter.ConvertFromString(fallbackHex)!;
            return $"0x{fallback.R:X2}{fallback.G:X2}{fallback.B:X2}";
        }
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new FileNotFoundException("Bundled FFmpeg tools were not found. Publish output must include tools\\ffmpeg\\bin\\ffmpeg.exe and ffprobe.exe.");
        }
    }

    private static async Task RunProcessAsync(string fileName, IEnumerable<string> arguments)
    {
        _ = await RunProcessInternalAsync(fileName, arguments);
    }

    private static async Task<string> RunProcessForTextAsync(string fileName, IEnumerable<string> arguments)
    {
        var (standardOutput, errorOutput) = await RunProcessInternalAsync(fileName, arguments);
        return string.IsNullOrWhiteSpace(standardOutput) ? errorOutput : standardOutput;
    }

    private static async Task<(string StandardOutput, string StandardError)> RunProcessInternalAsync(string fileName, IEnumerable<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(standardError)
                ? $"FFmpeg exited with code {process.ExitCode}."
                : standardError);
        }

        return (standardOutput, standardError);
    }
}

