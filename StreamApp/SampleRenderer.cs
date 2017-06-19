using System;
using System.Drawing;
using System.Drawing.Text;

namespace StreamApp {
    public class SampleRenderer : IApp {
        private Brush blackBrush = new SolidBrush(Color.Black);
        private Paddle p1, p2;
        private Ball ball = new Ball();

        Font font = new Font(new FontFamily("Segoe UI"), 32, FontStyle.Regular, GraphicsUnit.Pixel);
        SolidBrush solidBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));

        private bool[] keyStates = new bool[15];
        
        public void Update(double dt, Bitmap screen, Graphics graphics) {

            p1.Update(dt, screen, keyStates[4], keyStates[14]);
            p2.Update(dt, screen, keyStates[0], keyStates[10]);
            ball.Update(dt, screen);
            ball.UpdateCollision(p1, p2, screen);
        }

        public void Render(double dt, Bitmap screen, Graphics graphics) {
            // Clear the screen
            graphics.FillRectangle(blackBrush, 0, 0, screen.Width, screen.Height);
            

            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            graphics.DrawString(p1.Score.ToString(), font, solidBrush, 110, 10);
            graphics.DrawString(p2.Score.ToString(), font, solidBrush, 320, 10);

            ball.Render(graphics);
            p1.Render(graphics);
            p2.Render(graphics);
        }

        public void OnKeyDown(int keyIndex) {
            keyStates[keyIndex] = true;
        }

        public void OnKeyUp(int keyIndex) {
            keyStates[keyIndex] = false;
        }

        public void Init(int width, int height) {
            p1 = new Paddle(0, 0);
            p2 = new Paddle(width - Paddle.Width, 0);
        }


        private class Paddle {
            private const double Speed = 300;
            
            public const int Width = 15;
            Pen pen = new Pen(Color.White, Width);
            public const int Height = 100;
            public double X { get; private set; }
            public double Y { get; private set; }
            public int Score { get; set; }

            public Paddle(double x, double y) {
                this.X = x;
                this.Y = y;
            }

            public void Update(double dt, Bitmap screen, bool up, bool down) {
                if (up == down) {
                    return;
                }

                this.Y += Speed * dt * (up ? -1 : 1);
                if (this.Y < 0) {
                    this.Y = 0;
                }

                if (this.Y + Height > screen.Height) {
                    this.Y = screen.Height - Height;
                }
            }

            public void Render(Graphics graphics) {
                graphics.DrawLine(pen, (int)X + Width / 2, (int)Y, (int)X + Width / 2, (int)Y + Height);
            }
        }

        private class Ball {
            // Coordinate is the top-left of the ball
            private double x = 100;
            private double y = 100;
            private int diameter = 20;
            private Random rand = new Random();

            private int dx = 1, dy = 1;
            private double speedInPxPerSec = 200;
            Pen whitePen = new Pen(Color.DeepPink, 5);
            Pen whitePen2 = new Pen(Color.Aqua, 5);

            public void Update(double dt, Bitmap screen) {
                x += dx * speedInPxPerSec * dt;
                y += dy * speedInPxPerSec * dt;

                if ((dx == 1 && x > screen.Width) || (dx == -1 && x < 0)) {
                    dx *= -1;
                }
                if ((dy == 1 && y > screen.Height) || (dy == -1 && y < 0)) {
                    dy *= -1;
                }
            }

            public void Render(Graphics graphics) {
                graphics.DrawRectangle(whitePen, (int)(x), (int)(y), diameter, diameter);
                graphics.DrawRectangle(whitePen2, (int)(x + 5), (int)(y + 5), diameter/2, diameter/2);
            }

            public void UpdateCollision(Paddle p1, Paddle p2, Bitmap screen) {
                // Test collision with paddle 1
                if (x < p1.X + Paddle.Width && y + diameter > p1.Y && y < p1.Y + Paddle.Height) {
                    x = p1.X + Paddle.Width;
                    dx *= -1;
                    return;
                }

                // Test collision with paddle 2
                if (x + diameter > p2.X && y + diameter > p2.Y && y < p2.Y + Paddle.Height) {
                    x = p2.X - diameter;
                    dx *= -1;
                    return;
                }

                // Test collision with outer walls
                if (x < 0) {
                    p2.Score++;
                }

                if (x > p2.X + Paddle.Width) {
                    p1.Score++;
                }

                if (x < 0 || x > p2.X + Paddle.Width) {
                    x = screen.Width / 2 - diameter / 2;
                    y = screen.Height / 2 - diameter / 2;
                    dx = rand.Next(2) * 2 - 1;
                    dy = rand.Next(2) * 2 - 1;
                }
            }
        }
    }
}