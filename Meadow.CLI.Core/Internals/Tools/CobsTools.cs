using System;
namespace MeadowCLI.Hcom
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
        public static int CobsEncoding(byte[] source, int startingOffset, int length, ref byte[] encoded)
        {
            int sourceOffset = startingOffset;  // Offset into source buffer
            int encodedOffset = 1;  // Offset into destination buffer
            int replaceOffset = 0;  // Offset where replacement is being tracked
            byte replacement = 1;  // Value that will be inserted to indicate replaced value

            while (sourceOffset < length + startingOffset)
            {
                // If source value is the one we want to replace?
                if (source[sourceOffset] == 0x00)
                {
                    encoded[replaceOffset] = replacement;  // Replace original value with offset to replacement
                    replaceOffset = encodedOffset++;       // Update replacement offset and bump encoded offset
                    replacement = 1;                       // Reset replacement
                }
                else
                {
                    encoded[encodedOffset++] = source[sourceOffset];   // Just copy original value
                    replacement++;

                    if (replacement == 0xff)                     // Maximum possible offset
                    {
                        encoded[replaceOffset] = replacement;  // see above for identical code
                        replaceOffset = encodedOffset++;
                        replacement = 1;
                    }
                }
                sourceOffset++;                              // Point to next source value
            }

            encoded[replaceOffset] = replacement;
            return encodedOffset;      // Number of bytes written to result buffer
        }

        //---------------------------------------------------------------------------
        public static int CobsDecoding(byte[] encoded, int length, ref byte[] decoded)
        {
            int encodedOffset = 0;     // Offset into original (encoded) buffer 
            int decodedOffset = 0;     // Offset into destination (decoded) buffer 
            byte replacement = 0;     // Value that will be inserted to indicate replaced value

            while (encodedOffset < length)
            {
                replacement = encoded[encodedOffset];  // Grab next byte

                if (((encodedOffset + replacement) > length) && (replacement != 1))
                    return 0;

                encodedOffset++;           // Point to next source

                // Copy all unchanged bytes
                // C# would Array.Copy be noticably better?
                for (int i = 1; i < replacement; i++)
                    decoded[decodedOffset++] = encoded[encodedOffset++];

                // Sometimes don't need a trailing delimiter added 
                if (replacement < 0xff && encodedOffset != length)
                    decoded[decodedOffset++] = 0x00;
            }

            return decodedOffset;
        }
    }
}
