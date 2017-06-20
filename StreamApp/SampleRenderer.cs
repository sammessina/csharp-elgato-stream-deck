using System;
using System.Drawing;
using System.Drawing.Text;
using StreamDeckLibrary;

namespace StreamApp {
    public class SampleRenderer : IApp {
        private Brush blackBrush = new SolidBrush(Color.Black);
        private Paddle p1, p2;
        private Ball ball = new Ball();

        Font font = new Font(new FontFamily("Consolas"), 26, FontStyle.Regular, GraphicsUnit.Pixel);
        SolidBrush fontColor = new SolidBrush(Color.FromArgb(100, 255, 255, 255));

        private bool[] keyStates = new bool[15];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="screen"></param>
        /// <param name="graphics"></param>
        public void Update(double dt, Bitmap screen, Graphics graphics) {
            p1.Update(dt, screen, ball.dx == -1 && ball.X + ball.Size / 2 < screen.Width * 2 / 3, ball, keyStates[4], keyStates[14]);
            p2.Update(dt, screen, ball.dx == 1 && ball.X + ball.Size / 2 > screen.Width * 1 / 3, ball, keyStates[4], keyStates[10]);
            ball.Update(dt, screen);
            ball.UpdateCollision(p1, p2, screen);
        }

        public void Render(double dt, Bitmap screen, Graphics graphics) {
            // Clear the screen
            graphics.FillRectangle(blackBrush, 0, 0, screen.Width, screen.Height);


            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            graphics.DrawString(p1.Score.ToString(), font, fontColor, (StreamDeck.IconSize + StreamDeck.BezelWidth) + 10, 10);
            graphics.DrawString(p2.Score.ToString(), font, fontColor, (StreamDeck.IconSize + StreamDeck.BezelWidth) * 3 + 10, 10);

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
            private double Speed = 300;

            public const int Width = 10;
            Pen pen = new Pen(Color.White, Width);
            public const int Height = 75;
            public double X { get; private set; }
            public double Y { get; private set; }
            public int Score { get; set; }

            public Paddle(double x, double y) {
                this.X = x;
                this.Y = y;
            }

            public void Update(double dt, Bitmap screen, bool autoMove, Ball ball, bool up, bool down) {
                if (autoMove) {
                    // Automate input
                    up = ball.Y + ball.Size / 2 < Y + Height / 2;
                    down = ball.Y + ball.Size / 2 > Y + Height / 2;
                    Speed = 1000;
                }


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
            public double X { get; private set; } = 100;
            public double Y { get; private set; } = 100;
            public int Size { get; private set; } = 20;
            private Random rand = new Random();

            public double dx = 1, dy = 1;
            private double speedInPxPerSec = 200;
            private const double acceleration = 50; // How fast the velocity increases, in px/sec^2
            Pen whitePen = new Pen(Color.DeepPink, 5);
            Pen whitePen2 = new Pen(Color.Aqua, 5);

            public void Update(double dt, Bitmap screen) {
                X += dx * speedInPxPerSec * dt;
                Y += dy * speedInPxPerSec * dt;

                speedInPxPerSec += acceleration * dt;

                if ((dx > 0 && X + Size > screen.Width) || (dx < 0 && X < 0)) {
                    dx *= -1;
                }
                if ((dy > 0 && Y + Size > screen.Height) || (dy < 0 && Y < 0)) {
                    dy *= -1;
                }
            }

            public void Render(Graphics graphics) {
                graphics.DrawRectangle(whitePen, (int)(X), (int)(Y), Size, Size);
                graphics.DrawRectangle(whitePen2, (int)(X + 5), (int)(Y + 5), Size / 2, Size / 2);
            }

            public void UpdateCollision(Paddle p1, Paddle p2, Bitmap screen) {
                // Test collision with paddle 1
                if (X < p1.X + Paddle.Width && Y + Size > p1.Y && Y < p1.Y + Paddle.Height) {
                    X = p1.X + Paddle.Width;
                    dx = Math.Abs(dx);
                    return;
                }

                // Test collision with paddle 2
                if (X + Size > p2.X && Y + Size > p2.Y && Y < p2.Y + Paddle.Height) {
                    X = p2.X - Size;
                    dx = -Math.Abs(dx);
                    return;
                }

                // Test collision with outer walls
                if (X < 0) {
                    p2.Score++;
                }

                if (X > p2.X + Paddle.Width) {
                    p1.Score++;
                }

                if (X < 0 || X > p2.X + Paddle.Width) {
                    Reset(screen);
                }
            }

            private void Reset(Bitmap screen) {
                X = screen.Width / 2 - Size / 2;
                Y = screen.Height / 2 - Size / 2;
                dx = rand.Next(2) * 2 - 1;
                dy = (rand.NextDouble() + .5) * (rand.Next(2) * 2 - 1);
                //dy = rand.Next(2) * 2 - 1;

                speedInPxPerSec = 200;
            }
        }
    }
}