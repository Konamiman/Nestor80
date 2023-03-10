using System.Diagnostics;

// Adapted from
// https://referencesource.microsoft.com/#PresentationCore/Shared/MS/Internal/Ink/BitStream.cs

namespace Konamiman.Nestor80.Linker.Infrastructure;

/// <summary>
/// A stream-style reader for retrieving packed bits from a byte array.
/// Bits are read from the leftmost position in each byte.
/// </summary>
public class BitStreamReader
{
    const int BITS_PER_BYTE = 8;
    const int BITS_PER_SHORT = 16;

    /// <summary>
    /// Create a new BitStreamReader to unpack the bits in a buffer of bytes
    /// </summary>
    /// <param name="buffer">Buffer of bytes</param>
    public BitStreamReader(byte[] buffer)
    {
        Debug.Assert(buffer != null);

        _byteArray = buffer;
        _bufferLengthInBits = (uint)buffer.Length * (uint)BITS_PER_BYTE;
    }

    /// <summary>
    /// Create a new BitStreamReader to unpack the bits in a buffer of bytes
    /// and enforce a maximum buffer read length
    /// </summary>
    /// <param name="buffer">Buffer of bytes</param>
    /// <param name="bufferLengthInBits">Maximum number of bytes to read from the buffer</param>
    public BitStreamReader(byte[] buffer, uint bufferLengthInBits)
        : this(buffer)
    {
        if(bufferLengthInBits > (buffer.Length * BITS_PER_BYTE)) {
            throw new ArgumentOutOfRangeException(nameof(bufferLengthInBits));
        }

        _bufferLengthInBits = bufferLengthInBits;
    }

    /// <summary>
    /// Read a single UInt16 from the byte[]
    /// </summary>
    /// <param name="countOfBits"></param>
    /// <returns></returns>
    public ushort ReadUInt16(int countOfBits)
    {
        // we only support 1-16 bits currently, not multiple bytes, and not 0 bits
        if(countOfBits > BITS_PER_SHORT || countOfBits <= 0) {
            throw new ArgumentOutOfRangeException(nameof(countOfBits));
        }

        ushort retVal = 0;
        while(countOfBits > 0) {
            int countToRead = (int)BITS_PER_BYTE;
            if(countOfBits < 8) {
                countToRead = countOfBits;
            }
            //make room
            retVal <<= countToRead;
            byte b = ReadByte(countToRead);
            retVal |= (ushort)b;
            countOfBits -= countToRead;
        }
        return (UInt16)((retVal << 8) + (retVal >> 8));
    }

    /// <summary>
    /// Reads a single bit from the buffer
    /// </summary>
    /// <returns></returns>
    public bool ReadBit()
    {
        byte b = ReadByte(1);
        return ((b & 1) == 1);
    }

