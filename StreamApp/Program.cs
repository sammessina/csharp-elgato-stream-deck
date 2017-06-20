using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using StreamDeckLibrary;

namespace StreamApp {
    class Program {
        static void Main(string[] args) {
            var devices = StreamDeck.GetDevices().ToList();

            if (!devices.Any()) {
                Console.WriteLine("Could not find a StreamDeck.");
                Console.ReadKey();
                return;
            }

            var deck = devices.First();

            deck.Connect();
            deck.SetBrightness(50);

            var renderer = new SampleRenderer();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            double lastTime = sw.Elapsed.TotalMilliseconds;
            Bitmap screen = new Bitmap(StreamDeck.FullScreenWidth, StreamDeck.FullScreenHeight);
            var graphics = Graphics.FromImage(screen);
            double targetFps = 60;
            double targetInterval = 1000 / targetFps;
            renderer.Init(screen.Width, screen.Height);

            deck.OnButtonDown += index => renderer.OnKeyDown(index);
            deck.OnButtonUp += index => renderer.OnKeyUp(index);

            while (true) {
                double now = sw.Elapsed.TotalMilliseconds;
                double dtMs = now - lastTime;
                double dt = dtMs / 1000;
                lastTime = now;

                renderer.Update(dt, screen, graphics);
                renderer.Render(dt, screen, graphics);

                deck.WriteScreenImage(screen);

                double delay = targetInterval - dtMs;
                if (delay > 0) {
                    Thread.Sleep((int) delay);
                }
            }


            Console.WriteLine("StreamDeck found, press any key to exit.");
            Console.ReadKey();
            deck.Disconnect();
        }
    }
}
