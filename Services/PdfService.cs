using System.IO;
using Docnet.Core;
using Docnet.Core.Editors;
using Docnet.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TORQUE.Services;

public sealed class PdfService
{
    private const int ThumbnailMinDimension = 320;
    private const int ThumbnailMaxDimension = 460;

    private readonly IDocLib _docLib = DocLib.Instance;

    public Task<int> GetPageCountAsync(string inputPath)
    {
        return Task.Run(() =>
        {
            using var docReader = _docLib.GetDocReader(inputPath, new PageDimensions(1d));
            return docReader.GetPageCount();
        });
    }

    public Task<(int Width, int Height)> GetFirstPageDimensionsAsync(string inputPath)
    {
        return Task.Run(() =>
        {
            using var docReader = _docLib.GetDocReader(inputPath, new PageDimensions(1d));
            if (docReader.GetPageCount() == 0)
            {
                return (1080, 1528);
            }

            using var pageReader = docReader.GetPageReader(0);
            return (Math.Max(1, pageReader.GetPageWidth()), Math.Max(1, pageReader.GetPageHeight()));
        });
    }

    public Task CreateThumbnailAsync(string inputPath, string outputPath)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var renderedPage = RenderPage(inputPath, 0, isThumbnail: true, compressionLevel: 1);
            SaveRenderedPage(renderedPage, outputPath, "png", compressionLevel: 1);
        });
    }

    public Task RenderPageAsImageAsync(string inputPath, int pageIndex, string outputPath, string outputFormat, int compressionLevel)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var renderedPage = RenderPage(inputPath, pageIndex, isThumbnail: false, compressionLevel);
            SaveRenderedPage(renderedPage, outputPath, outputFormat, compressionLevel);
        });
    }

    public Task<byte[]> ExportSinglePagePdfAsync(string inputPath, int pageIndex, int compressionLevel, bool removeMetadata)
    {
        return Task.Run(() => CreatePdfWithWhiteBackground(inputPath, [pageIndex], compressionLevel, removeMetadata));
    }

    public Task<byte[]> CompressDocumentAsync(string inputPath, int compressionLevel, bool removeMetadata)
    {
        return Task.Run(() =>
        {
            using var importedDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var pageCount = importedDocument.PageCount;

            return CreatePdfWithWhiteBackground(
                inputPath,
                Enumerable.Range(0, pageCount),
                compressionLevel,
                removeMetadata);
        });
    }

    public Task<byte[]> ReverseDocumentAsync(string inputPath, int compressionLevel, bool removeMetadata)
    {
        return Task.Run(() =>
        {
            using var importedDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var pageCount = importedDocument.PageCount;

            return CreatePdfWithWhiteBackground(
                inputPath,
                Enumerable.Range(0, pageCount).Reverse(),
                compressionLevel,
                removeMetadata);
        });
    }

    public Task<byte[]> MergeDocumentsAsync(IReadOnlyList<string> inputPaths, int compressionLevel, bool removeMetadata)
    {
        return Task.Run(() =>
        {
            if (inputPaths.Count == 0)
            {
                return Array.Empty<byte>();
            }

            return CreateMergedPdfWithWhiteBackground(inputPaths, compressionLevel, removeMetadata);
        });
    }

    public static int NormalizeCompressionLevel(int compressionLevel)
        => Math.Clamp(compressionLevel, 0, 3);

    public static string GetCompressionLabel(int compressionLevel)
        => NormalizeCompressionLevel(compressionLevel) switch
        {
            0 => "None",
            1 => "Low",
            2 => "Mid",
            _ => "High",
        };

    public static int MapCompressionLevelToImagePercent(int compressionLevel)
        => NormalizeCompressionLevel(compressionLevel) switch
        {
            0 => 0,
            1 => 28,
            2 => 56,
            _ => 78,
        };

    private List<JpegImage> RenderAllPagesAsJpegImages(string inputPath, int compressionLevel)
    {
        using var docReader = _docLib.GetDocReader(inputPath, CreateRenderDimensions(compressionLevel));
        return RenderPagesAsJpegImages(
            docReader,
            Enumerable.Range(0, docReader.GetPageCount()),
            compressionLevel);
    }

    private static List<JpegImage> RenderPagesAsJpegImages(
        Docnet.Core.Readers.IDocReader docReader,
        IEnumerable<int> pageIndices,
        int compressionLevel)
    {
        var pages = new List<JpegImage>();

        foreach (var pageIndex in pageIndices)
        {
            using var pageReader = docReader.GetPageReader(pageIndex);
            var renderedPage = CreateRenderedPage(pageReader);
            pages.Add(ConvertToJpegImage(renderedPage, compressionLevel));
        }

        return pages;
    }

    private JpegImage RenderPageAsJpegImage(string inputPath, int pageIndex, int compressionLevel)
    {
        var renderedPage = RenderPage(inputPath, pageIndex, isThumbnail: false, compressionLevel);
        return ConvertToJpegImage(renderedPage, compressionLevel);
    }

    private RenderedPdfPage RenderPage(string inputPath, int pageIndex, bool isThumbnail, int compressionLevel)
    {
        using var docReader = _docLib.GetDocReader(
            inputPath,
            isThumbnail
                ? new PageDimensions(ThumbnailMinDimension, ThumbnailMaxDimension)
                : CreateRenderDimensions(compressionLevel));

        var pageCount = docReader.GetPageCount();
        if (pageCount == 0)
        {
            throw new InvalidOperationException("This PDF does not contain any pages.");
        }

        if (pageIndex < 0 || pageIndex >= pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        using var pageReader = docReader.GetPageReader(pageIndex);
        return CreateRenderedPage(pageReader);
    }

    private static RenderedPdfPage CreateRenderedPage(Docnet.Core.Readers.IPageReader pageReader)
    {
        var width = Math.Max(1, pageReader.GetPageWidth());
        var height = Math.Max(1, pageReader.GetPageHeight());
        var bytes = pageReader.GetImage();
        return new RenderedPdfPage(width, height, bytes);
    }

    private static PageDimensions CreateRenderDimensions(int compressionLevel)
    {
        var scalingFactor = NormalizeCompressionLevel(compressionLevel) switch
        {
            0 => 2.1d,
            1 => 1.65d,
            2 => 1.25d,
            _ => 0.95d,
        };

        return new PageDimensions(scalingFactor);
    }

    private static void SaveRenderedPage(RenderedPdfPage renderedPage, string outputPath, string outputFormat, int compressionLevel)
    {
        switch (outputFormat.ToLowerInvariant())
        {
            case "png":
                SaveBitmap(outputPath, CreateBitmapSource(renderedPage), isJpeg: false, quality: null);
                break;

            case "jpg":
            case "jpeg":
                SaveBitmap(outputPath, CreateBitmapSource(renderedPage), isJpeg: true, GetJpegQuality(compressionLevel));
                break;

            default:
                throw new InvalidOperationException($"Unsupported PDF image format '{outputFormat}'.");
        }
    }

    private static JpegImage ConvertToJpegImage(RenderedPdfPage renderedPage, int compressionLevel)
    {
        using var memoryStream = new MemoryStream();
        SaveBitmap(memoryStream, CreateBitmapSource(renderedPage), isJpeg: true, GetJpegQuality(compressionLevel));

        return new JpegImage
        {
            Bytes = memoryStream.ToArray(),
            Width = renderedPage.Width,
            Height = renderedPage.Height,
        };
    }

    private static BitmapSource CreateBitmapSource(RenderedPdfPage renderedPage)
    {
        var flattenedBytes = CompositeOverWhite(renderedPage.Bytes);
        var bitmapSource = BitmapSource.Create(
            renderedPage.Width,
            renderedPage.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            flattenedBytes,
            renderedPage.Width * 4);

        bitmapSource.Freeze();
        return bitmapSource;
    }

    private static void SaveBitmap(string outputPath, BitmapSource bitmapSource, bool isJpeg, long? quality)
    {
        using var stream = File.Create(outputPath);
        SaveBitmap(stream, bitmapSource, isJpeg, quality);
    }

    private static void SaveBitmap(Stream outputStream, BitmapSource bitmapSource, bool isJpeg, long? quality)
    {
        BitmapEncoder encoder = isJpeg
            ? new JpegBitmapEncoder
            {
                QualityLevel = (int)Math.Clamp(quality ?? 90L, 1L, 100L),
            }
            : new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(outputStream);
    }

    private static long GetJpegQuality(int compressionLevel)
        => NormalizeCompressionLevel(compressionLevel) switch
        {
            0 => 94L,
            1 => 84L,
            2 => 72L,
            _ => 60L,
        };

    private static byte[] CreateMergedPdfWithWhiteBackground(IReadOnlyList<string> inputPaths, int compressionLevel, bool removeMetadata)
    {
        using var outputDocument = new PdfDocument();
        ApplyPdfCompressionOptions(outputDocument, compressionLevel);

        foreach (var inputPath in inputPaths)
        {
            using var importedDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            var pageCount = importedDocument.PageCount;

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                AddImportedPdfPageToDocument(outputDocument, importedDocument.Pages[pageIndex]);
            }
        }

        return SavePdfDocumentToBytes(outputDocument, removeMetadata);
    }

    private static byte[] CreatePdfWithWhiteBackground(string inputPath, IEnumerable<int> pageIndices, int compressionLevel, bool removeMetadata)
    {
        using var outputDocument = new PdfDocument();
        ApplyPdfCompressionOptions(outputDocument, compressionLevel);
        using var importedDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);

        foreach (var pageIndex in pageIndices)
        {
            AddImportedPdfPageToDocument(outputDocument, importedDocument.Pages[pageIndex]);
        }

        return SavePdfDocumentToBytes(outputDocument, removeMetadata);
    }

    private static void AddImportedPdfPageToDocument(PdfDocument outputDocument, PdfPage importedPage)
    {
        var outputPage = outputDocument.AddPage(importedPage);
        using var graphics = XGraphics.FromPdfPage(outputPage, XGraphicsPdfPageOptions.Prepend);
        graphics.DrawRectangle(XBrushes.White, 0, 0, outputPage.Width.Point, outputPage.Height.Point);
    }

    private static void ApplyPdfCompressionOptions(PdfDocument outputDocument, int compressionLevel)
    {
        switch (NormalizeCompressionLevel(compressionLevel))
        {
            case 0:
                outputDocument.Options.NoCompression = true;
                outputDocument.Options.CompressContentStreams = false;
                outputDocument.Options.FlateEncodeMode = PdfFlateEncodeMode.Default;
                break;

            case 1:
                outputDocument.Options.NoCompression = false;
                outputDocument.Options.CompressContentStreams = true;
                outputDocument.Options.FlateEncodeMode = PdfFlateEncodeMode.BestSpeed;
                break;

            case 2:
                outputDocument.Options.NoCompression = false;
                outputDocument.Options.CompressContentStreams = true;
                outputDocument.Options.FlateEncodeMode = PdfFlateEncodeMode.Default;
                break;

            default:
                outputDocument.Options.NoCompression = false;
                outputDocument.Options.CompressContentStreams = true;
                outputDocument.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                break;
        }
    }

    private static byte[] SavePdfDocumentToBytes(PdfDocument document, bool removeMetadata)
    {
        if (removeMetadata)
        {
            document.Info.Elements.Clear();
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static byte[] CompositeOverWhite(byte[] sourceBytes)
    {
        var targetBytes = new byte[sourceBytes.Length];

        for (var index = 0; index < sourceBytes.Length; index += 4)
        {
            var blue = sourceBytes[index];
            var green = sourceBytes[index + 1];
            var red = sourceBytes[index + 2];
            var alpha = sourceBytes[index + 3];

            if (alpha == byte.MaxValue)
            {
                targetBytes[index] = blue;
                targetBytes[index + 1] = green;
                targetBytes[index + 2] = red;
                targetBytes[index + 3] = byte.MaxValue;
                continue;
            }

            var inverseAlpha = byte.MaxValue - alpha;
            targetBytes[index] = BlendChannelOverWhite(blue, alpha, inverseAlpha);
            targetBytes[index + 1] = BlendChannelOverWhite(green, alpha, inverseAlpha);
            targetBytes[index + 2] = BlendChannelOverWhite(red, alpha, inverseAlpha);
            targetBytes[index + 3] = byte.MaxValue;
        }

        return targetBytes;
    }

    private static byte BlendChannelOverWhite(byte color, byte alpha, int inverseAlpha)
    {
        var blended = (color * alpha) + (byte.MaxValue * inverseAlpha);
        return (byte)((blended + 127) / byte.MaxValue);
    }

    private sealed record RenderedPdfPage(int Width, int Height, byte[] Bytes);
}

