using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HNXOSOptimizer
{
    public static class IconGenerator
    {
        private static readonly string IconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");

        public static string GetIconPath()
        {
            if (File.Exists(IconPath))
            {
                return IconPath;
            }

            try
            {
                string bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "background.png");
                if (File.Exists(bgPath))
                {
                    GenerateCircularIcon(bgPath, IconPath);
                    Logger.LogInfo("Successfully generated circular application icon: " + IconPath);
                }
                else
                {
                    Logger.LogWarning("Cannot generate icon: background.png not found.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error generating circular icon", ex);
            }

            return File.Exists(IconPath) ? IconPath : string.Empty;
        }

        private static void GenerateCircularIcon(string sourceImgPath, string targetIconPath)
        {
            // 1. Load source image
            var source = new BitmapImage();
            source.BeginInit();
            source.UriSource = new Uri(sourceImgPath, UriKind.Absolute);
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.EndInit();

            // 2. Crop to center square
            double srcWidth = source.PixelWidth;
            double srcHeight = source.PixelHeight;
            double minSize = Math.Min(srcWidth, srcHeight);
            
            double xOffset = (srcWidth - minSize) / 2.0;
            double yOffset = (srcHeight - minSize) / 2.0;

            var cropped = new CroppedBitmap(source, new Int32Rect((int)xOffset, (int)yOffset, (int)minSize, (int)minSize));

            // 3. Create circular rendering context (size 256x256)
            int targetSize = 256;
            var targetBitmap = new RenderTargetBitmap(targetSize, targetSize, 96, 96, PixelFormats.Pbgra32);
            
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Draw a circle clip geometry
                var clipGeometry = new EllipseGeometry(
                    new Point(targetSize / 2.0, targetSize / 2.0),
                    targetSize / 2.0,
                    targetSize / 2.0
                );
                
                drawingContext.PushClip(clipGeometry);
                
                // Draw the square cropped image into the circle clip
                drawingContext.DrawImage(cropped, new Rect(0, 0, targetSize, targetSize));
                
                drawingContext.Pop();
            }

            targetBitmap.Render(drawingVisual);

            // 4. Encode as PNG
            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(targetBitmap));
                encoder.Save(ms);
                pngBytes = ms.ToArray();
            }

            // 5. Wrap inside Windows ICO format
            using (var fs = new FileStream(targetIconPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // ICO Header (6 bytes)
                bw.Write((ushort)0); // Reserved. Must always be 0.
                bw.Write((ushort)1); // Specifies image type: 1 for icon (.ICO)
                bw.Write((ushort)1); // Specifies number of images in the file: 1

                // Icon Directory Entry (16 bytes)
                bw.Write((byte)0); // Width. 256 pixels is represented by 0.
                bw.Write((byte)0); // Height. 256 pixels is represented by 0.
                bw.Write((byte)0); // Color count. 0 if >= 8bpp
                bw.Write((byte)0); // Reserved. Should be 0.
                bw.Write((ushort)1); // Color planes. 1
                bw.Write((ushort)32); // Bits per pixel. 32-bit (ARGB)
                bw.Write((uint)pngBytes.Length); // Size of image data in bytes
                bw.Write((uint)22); // Offset of image data from start of file (6 header + 16 directory = 22)

                // Write PNG payload
                bw.Write(pngBytes);
            }
        }
    }
}
