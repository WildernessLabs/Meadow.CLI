using System;

namespace MeadowCLI.Hcom
{
	//==============================================================
	// Circular Buffer
	// This circular buffer was written for the Meadow F7. Unlike the classic
	// version, this version has a byte array as input (received data) and
	// returns a byte array consisting of everything from the head to the
	// first byte whose value is 0. It's designed to work with the COBS
	// encoding scheme.
	public enum HcomBufferReturn
	{
		// Leaving in this form to match 'C' coding style
		HCOM_CIR_BUF_INIT_OK,
		HCOM_CIR_BUF_INIT_FAILED,

		HCOM_CIR_BUF_GET_FOUND_MSG,
		HCOM_CIR_BUF_GET_NONE_FOUND,
		HCOM_CIR_BUF_GET_BUF_NO_ROOM,

		HCOM_CIR_BUF_ADD_SUCCESS,
		HCOM_CIR_BUF_ADD_WONT_FIT,
		HCOM_CIR_BUF_ADD_BAD_ARG
	}

	public class HostCommBuffer
	{
		byte[] hcomCircularBuffer;
		int bottom;  // bottom of buffer
		int top;     // top end of buffer
		int head;
		int tail;
        readonly object bufferLock = new object();

		//------------------------------------------------------------------------------
		public HcomBufferReturn Init(int totalCapacity)
		{
			hcomCircularBuffer = new byte[totalCapacity];
			bottom = 0;
			top = bottom + totalCapacity;
			head = bottom;
			tail = bottom;

			for(int i = 0; i < hcomCircularBuffer.Length; i++)
			{
				hcomCircularBuffer[i] = 0xff;
			}

			return HcomBufferReturn.HCOM_CIR_BUF_INIT_OK;
		}

		//------------------------------------------------------------------------------
		private int GetAvailableSpace()
		{
			// We leave one free byte so the head and tail are equal only if
			// empty not when full. Full means 1 unused byte.
			if (head < tail)
				return tail - head - 1;
			else
				return ((top - bottom) - (head - tail)) - 1;
		}

		//------------------------------------------------------------------------------
		// Adds 1 to n bytes to the head of the circular buffer
		public HcomBufferReturn AddBytes(byte[] newBytes, int bytesOffset, int bytesToAdd)
		{
			lock (bufferLock)
			{
				if (bytesToAdd == 0)
					return HcomBufferReturn.HCOM_CIR_BUF_ADD_BAD_ARG;

				if (GetAvailableSpace() < bytesToAdd)
					return HcomBufferReturn.HCOM_CIR_BUF_ADD_WONT_FIT;

				int newHead = head + bytesToAdd;
				if (newHead < top)
				{
					// Simple case (no wrap around)
					Array.Copy(newBytes, bytesOffset, hcomCircularBuffer, head, bytesToAdd);
					head = newHead;
				}
				else
				{
					// Wrap around - fill up head-top space and use bottom space too
					int spaceAvailOnTop = top - head;
					Array.Copy(newBytes, bytesOffset, hcomCircularBuffer, head, spaceAvailOnTop);
					Array.Copy(newBytes, spaceAvailOnTop + bytesOffset, hcomCircularBuffer, 
						bytesToAdd - spaceAvailOnTop, bytesToAdd);
					head = bottom + bytesToAdd - spaceAvailOnTop;
				}
				return HcomBufferReturn.HCOM_CIR_BUF_ADD_SUCCESS;
			}
		}

		//------------------------------------------------------------------------------
		// Caller must supply packetBuffer and the buffers size
		public HcomBufferReturn GetNextPacket(byte[] packetBuffer,
				int packetBufferSize, out int packetLength)
		{
			lock (bufferLock)
			{
				int foundOffset;
				int sizeFoundTop;

				packetLength = 0;
				if (head == tail)
					return HcomBufferReturn.HCOM_CIR_BUF_GET_NONE_FOUND;

				// Scan the buffer looking for the delimiter 0x00
				if (head > tail)
				{
					// Simple case (no wrap around)
					foundOffset = Array.IndexOf(hcomCircularBuffer, (byte)0x00, tail, head - tail);
					if (foundOffset == -1)
						return HcomBufferReturn.HCOM_CIR_BUF_GET_NONE_FOUND;
				}
				else
				{
					foundOffset = Array.IndexOf(hcomCircularBuffer, (byte)0x00, tail, top - tail);
				}

				if (foundOffset != -1)
				{
					// Found the delimiter, message in one contiguous block
					sizeFoundTop = foundOffset - tail + 1;
					if (sizeFoundTop > packetBufferSize)
						return HcomBufferReturn.HCOM_CIR_BUF_GET_BUF_NO_ROOM;

					Array.Copy(hcomCircularBuffer, tail, packetBuffer, 0, sizeFoundTop);

					// memcpy(packetBuffer, tail, sizeFoundTop);
					tail = foundOffset + 1;
					packetLength = sizeFoundTop;
					return HcomBufferReturn.HCOM_CIR_BUF_GET_FOUND_MSG;
				}

				// Continue looking for the delimiter from the bottom up since we got here
				// because the delimiter was not found while scanning
				foundOffset = Array.IndexOf(hcomCircularBuffer, (byte)0x00, bottom, head - bottom);
				if (foundOffset == -1)
					return HcomBufferReturn.HCOM_CIR_BUF_GET_NONE_FOUND;

				sizeFoundTop = top - tail;
				int sizeFoundBottom = foundOffset - bottom + 1;
				if (sizeFoundBottom + sizeFoundTop > packetBufferSize)
					return HcomBufferReturn.HCOM_CIR_BUF_GET_BUF_NO_ROOM;

				Array.Copy(hcomCircularBuffer, tail, packetBuffer, 0, sizeFoundTop);
				Array.Copy(hcomCircularBuffer, bottom, packetBuffer, sizeFoundTop, sizeFoundBottom);

				tail = foundOffset + 1;
				packetLength = sizeFoundTop + sizeFoundBottom;
				return HcomBufferReturn.HCOM_CIR_BUF_GET_FOUND_MSG;
			}
		}
	}
}