    /// <summary>
    /// Read a specified number of bits from the stream into a single byte
    /// </summary>
    /// <param name="countOfBits">The number of bits to unpack</param>
    /// <returns>A single byte that contains up to 8 packed bits</returns>
    public byte ReadByte(int countOfBits)
    {
        // if the end of the stream has been reached, then throw an exception
        if(EndOfStream) {
            throw new OutOfDataException();
        }

        // we only support 1-8 bits currently, not multiple bytes, and not 0 bits
        if(countOfBits > BITS_PER_BYTE || countOfBits <= 0) {
            throw new ArgumentOutOfRangeException(nameof(countOfBits));
        }

        if(countOfBits > _bufferLengthInBits) {
            throw new OutOfDataException();
        }

        _bufferLengthInBits -= (uint)countOfBits;

        // initialize return byte to 0 before reading from the cache
        byte returnByte = 0;

        // if the partial bit cache contains more bits than requested, then read the
        //      cache only
        if(_cbitsInPartialByte >= countOfBits) {
            // retrieve the requested count of most significant bits from the cache
            //      and store them in the least significant positions in the return byte
            int rightShiftPartialByteBy = BITS_PER_BYTE - countOfBits;
            returnByte = (byte)(_partialByte >> rightShiftPartialByteBy);

            // reposition any unused portion of the cache in the most significant part of the bit cache
            unchecked // disable overflow checking since we are intentionally throwing away
                      //  the significant bits
            {
                _partialByte <<= countOfBits;
            }
            // update the bit count in the cache
            _cbitsInPartialByte -= countOfBits;
        }
        // otherwise, we need to retrieve more full bytes from the stream
        else {
            // retrieve the next full byte from the stream
            byte nextByte = _byteArray[_byteArrayIndex];
            _byteArrayIndex++;

            //right shift partial byte to get it ready to or with the partial next byte
            int rightShiftPartialByteBy = BITS_PER_BYTE - countOfBits;
            returnByte = (byte)(_partialByte >> rightShiftPartialByteBy);

            // now copy the remaining chunk of the newly retrieved full byte
            int rightShiftNextByteBy = Math.Abs((countOfBits - _cbitsInPartialByte) - BITS_PER_BYTE);
            returnByte |= (byte)(nextByte >> rightShiftNextByteBy);

            // update the partial bit cache with the remainder of the newly retrieved full byte
            unchecked // disable overflow checking since we are intentionally throwing away
                      //  the significant bits
            {
                _partialByte = (byte)(nextByte << (countOfBits - _cbitsInPartialByte));
            }

            _cbitsInPartialByte = BITS_PER_BYTE - (countOfBits - _cbitsInPartialByte);
        }
        return returnByte;
    }

    /// <summary>
    /// Since the return value of Read cannot distinguish between valid and invalid
    /// data (e.g. 8 bits set), the EndOfStream property detects when there is no more
    /// data to read.
    /// </summary>
    /// <value>True if stream end has been reached</value>
    public bool EndOfStream
    {
        get
        {
            return 0 == _bufferLengthInBits;
        }
    }

    /// <summary>
    /// Advance the stream pointer as needed so it points to the start of the next byte.
    /// </summary>
    public void ForceByteBoundary()
    {
        if(_cbitsInPartialByte > 0) ReadByte(_cbitsInPartialByte);
    }

    /// <summary>
    /// Force byte boundary, then read directly the specified number of bytes.
    /// </summary>
    public byte[] ReadDirectBytes(int length) 
    {
        ForceByteBoundary();
        var result = new byte[length];
        try {
            Array.Copy(_byteArray, _byteArrayIndex, result, 0, length);
        }
        catch {
            throw new OutOfDataException();
        }
        _byteArrayIndex += length;
        return result;
    }

    public byte[] PeekBytes(int length)
    {
        if(_cbitsInPartialByte != 0) {
            throw new InvalidOperationException($"{nameof(BitStreamReader)}.{nameof(PeekBytes)}: not at a byte boundary ({_cbitsInPartialByte} bits left from last byte)");
        }

        length = Math.Min(length, _byteArray.Length - _byteArrayIndex);
        return _byteArray.Skip(_byteArrayIndex).Take(length).ToArray();
    }

    public void DiscardBytes(int length)
    {
        if(_cbitsInPartialByte != 0) {
            throw new InvalidOperationException($"{nameof(BitStreamReader)}.{nameof(DiscardBytes)}: not at a byte boundary ({_cbitsInPartialByte} bits left from last byte)");
        }

        length = Math.Min(length, _byteArray.Length - _byteArrayIndex);
        _byteArrayIndex += length;
    }

    // reference to the source byte buffer to read from
    private readonly byte[] _byteArray = null;

    // maximum length of buffer to read in bits
    private uint _bufferLengthInBits = 0;

    // the index in the source buffer for the next byte to be read
    private int _byteArrayIndex = 0;

    // since the bits from multiple inputs can be packed into a single byte
    //  (e.g. 2 bits per input fits 4 per byte), we use this field as a cache
    //  of the remaining partial bits.
    private byte _partialByte = 0;

    // the number of bits (partial byte) left to read in the overlapped byte field
    private int _cbitsInPartialByte = 0;
}
