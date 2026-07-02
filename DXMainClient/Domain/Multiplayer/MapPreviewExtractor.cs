using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using ClientCore;
using Rampastring.Tools;
using lzo.net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace DTAClient.Domain.Multiplayer
{
    /// <summary>
    /// 用于从地图中提取预览图像的辅助类。
    /// </summary>
    public static class MapPreviewExtractor
    {
        /// <summary>
        /// 将地图预览图像提取为位图。
        /// </summary>
        /// <param name="mapIni">地图文件。</param>
        /// <returns>地图预览图像的位图，如果无法提取预览则返回null。</returns>
        public static Image ExtractMapPreview(IniFile mapIni)
        {
            List<string> sectionKeys = mapIni.GetSectionKeys("PreviewPack");

            string baseFilename = mapIni.FileName.Replace(ProgramConstants.GamePath, "");

            if (sectionKeys == null || sectionKeys.Count == 0)
            {
                Logger.Log("MapPreviewExtractor: " + baseFilename + " - no [PreviewPack] exists, unable to extract preview.");
                return null;
            }

            if (mapIni.GetStringValue("PreviewPack", "1", string.Empty) ==
                "yAsAIAXQ5PDQ5PDQ6JQATAEE6PDQ4PDI4JgBTAFEAkgAJyAATAG0AydEAEABpAJIA0wBVA")
            {
                Logger.Log("MapPreviewExtractor: " + baseFilename + " - Hidden preview detected, not extracting preview.");
                return null;
            }

            string[] previewSizes = mapIni.GetStringValue("Preview", "Size", "").Split(',');
            int previewWidth = previewSizes.Length > 3 ? Conversions.IntFromString(previewSizes[2], -1) : -1;
            int previewHeight = previewSizes.Length > 3 ? Conversions.IntFromString(previewSizes[3], -1) : -1;

            if (previewWidth < 1 || previewHeight < 1)
            {
                Logger.Log("MapPreviewExtractor: " + baseFilename + " - [Preview] Size value is invalid, unable to extract preview.");
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
                Logger.Log("MapPreviewExtractor: " + baseFilename + " - [PreviewPack] is malformed, unable to extract preview.");
                return null;
            }

            byte[] dataDest = DecompressPreviewData(dataSource, previewWidth * previewHeight * 3, out string errorMessage);

            if (errorMessage != null)
            {
                Logger.Log("MapPreviewExtractor: " + baseFilename + " - " + errorMessage);
                return null;
            }

            Image bitmap = CreatePreviewBitmapFromImageData(previewWidth, previewHeight, dataDest, out errorMessage);

            if (errorMessage != null)
            {
                Logger.Log("MapPreviewExtractor: " + baseFilename + " - " + errorMessage);
                return null;
            }

            return bitmap;
        }

        /// <summary>
        /// 解压地图预览图像数据。
        /// </summary>
        /// <param name="dataSource">压缩的地图预览图像数据数组。</param>
        /// <param name="decompressedDataSize">解压后的预览图像数据大小。</param>
        /// <param name="errorMessage">如果出错则设置为错误消息，否则为null。</param>
        /// <returns>如果解压成功则返回解压后的预览图像数据数组，否则返回null。</returns>
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
                        errorMessage = "预览数据与预览大小不匹配或数据已损坏，无法提取预览。";
                        return null;
                    }

                    LzoStream stream = new LzoStream(new MemoryStream(dataSource, readBytes, sizeCompressed), CompressionMode.Decompress);
                    stream.Read(dataDest, writtenBytes, sizeUncompressed);
                    readBytes += sizeCompressed;
                    writtenBytes += sizeUncompressed;
                }

                errorMessage = null;
                return dataDest;
            }
            catch (Exception e)
            {
                errorMessage = "解压预览数据时遇到错误。消息: " + e.Message;
                return null;
            }
        }

        /// <summary>
        /// 根据提供的尺寸和24位RGB格式的原始图像像素数据创建预览位图。
        /// </summary>
        /// <param name="width">位图宽度。</param>
        /// <param name="height">位图高度。</param>
        /// <param name="imageData">24位RGB格式的原始图像像素数据。</param>
        /// <param name="errorMessage">如果出错则设置为错误消息，否则为null。</param>
        /// <returns>基于提供的尺寸和原始图像数据的位图，如果图像数据长度与提供的尺寸不匹配或出错则返回null。</returns>
        private static Image CreatePreviewBitmapFromImageData(int width, int height, byte[] imageData, out string errorMessage)
        {
            const int pixelFormatBitCount = 24;
            const int pixelFormatByteCount = pixelFormatBitCount / 8;

            if (imageData.Length != width * height * pixelFormatByteCount)
            {
                errorMessage = "提供的预览图像尺寸与预览图像数据长度不匹配。";
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
                        // GDI+位图原始像素数据为BGR格式，每个像素的红蓝值需要互换。
                        bitmapPixelData[writtenBytes] = imageData[readBytes + 2];
                        bitmapPixelData[writtenBytes + 1] = imageData[readBytes + 1];
                        bitmapPixelData[writtenBytes + 2] = imageData[readBytes];
                        writtenBytes += pixelFormatByteCount;
                        readBytes += pixelFormatByteCount;
                    }

                    // GDI+位图的步幅/扫描宽度必须是4的倍数，因此每个步幅/扫描行的末尾可能包含额外的字节
                    // 这些字节在位图原始像素数据中存在但在图像数据中不存在，复制时应跳过。
                    writtenBytes += numSkipBytes;
                }

                // https://github.com/SixLabors/ImageSharp/blob/main/tests/ImageSharp.Tests/TestUtilities/ReferenceCodecs/SystemDrawingBridge.cs
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
                errorMessage = "创建预览位图时遇到错误。消息: " + e.Message;
                return null;
            }
        }
    }
}