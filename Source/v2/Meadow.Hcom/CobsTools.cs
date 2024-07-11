namespace Meadow.Hcom;

internal static class CobsTools
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
	public static async Task<(int encodedOffset, byte[] encoded)> CobsEncoding(byte[] source, int startingSkip, int length, int encodingLength, int encodeSkip)
	{
		int sourceOffset = startingSkip;  // Offset into source buffer
		int encodedOffset = 1;  // Offset into destination buffer, starts at 1 due to COBS encoding specifics
		int replaceOffset = 0;  // Offset where replaceValue is being tracked
		byte replaceValue = 1;  // Value of the offset to the next delimiter
		byte[] encoded = new byte[encodingLength + 2]; // Allocate enough space for encoding

		try
		{
			while (sourceOffset < length + startingSkip)
			{
				if (source[sourceOffset] == 0x00)
				{
					// Replace original value with offset to replaceValue
					encoded[encodeSkip + replaceOffset] = replaceValue;
					replaceOffset = encodedOffset++; // Update replace offset and bump encoded offset
					replaceValue = 1; // Reset replaceValue
				}
				else
				{
					if (encodeSkip + encodedOffset >= encoded.Length)
					{
						Console.WriteLine("encodeSkip + encodedOffset >= encoded.Length");
						return (-1, Array.Empty<byte>()); // Return error state
					}
					// Just copy original value
					encoded[encodeSkip + encodedOffset++] = source[sourceOffset];
					replaceValue++; // Keep zero offset tracker

					if (replaceValue == 0xff) // Signals maximum possible offset
					{
						encoded[encodeSkip + replaceOffset] = replaceValue;
						replaceOffset = encodedOffset++;
						replaceValue = 1;
					}
				}
				sourceOffset++; // Point to next source value
			}
		}
		catch (Exception except)
		{
			Console.WriteLine($"An exception was caught: {except}");
			await Task.Delay(10 * 60 * 1000); // Asynchronously wait for 10 minutes before re-throwing
			throw;
		}

		// Last character
		encoded[encodeSkip + replaceOffset] = replaceValue;

		return (encodedOffset, encoded); // Return both the encoded offset and the encoded data
	}

	//---------------------------------------------------------------------------
	public static async Task<(int encodedOffset, byte[] decoded)> CobsDecoding(byte[] encoded, int length, int decodedBufferLength)
	{
		int encodedOffset = 0;      // Offset into original (encoded) buffer 
		int decodedOffset = 0;      // Offset into destination (decoded) buffer 
		byte replaceValue = 0;      // Value that will be inserted to indicate replaced value
		byte[] decoded = new byte[decodedBufferLength + 2];

		while (encodedOffset < length)
		{
			replaceValue = encoded[encodedOffset];  // Grab next byte

			if (((encodedOffset + replaceValue) > length) && (replaceValue != 1))
				return (0, Array.Empty<byte>());

			encodedOffset++;           // Point to next source

			// Copy all unchanged bytes
			// C# would Array.Copy be noticably better?
			for (int i = 1; i < replaceValue; i++)
				decoded[decodedOffset++] = encoded[encodedOffset++];

			// Sometimes don't need a trailing delimiter added 
			if (replaceValue < 0xff && encodedOffset != length)
				decoded[decodedOffset++] = 0x00;
		}

		return (decodedOffset, decoded);
	}
}