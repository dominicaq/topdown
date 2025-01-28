using System;
using System.Runtime.InteropServices;

namespace FogOfWar
{
    // Ensures struct is packed to 1 byte
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TileData
    {
        // Packed data structure: visbility | Seen | Height
        private byte data;

        // Visible: 1 bit (bit 7)
        public bool Visible
        {
            get => (data & 0b10000000) != 0;
            set
            {
                if (value)
                    data |= 0b10000000; // Set bit 7 to 1 (visible).
                else
                    data &= 0b01111111; // Set bit 7 to 0 (not visible).
            }
        }

        // Seen: 1 bit (bit 6)
        public bool Seen
        {
            get => (data & 0b01000000) != 0;
            set
            {
                if (value)
                    data |= 0b01000000; // Set bit 6 to 1 (seen).
                else
                    data &= 0b10111111; // Set bit 6 to 0 (not seen).
            }
        }

        public bool ClientVisible // NOTE: dont use this var, client visible should be per agent
        {
            get => (data & 0b00100000) != 0;
            set
            {
                if (value)
                    data |= 0b00100000; // Set bit 5 to 1 (client can see).
                else
                    data &= 0b11011111; // Set bit 5 to 0 (client cant see).
            }
        }

        // Height: 5 bits (bits 0–4)
        public byte Height
        {
            get => (byte)(data & 0b00011111); // Extract bits 0–4.
            set
            {
                // Clear bits 0–4 and set new height.
                if (value > 31) throw new ArgumentOutOfRangeException(nameof(value), "Height must be in range 0–31.");
                data = (byte)((data & 0b11100000) | value);
            }
        }
    }
}
