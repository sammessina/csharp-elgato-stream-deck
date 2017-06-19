using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using BitmapData = System.Drawing.Imaging.BitmapData;

namespace StreamDeckLibrary {
    public class StreamDeck {
        public delegate void ButtonEventHandler(int keyIndex);

        public event ButtonEventHandler OnButtonDown, OnButtonUp;

        /// <summary>
        /// Whether to enable optimizations around only writing to screens when the new screen contents are different from the old.
        /// NOTE: At best, 1 out of every 64,000 writes will be falsly detected as duplicated and won't be written.
        /// </summary>
        public bool EnableWriteOptimizations { get; set; } = true;

        public const int FullScreenWidth = 468;
        public const int FullScreenHeight = 270;

        public const int VisibleScreenWidth = 360;
        public const int VisibleScreenHeight = 216;

        public const int IconSize = 72;

        private const int VendorId = 0x0fd9;
        private const int ProductId = 0x0060;
        private readonly HidDevice device;
        private const int NumFirstPagePixels = 2583;
        private const int NumSecondPagePixels = 2601;
        private const int NumPixels = NumFirstPagePixels + NumSecondPagePixels;

        private Dictionary<int, long> lastScreenWriteHashes;
        
        private InputMessage lastInput;

        private StreamDeck(HidDevice device) {
            this.device = device;
            ResetState();
        }

        /// <summary>
        /// Opens a handle to the device.
        /// </summary>
        public void Connect() {
            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
            device.ReadReport(OnReport);
            device.Inserted += DeviceAttachedHandler;
            device.Removed += DeviceRemovedHandler;
            device.MonitorDeviceEvents = true;
        }

        /// <summary>
        /// Closes the handle to the device.
        /// </summary>
        public void Disconnect() {
            device.CloseDevice();
            device.Inserted -= DeviceAttachedHandler;
            device.Removed -= DeviceRemovedHandler;
            device.MonitorDeviceEvents = false;
        }

        /// <summary>
        /// Sets the brightness of the device.
        /// </summary>
        /// <param name="amount">The brightness, in percentage. 0-100.</param>
        public void SetBrightness(int amount) {
            if (amount < 0 || amount > 100) {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Value must be between 0-100.");
            }

            byte[] data = { 0x05, 0x55, 0xAA, 0xD1, 0x01, (byte)amount, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            device.WriteFeatureData(data);
        }

        /// <summary>
        /// Writes an image to a button.
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14</param>
        /// <param name="image">The image to draw. Must be 72x72.</param>
        public void WriteButtonImage(int keyIndex, Bitmap image) {
            if (keyIndex < 0 || keyIndex > 14) {
                throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, "Value must be between 0-14.");
            }

            long hash;
            byte[] data = BitmapToByteArray(image, out hash);
            WriteButtonBytesIfUnchanged(keyIndex, data, hash);
        }

        /// <summary>
        /// Sets a button to a given color.
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14.</param>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        public void WriteButtonColor(int keyIndex, byte r, byte g, byte b) {
            if (keyIndex < 0 || keyIndex > 14) {
                throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, "Value must be between 0-14.");
            }

            // Generate array with pixel data.
            byte[] data = new byte[NumPixels * 3];
            for (int i = 0; i < data.Length; i += 3) {
                data[i] = b;
                data[i + 1] = g;
                data[i + 2] = r;
            }

            WriteButtonBytes(keyIndex, data);
        }

        /// <summary>
        /// Writes an image to the entire screen.
        /// </summary>
        /// <param name="image">The image to draw. Must be 360x216 or 468x270 (with bezel compensation).</param>
        public void WriteScreenImage(Bitmap image) {
            // Note: By my measurement, each screen is 14x14mm. The width of all screens on the first row is 91mm.
            const int bezelWidthPx = 27;
            
            if ((image.Width != VisibleScreenWidth || image.Height != VisibleScreenHeight) && (image.Width != FullScreenWidth || image.Height != FullScreenHeight)) {
                throw new ArgumentException(string.Format("Unsupported image resolution given: {0}x{1}", image.Width, image.Height), nameof(image));
            }
            
            // Use the same algorithm, but just adjust the amount of data that is skipped in between sections
            int skipPx = (image.Width == VisibleScreenWidth) ? 0 : bezelWidthPx;
            int stridePx = skipPx + IconSize;

            for (int y = 0; y < 3; y++) {
                for (int x = 0; x < 5; x++) {
                    int startx = stridePx * x;
                    int starty = stridePx * y;

                    long hash;
                    byte[] data = BitmapSectionToByteArray(image, startx, starty, IconSize, IconSize, out hash);
                    WriteButtonBytesIfUnchanged(GetKeyIndex(x, y), data, hash);
                }
            }
        }

