using System;
using System.Collections.Generic;
using System.Linq;
using HidLibrary;

namespace StreamDeckLibrary {
    public class StreamDeck {
        private const int VendorId = 0x0fd9;
        private const int ProductId = 0x0060;
        private HidDevice device;
        private const int NUM_FIRST_PAGE_PIXELS = 2583;
        private const int NUM_SECOND_PAGE_PIXELS = 2601;
        private const int ICON_SIZE = 72;

        private StreamDeck(HidDevice device) {
            this.device = device;
        }

        public void Connect() {
            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead|ShareMode.ShareWrite);
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
                throw new ArgumentException("Brightness must be between 0-100");
            }

            byte[] data = { 0x05, 0x55, 0xAA, 0xD1, 0x01, (byte)amount, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            device.WriteFeatureData(data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyIndex">The key to write to 0 - 14</param>
        public void WriteImage(int keyIndex, int r, int g, int b) {
            if (keyIndex < 0 || keyIndex > 14) {
                throw new ArgumentException("keyIndex must be between 0-14");
            }

            WritePage1(keyIndex, r, g, b);
            WritePage2(keyIndex, r, g, b);
        }

        private void WritePage1(int keyIndex, int r, int g, int b) {
            byte[] data = {
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


            byte[] actual = new byte[8191];
            Array.Copy(data, actual, data.Length); // copy header

            for (int i = 0; i < NUM_FIRST_PAGE_PIXELS; i++) {
                int pixel_start = data.Length + 3 * i;
                actual[pixel_start] = (byte) b; //B
                actual[pixel_start + 1] = (byte) g; //G
                actual[pixel_start + 2] = (byte) r; //R
            }

            device.Write(actual);
        }

        private void WritePage2(int keyIndex, int r, int g, int b) {
            byte[] data = {
                0x02, 0x01, 0x02, 0x00, 0x01, (byte)(keyIndex + 1),
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            };


            byte[] actual = new byte[8191];
            Array.Copy(data, actual, data.Length); // copy header

            for (int i = 0; i < NUM_SECOND_PAGE_PIXELS; i++) {
                int pixel_start = data.Length + 3 * i;
                actual[pixel_start] = (byte)b; //B
                actual[pixel_start + 1] = (byte)g; //G
                actual[pixel_start + 2] = (byte)r; //R
            }

            device.Write(actual);
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

        public static IEnumerable<StreamDeck> GetDevices() {
            return HidDevices.Enumerate(VendorId, ProductId).Select(device => new StreamDeck(device));
        }
    }
}
