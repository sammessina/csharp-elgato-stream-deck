using System;
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
            
            deck.SetBrightness(100);

            Random rand = new Random();

            while (true) {
                int r = rand.Next(0, 2)*255;
                int g = rand.Next(0, 2) * 255;
                int b = r = g = rand.Next(0, 2) * 255;

                for (int row = 0; row < 1; row++) {
                    for (int i = 4; i >= 4; i--) {
                        deck.WriteImage(row*5+i, r, g, b);
                        
                    }
                    //Thread.Sleep(50);
                }
            }


            /*
                        deck.SetBrightness(0);
                        Thread.Sleep(1000);
                        deck.SetBrightness(90);
                        Thread.Sleep(1000);

                        for (int i = 50; i < 100; i++) {
                            deck.SetBrightness(i);
                            Thread.Sleep(100);
                        }*/

            //_device.WriteFeatureData(SetBrightnessMessage.data);
            //_device.WriteReport(new SetBrightnessMessage());


            Console.WriteLine("StreamDeck found, press any key to exit.");
            Console.ReadKey();
            deck.Disconnect();
        }
    }
}
