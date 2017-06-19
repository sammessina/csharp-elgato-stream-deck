using System.Drawing;

namespace StreamApp {
    public interface IApp {
        void OnKeyDown(int keyIndex);
        void OnKeyUp(int keyIndex);
        void Init(int width, int height);
        void Update(double dt, Bitmap screen, Graphics graphics);
        void Render(double dt, Bitmap screen, Graphics graphics);
    }
}