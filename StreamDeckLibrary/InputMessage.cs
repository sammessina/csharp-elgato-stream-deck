using System;

namespace StreamDeckLibrary {
    public class InputMessage {
        private readonly byte[] data;

        public InputMessage() {
            data = new byte[16];
        }

        public InputMessage(byte[] data) {
            this.data = data;
        }

        public bool IsButtonPressed(int keyIndex) {
            if (keyIndex < 0 || keyIndex > 14) {
                throw new ArgumentOutOfRangeException(nameof(keyIndex), keyIndex, "Value must be between 0-14.");
            }

            return data[keyIndex] != 0;
        }
    }
}