using System;
using System.Buffers;

namespace Meadow.CLI.Core.DeviceManagement.Tools
{
    public static class CobsTools
    {
        //==============================================================
        // Consistent Overhead Byte Stuffing (COBS) is a scheme to take binary data
        // replace an arbituary byte value, usually 0x00, with an encoding that replaces
        // this value, in a way, that allows the orginal data can be recovered while
        // creating frames around the data.
        //
        // The following C# code was ported from a 'C' example licensed under MIT License
        // https://github.com/bakercp/PacketSerial/blob/master/src/Encoding/COBS.h.
        //	After porting, I found errors. I referred to the original authors paper at
        // http://conferences.sigcomm.org/sigcomm/1997/papers/p062.pdf for additional insights.
        // Modifications were needed and adding a starting offset to support large buffers was
        // added to allow sub-segments to be encoded.
        //
        public static int CobsEncoding(byte[] source, int startingSkip, int length,
                                    ref byte[] encoded, int encodeSkip)
        {
            int sourceOffset = startingSkip;  // Offset into source buffer
            // Add 1 because first byte filled with first replaceValue value
            int encodedOffset = 1;  // Offset into destination buffer
            int replaceOffset = 0;  // Offset where replaceValue is being tracked
            byte replaceValue = 1;  // Value of the offset to the next delimiter

            while (sourceOffset < length + startingSkip)
            {
                // Is source value the one we must replace?
                if (source[sourceOffset] == 0x00)
                {
                    encoded[encodeSkip + replaceOffset] = replaceValue;  // Replace original value with offset to replaceValue
                    replaceOffset = encodedOffset++;        // Update replace offset and bump encoded offset
                    replaceValue = 1;                       // Reset replaceValue
                }
                else
                {
                    encoded[encodeSkip + encodedOffset++] = source[sourceOffset]; // Just copy original value
                    replaceValue++;                                  // Keep zero offset tracker

                    // replaceValue has been tracking the delimiter offset. If it's  0xff then
                    // special action is needed, because we reached the end of a 254 byte block
                    // of data. And now encoding starts like at the first.
                    if (replaceValue == 0xff)                   // Signals maximum possible offset
                    {
                        encoded[encodeSkip + replaceOffset] = replaceValue;
                        replaceOffset = encodedOffset++;
                        replaceValue = 1;
                    }
                }
                sourceOffset++;                              // Point to next source value
            }

            // Last character
            encoded[encodeSkip + replaceOffset] = replaceValue;
            return encodedOffset;      // Number of bytes written to result buffer
        }

        //---------------------------------------------------------------------------
        public static int CobsDecoding(byte[] encoded, int length, ref byte[] decoded)
        {
            int encodedOffset = 0;      // Offset into original (encoded) buffer 
            int decodedOffset = 0;      // Offset into destination (decoded) buffer 
            byte replaceValue = 0;       // Value that will be inserted to indicate replaced value

            while (encodedOffset < length)
            {
                replaceValue = encoded[encodedOffset];  // Grab next byte

                if (((encodedOffset + replaceValue) > length) && (replaceValue != 1))
                    return 0;

                encodedOffset++;           // Point to next source

                // Copy all unchanged bytes
                // C# would Array.Copy be noticably better?
                for (int i = 1; i < replaceValue; i++)
                    decoded[decodedOffset++] = encoded[encodedOffset++];

                // Sometimes don't need a trailing delimiter added 
                if (replaceValue < 0xff && encodedOffset != length)
                    decoded[decodedOffset++] = 0x00;
            }

            return decodedOffset;
        }

        //---------------------------------------------------------------------------
        public static int CobsDecoding(Memory<byte> encoded, ref byte[] decoded)
        {
            int encodedOffset = 0; // Offset into original (encoded) buffer 
            int decodedOffset = 0; // Offset into destination (decoded) buffer 
            byte replaceValue = 0; // Value that will be inserted to indicate replaced value

            while (encodedOffset < encoded.Length)
            {
                replaceValue = encoded.Span[encodedOffset]; // Grab next byte

                if (((encodedOffset + replaceValue) > encoded.Length) && (replaceValue != 1))
                    return 0;

                encodedOffset++; // Point to next source

                // Copy all unchanged bytes
                // C# would Array.Copy be noticably better?
                for (int i = 1; i < replaceValue; i++)
                    decoded[decodedOffset++] = encoded.Span[encodedOffset++];

                // Sometimes don't need a trailing delimiter added 
                if (replaceValue < 0xff && encodedOffset != encoded.Length)
                    decoded[decodedOffset++] = 0x00;
            }

            return decodedOffset;
        }
    }
}
