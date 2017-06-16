using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using HidLibrary;
using BitmapData = System.Drawing.Imaging.BitmapData;

namespace StreamDeckLibrary {
    public class StreamDeck {
        private const int VendorId = 0x0fd9;
        private const int ProductId = 0x0060;
        private readonly HidDevice device;
        private const int NumFirstPagePixels = 2583;
        private const int NumSecondPagePixels = 2601;
        private const int NumPixels = NumFirstPagePixels + NumSecondPagePixels;
        private const int IconSize = 72;

        private StreamDeck(HidDevice device) {
            this.device = device;
        }

        public void Connect() {
            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
            device.ReadReport(OnReport);
            device.Inserted += DeviceAttachedHandler;
            device.Removed += DeviceRemovedHandler;
            device.MonitorDeviceEvents = true;
        }

        public void Disconnect() {
            device.CloseDevice();
            device.Inserted -= DeviceAttachedHandler;
            device.Removed -= DeviceRemovedHandler;
            device.MonitorDeviceEvents = false;
        }

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

            byte[] data = BitmapToByteArray(image);
            WriteButtonBytes(keyIndex, data);
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

        private void OnReport(HidReport report) {
            if (!device.IsConnected) {
                return;
            }

            if (report.Data.Length >= 4) {
                //var message = MessageFactory.CreateMessage(_currentProductId, report.Data);
            }

            device.ReadReport(OnReport);
        }

        private static void KeyDepressed() {
            Console.WriteLine("Button depressed.");
        }

        private void DeviceAttachedHandler() {
            Console.WriteLine("attached.");
            device.ReadReport(OnReport);
        }

        private void DeviceRemovedHandler() {
            Console.WriteLine("removed.");
        }

        /// <summary>
        /// https://stackoverflow.com/a/4748383
        /// </summary>
        /// <param name="image"></param>
        private static byte[] BitmapToByteArray(Bitmap image) {
            const int pixelWidth = 4;
            byte[] result = new byte[image.Width * image.Height * 3];

            if (image == null) {
                throw new ArgumentNullException(nameof(image));
            }

            BitmapData bData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int pixelnum = 0;

            try {
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


                        //result[(scanline * scansize) + pixeloffset] =
                    }
                }
            } finally {
                image.UnlockBits(bData);
            }

            return result;
        }

        public static IEnumerable<StreamDeck> GetDevices() {
            return HidDevices.Enumerate(VendorId, ProductId).Select(device => new StreamDeck(device));
        }
    }
}