        /// <summary>
        /// Converts an x- and y-coordinate to the key index used by the device.
        /// </summary>
        /// <param name="x">The x-coordinate of the key. 0-4.</param>
        /// <param name="y">The y-coordinate of the key. 0-2.</param>
        /// <returns></returns>
        public static int GetKeyIndex(int x, int y) {
            if (x < 0 || x > 5) {
                throw new ArgumentOutOfRangeException(nameof(x), x, "Value must be 0-4.");
            }

            if (y < 0 || y > 3) {
                throw new ArgumentOutOfRangeException(nameof(y), y, "Value must be 0-2.");
            }

            return y * 5 + (4 - x);
        }

        /// <summary>
        /// Writes a buffer to a button.
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14.</param>
        /// <param name="data">The image data.</param>
        /// <param name="hash">The hash of the image data.</param>
        private void WriteButtonBytesIfUnchanged(int keyIndex, byte[] data, long hash) {
            if (!EnableWriteOptimizations || !lastScreenWriteHashes.ContainsKey(keyIndex) || lastScreenWriteHashes[keyIndex] != hash) {
                lastScreenWriteHashes[keyIndex] = hash;
                WriteButtonBytes(keyIndex, data);
            }
        }

        /// <summary>
        /// Writes a buffer to a button.
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14.</param>
        /// <param name="data">The image data.</param>
        private void WriteButtonBytes(int keyIndex, byte[] data) {
            WritePage1(keyIndex, data);
            WritePage2(keyIndex, data);
        }

