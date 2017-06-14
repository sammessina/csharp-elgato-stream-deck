using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HidLibrary {
    internal class HidDeviceEventMonitor {
        public event InsertedEventHandler Inserted;
        public event RemovedEventHandler Removed;

        public delegate void InsertedEventHandler();
        public delegate void RemovedEventHandler();

        private readonly HidDevice _device;
        private bool _wasConnected;

        public HidDeviceEventMonitor(HidDevice device) {
            _device = device;
        }

        public void Init() {
            //var eventMonitor = new Action(DeviceEventMonitor);
            //eventMonitor.BeginInvoke(DisposeDeviceEventMonitor, eventMonitor);
            DeviceChangeNotifier.DeviceNotify += DeviceEventMonitor;

            DeviceChangeNotifier.Start();
        }

        private void DeviceEventMonitor(IEnumerable<HidDevices.DeviceInfo> deviceInfos) {
            var isConnected = deviceInfos.Any(info => info.Path == _device.DevicePath); //_device.IsConnected;

            if (isConnected != _wasConnected) {
                if (isConnected && Inserted != null) Inserted();
                else if (!isConnected && Removed != null) Removed();
                _wasConnected = isConnected;
            }
        }

        internal class DeviceChangeNotifier : Form {
            public delegate void DeviceNotifyDelegate(IEnumerable<HidDevices.DeviceInfo> devices);
            public static event DeviceNotifyDelegate DeviceNotify;
            private static DeviceChangeNotifier mInstance;
            private static bool started;

            public static void Start() {
                lock (typeof(DeviceChangeNotifier)) {
                    if (started) {
                        return;
                    }

                    started = true;
                }

                Thread t = new Thread(runForm);
                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = true;
                t.Start();
            }
            public static void Stop() {
                if (mInstance == null) throw new InvalidOperationException("Notifier not started");
                DeviceNotify = null;
                mInstance.Invoke(new MethodInvoker(mInstance.endForm));
                started = false;
            }
            private static void runForm() {
                Application.Run(new DeviceChangeNotifier());
            }

            private void endForm() {
                this.Close();
            }
            protected override void SetVisibleCore(bool value) {
                // Prevent window getting visible
                if (mInstance == null) CreateHandle();
                mInstance = this;
                value = false;
                base.SetVisibleCore(value);
            }
            protected override void WndProc(ref Message m) {
                // Trap WM_DEVICECHANGE
                if (m.Msg == NativeMethods.WM_DEVICECHANGE) {
                    DeviceNotify?.Invoke(HidDevices.EnumerateDevices().ToList());
                }
                base.WndProc(ref m);
            }
        }
    }
}
