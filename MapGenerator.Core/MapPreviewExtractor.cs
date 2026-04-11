using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using lzo.net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace MapGenerator.Core
{
    public static class MapPreviewExtractor
    {
        public static Image ExtractMapPreview(string mapFilePath)
        {
            if (!File.Exists(mapFilePath))
            {
                throw new FileNotFoundException("Map file not found", mapFilePath);
            }

            IniFile mapIni = new IniFile(mapFilePath);
            return ExtractMapPreview(mapIni);
        }

        public static Image ExtractMapPreview(IniFile mapIni)
        {
            List<string> sectionKeys = mapIni.GetSectionKeys("PreviewPack");

            if (sectionKeys == null || sectionKeys.Count == 0)
            {
                return null;
            }

            if (mapIni.GetStringValue("PreviewPack", "1", string.Empty) ==
                "yAsAIAXQ5PDQ5PDQ6JQATAEE6PDQ4PDI4JgBTAFEAkgAJyAATAG0AydEAEABpAJIA0wBVA")
            {
                return null;
            }

            string[] previewSizes = mapIni.GetStringValue("Preview", "Size", "").Split(',');
            int previewWidth = previewSizes.Length > 3 ? int.Parse(previewSizes[2]) : -1;
            int previewHeight = previewSizes.Length > 3 ? int.Parse(previewSizes[3]) : -1;

            if (previewWidth < 1 || previewHeight < 1)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            if (sectionKeys != null)
            {
                foreach (string key in sectionKeys)
                    sb.Append(mapIni.GetStringValue("PreviewPack", key, string.Empty));
            }

            byte[] dataSource;

            try
            {
                dataSource = Convert.FromBase64String(sb.ToString());
            }
            catch (Exception)
            {
                return null;
            }

            byte[] dataDest = DecompressPreviewData(dataSource, previewWidth * previewHeight * 3, out string errorMessage);

            if (errorMessage != null)
            {
                return null;
            }

            Image bitmap = CreatePreviewBitmapFromImageData(previewWidth, previewHeight, dataDest, out errorMessage);

            if (errorMessage != null)
            {
                return null;
            }

            return bitmap;
        }

        private static byte[] DecompressPreviewData(byte[] dataSource, int decompressedDataSize, out string errorMessage)
        {
            try
            {
                byte[] dataDest = new byte[decompressedDataSize];
                int readBytes = 0, writtenBytes = 0;

                while (true)
                {
                    if (readBytes >= dataSource.Length)
                        break;

                    ushort sizeCompressed = BitConverter.ToUInt16(dataSource, readBytes);
                    readBytes += 2;
                    ushort sizeUncompressed = BitConverter.ToUInt16(dataSource, readBytes);
                    readBytes += 2;

                    if (sizeCompressed == 0 || sizeUncompressed == 0)
                        break;

                    if (readBytes + sizeCompressed > dataSource.Length ||
                        writtenBytes + sizeUncompressed > dataDest.Length)
                    {
                        errorMessage = "Preview data does not match preview size or the data is corrupted, unable to extract preview.";
                        return null;
                    }

                    using (MemoryStream ms = new MemoryStream(dataSource, readBytes, sizeCompressed))
                    {
                        LzoStream stream = new LzoStream(ms, CompressionMode.Decompress);
                        stream.Read(dataDest, writtenBytes, sizeUncompressed);
                    }
                    readBytes += sizeCompressed;
                    writtenBytes += sizeUncompressed;
                }

                errorMessage = null;
                return dataDest;
            }
            catch (Exception e)
            {
                errorMessage = "Error encountered decompressing preview data. Message: " + e.Message;
                return null;
            }
        }

        private static Image CreatePreviewBitmapFromImageData(int width, int height, byte[] imageData, out string errorMessage)
        {
            const int pixelFormatBitCount = 24;
            const int pixelFormatByteCount = pixelFormatBitCount / 8;

            if (imageData.Length != width * height * pixelFormatByteCount)
            {
                errorMessage = "Provided preview image dimensions do not match preview image data length.";
                return null;
            }

            try
            {
                int strideWidth = (((width * pixelFormatBitCount) + 31) & ~31) >> 3;
                int numSkipBytes = strideWidth - (width * pixelFormatByteCount);
                byte[] bitmapPixelData = new byte[strideWidth * height];
                int writtenBytes = 0;
                int readBytes = 0;

                for (int h = 0; h < height; h++)
                {
                    for (int w = 0; w < width; w++)
                    {
                        bitmapPixelData[writtenBytes] = imageData[readBytes + 2];
                        bitmapPixelData[writtenBytes + 1] = imageData[readBytes + 1];
                        bitmapPixelData[writtenBytes + 2] = imageData[readBytes];
                        writtenBytes += pixelFormatByteCount;
                        readBytes += pixelFormatByteCount;
                    }
                    writtenBytes += numSkipBytes;
                }

                var image = new Image<Bgr24>(width, height);
                Configuration configuration = Configuration.Default;
                Buffer2D<Bgr24> imageBuffer = image.Frames.RootFrame.PixelBuffer;
                using IMemoryOwner<Bgr24> workBuffer = Configuration.Default.MemoryAllocator.Allocate<Bgr24>(width);

                unsafe
                {
                    fixed (byte* sourcePtrBase = &bitmapPixelData[0])
                    {
                        fixed (Bgr24* destPtr = &workBuffer.Memory.Span[0])
                        {
                            for (int rowCount = 0; rowCount < height; rowCount++)
                            {
                                Span<Bgr24> row = imageBuffer.DangerousGetRowSpan(rowCount);
                                byte* sourcePtr = sourcePtrBase + (strideWidth * rowCount);

                                Buffer.MemoryCopy(sourcePtr, destPtr, strideWidth, strideWidth);
                                PixelOperations<Bgr24>.Instance.FromBgr24(configuration, workBuffer.Memory.Span[..width], row);
                            }
                        }
                    }
                }

                errorMessage = null;
                return image;
            }
            catch (Exception e)
            {
                errorMessage = "Error encountered creating preview bitmap. Message: " + e.Message;
                return null;
            }
        }
    }

    public class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>();
        public string FileName { get; }

        public IniFile(string fileName)
        {
            FileName = fileName;
            Load();
        }

        private void Load()
        {
            if (!File.Exists(FileName))
                return;

            string currentSection = null;
            foreach (string line in File.ReadAllLines(FileName))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    sections[currentSection] = new Dictionary<string, string>();
                }
                else if (currentSection != null && trimmedLine.Contains('='))
                {
                    int equalsIndex = trimmedLine.IndexOf('=');
                    string key = trimmedLine.Substring(0, equalsIndex).Trim();
                    string value = trimmedLine.Substring(equalsIndex + 1).Trim();
                    sections[currentSection][key] = value;
                }
            }
        }

        public List<string> GetSectionKeys(string section)
        {
            if (sections.TryGetValue(section, out var keys))
            {
                return new List<string>(keys.Keys);
            }
            return null;
        }

        public string GetStringValue(string section, string key, string defaultValue)
        {
            if (sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