        /// <summary>
        /// Writes the first part of the image.
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14.</param>
        /// <param name="data">The image data.</param>
        private void WritePage1(int keyIndex, byte[] data) {
            byte[] header = {
                0x02, 0x01, 0x01, 0x00, 0x00, (byte) (keyIndex + 1), 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x42, 0x4d, 0xf6, 0x3c, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
                0x00, 0x00, 0x48, 0x00, 0x00, 0x00, 0x48, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
                0x00, 0x00, 0xc0, 0x3c, 0x00, 0x00, 0xc4, 0x0e,
                0x00, 0x00, 0xc4, 0x0e, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            byte[] packet = new byte[8191];
            Array.Copy(header, packet, header.Length); // copy header
            Array.Copy(data, 0, packet, header.Length, NumFirstPagePixels * 3); // copy data

            device.Write(packet);
        }

        /// <summary>
        /// Writes the second part of the image.
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14.</param>
        /// <param name="data">The image data.</param>
        private void WritePage2(int keyIndex, byte[] data) {
            byte[] header = {
                0x02, 0x01, 0x02, 0x00, 0x01, (byte)(keyIndex + 1),
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            };

            byte[] packet = new byte[8191];
            Array.Copy(header, packet, header.Length); // copy header
            Array.Copy(data, NumFirstPagePixels * 3, packet, header.Length, NumSecondPagePixels * 3); // copy data

            device.Write(packet);
        }

        /// <summary>
        /// Handles incoming HID reports.
        /// </summary>
        /// <param name="report">The input report.</param>
        private void OnReport(HidReport report) {
            if (!device.IsConnected) {
                Console.WriteLine("But device not connected?");
                return;
            }

            if (report.Data.Length != 16) {
                Console.WriteLine("Unknown input");
                return;
            }

            lock (lastInput) {
                InputMessage input = new InputMessage(report.Data);

                for (int i = 0; i < 15; i++) {
                    bool keyPressed = input.IsButtonPressed(i);
                    if (keyPressed != lastInput.IsButtonPressed(i)) {
                        if (keyPressed) {
                            Console.WriteLine("Press: {0}", i);
                            OnButtonDown?.Invoke(i);
                        } else {
                            Console.WriteLine("Release: {0}", i);
                            OnButtonUp?.Invoke(i);
                        }
                    }
                }

                lastInput = input;
            }

            device.ReadReport(OnReport);
        }

        /// <summary>
        /// Handles when the device is re-attached.
        /// </summary>
        private void DeviceAttachedHandler() {
            Console.WriteLine("attached.");
            ResetState();
            device.ReadReport(OnReport);
        }

        /// <summary>
        /// Handles when the device is disconnected.
        /// </summary>
        private void DeviceRemovedHandler() {
            Console.WriteLine("removed.");
        }

        private void ResetState() {
            lastScreenWriteHashes = new Dictionary<int, long>();
            lastInput = new InputMessage();
        }

        /// <summary>
        /// Converts a Bitmap to the BGR format used by the device.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="hash">The hash of the image data.</param>
        /// <returns>The image data in BGR format.</returns>
        private byte[] BitmapToByteArray(Bitmap image, out long hash) {
            return BitmapSectionToByteArray(image, 0, 0, image.Width, image.Height, out hash);
        }

        /// <summary>
        /// Converts a Bitmap to the BGR format used by the device.
        /// </summary>
        /// <param name="image">The source image.</param>
        /// <param name="x1">The x coordinate of where to start reading data from.</param>
        /// <param name="y1">The y coordinate of where to start reading data from.</param>
        /// <param name="width">The width of the resulting image.</param>
        /// <param name="height">The height of the resulting image.</param>
        /// <param name="hash">The hash of the image data.</param>
        /// <returns>The image data in BGR format.</returns>
        private byte[] BitmapSectionToByteArray(Bitmap image, int x1, int y1, int width, int height, out long hash) {
            if (image == null) {
                throw new ArgumentNullException(nameof(image));
            }

            const int pixelWidth = 4;
            byte[] result = new byte[width * height * 3];

            BitmapData bData = image.LockBits(new Rectangle(x1, y1, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try {
                BitmapToByteArrayInner(bData, pixelWidth, result, out hash);
            } finally {
                image.UnlockBits(bData);
            }

            return result;
        }

        /// <summary>
        /// The inner portion of BitmapToByteArray(). Does the data conversion.
        /// </summary>
        /// <param name="bData">The image input data.</param>
        /// <param name="pixelWidth">The size, in bytes, of each input pixel in the bData parameter.</param>
        /// <param name="result">The image data in BGR format.</param>
        /// <param name="hash">The hash of the image data.</param>
        private void BitmapToByteArrayInner(BitmapData bData, int pixelWidth, byte[] result, out long hash) {
            int pixelnum = 0;
            byte[] pixelData = new Byte[bData.Stride];
            for (int scanline = 0; scanline < bData.Height; scanline++) {
                Marshal.Copy(bData.Scan0 + (scanline * bData.Stride), pixelData, 0, bData.Stride);
                for (int pixeloffset = bData.Width - 1; pixeloffset >= 0; pixeloffset--) {
                    byte a = pixelData[pixeloffset * pixelWidth + 3];
                    byte r = pixelData[pixeloffset * pixelWidth + 2];
                    byte g = pixelData[pixeloffset * pixelWidth + 1];
                    byte b = pixelData[pixeloffset * pixelWidth];

                    double alpha = a / 255.0;

                    r = (byte)Math.Round(r * alpha);
                    g = (byte)Math.Round(g * alpha);
                    b = (byte)Math.Round(b * alpha);

                    result[pixelnum++] = b;
                    result[pixelnum++] = g;
                    result[pixelnum++] = r;
/*

                    hash += r * 31 * scanline * pixeloffset;
                    hash += g * 51 * scanline * pixeloffset;
                    hash += b * 71 * scanline * pixeloffset;*/
                    //result[(scanline * scansize) + pixeloffset] =
                }
            }

            //hash = new Random().Next(1000)
            // TODO: Calculate hash inline
            hash = EnableWriteOptimizations ? Crc16Ccitt(result) : 0;
        }

        /// <summary>
        /// Scans for Stream Deck devices.
        /// </summary>
        /// <returns>The IEnumerable of found devices.</returns>
        public static IEnumerable<StreamDeck> GetDevices() {
            return HidDevices.Enumerate(VendorId, ProductId).Select(device => new StreamDeck(device));
        }



        private static ushort Crc16Ccitt(byte[] bytes) {
            const ushort poly = 4129;
            ushort[] table = new ushort[256];
            ushort initialValue = 0xffff;
            ushort temp, a;
            ushort crc = initialValue;
            for (int i = 0; i < table.Length; ++i) {
                temp = 0;
                a = (ushort)(i << 8);
                for (int j = 0; j < 8; ++j) {
                    if (((temp ^ a) & 0x8000) != 0)
                        temp = (ushort)((temp << 1) ^ poly);
                    else
                        temp <<= 1;
                    a <<= 1;
                }
                table[i] = temp;
            }
            for (int i = 0; i < bytes.Length; ++i) {
                crc = (ushort)((crc << 8) ^ table[((crc >> 8) ^ (0xff & bytes[i]))]);
            }
            return crc;
        }
    }
}
