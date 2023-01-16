namespace Konamiman.Nestor80.Assembler.Relocatable
{
    /// <summary>
    /// A stream-like writer for packing bits into a byte buffer. This is used to
    /// generate relocatable files compatible with LINK-80.
    /// 
    /// Class adapted from
    /// https://referencesource.microsoft.com/#PresentationCore/Shared/MS/Internal/Ink/BitStream.cs
    /// </summary>
    public class BitStreamWriter
    {
        /// <summary>
        /// Create a new bit writer that writes to the target buffer
        /// </summary>
        /// <param name="bufferToWriteTo"></param>
        public BitStreamWriter(List<byte> bufferToWriteTo)
        {
            if (bufferToWriteTo == null)
            {
                throw new ArgumentNullException("bufferToWriteTo");
            }
            _targetBuffer = bufferToWriteTo;
        }

        /// <summary>
        /// Writes the count of bits from the int to the left packed buffer
        /// </summary>
        /// <param name="bits"></param>
        /// <param name="countOfBits"></param>
        public void Write(uint bits, int countOfBits)
        {
            // validate that a subset of the bits in a single byte are being written
            if (countOfBits <= 0 || countOfBits > Native.BitsPerInt)
                throw new ArgumentOutOfRangeException("countOfBits");


            // calculate the number of full bytes
            //   Example: 10 bits would require 1 full byte
            int fullBytes = countOfBits / Native.BitsPerByte;

            // calculate the number of bits that spill beyond the full byte boundary
            //   Example: 10 buttons would require 2 extra bits (8 fit in a full byte)
            int bitsToWrite = countOfBits % Native.BitsPerByte;

            for (; fullBytes >= 0; fullBytes--)
            {
                byte byteOfData = (byte)(bits >> fullBytes * Native.BitsPerByte);
                //
                // write 8 or less bytes to the bitwriter
                // checking for 0 handles the case where we're writing 8, 16 or 24 bytes
                // and bitsToWrite is initialize to zero
                //
                if (bitsToWrite > 0)
                {
                    Write(byteOfData, bitsToWrite);
                }
                if (fullBytes > 0)
                {
                    bitsToWrite = Native.BitsPerByte;
                }
            }
        }

        /// <summary>
        /// Writes the count of bits from the int to the buffer in reverse order
        /// </summary>
        /// <param name="bits"></param>
        /// <param name="countOfBits"></param>
        public void WriteReverse(uint bits, int countOfBits)
        {
            // validate that a subset of the bits in a single byte are being written
            if (countOfBits <= 0 || countOfBits > Native.BitsPerInt)
                throw new ArgumentOutOfRangeException("countOfBits");

            // calculate the number of full bytes
            //   Example: 10 bits would require 1 full byte
            int fullBytes = countOfBits / Native.BitsPerByte;

            // calculate the number of bits that spill beyond the full byte boundary
            //   Example: 10 buttons would require 2 extra bits (8 fit in a full byte)
            int bitsToWrite = countOfBits % Native.BitsPerByte;
            if (bitsToWrite > 0)
            {
                fullBytes++;
            }
            for (int x = 0; x < fullBytes; x++)
            {
                byte byteOfData = (byte)(bits >> x * Native.BitsPerByte);
                Write(byteOfData, Native.BitsPerByte);
            }
        }

        /// <summary>
        /// Write a specific number of bits from byte input into the stream
        /// </summary>
        /// <param name="bits">The byte to read the bits from</param>
        /// <param name="countOfBits">The number of bits to read</param>
        public void Write(byte bits, int countOfBits)
        {
            // validate that a subset of the bits in a single byte are being written
            if (countOfBits <= 0 || countOfBits > Native.BitsPerByte)
                throw new ArgumentOutOfRangeException("countOfBits");

            byte buffer;
            // if there is remaining bits in the last byte in the stream
            //      then use those first
            if (_remaining > 0)
            {
                // retrieve the last byte from the stream, update it, and then replace it
                buffer = _targetBuffer[_targetBuffer.Count - 1];
                // if the remaining bits aren't enough then just copy the significant bits
                //      of the input into the remainder
                if (countOfBits > _remaining)
                {
                    buffer |= (byte)((bits & 0xFF >> Native.BitsPerByte - countOfBits) >> countOfBits - _remaining);
                }
                // otherwise, copy the entire set of input bits into the remainder
                else
                {
                    buffer |= (byte)((bits & 0xFF >> Native.BitsPerByte - countOfBits) << _remaining - countOfBits);
                }
                _targetBuffer[_targetBuffer.Count - 1] = buffer;
            }

            // if the remainder wasn't large enough to hold the entire input set
            if (countOfBits > _remaining)
            {
                // then copy the uncontained portion of the input set into a temporary byte
                _remaining = Native.BitsPerByte - (countOfBits - _remaining);
                unchecked // disable overflow checking since we are intentionally throwing away
                          //  the significant bits
                {
                    buffer = (byte)(bits << _remaining);
                }
                // and add it to the target buffer
                _targetBuffer.Add(buffer);
            }
            else
            {
                // otherwise, simply update the amount of remaining bits we have to spare
                _remaining -= countOfBits;
            }
        }

        public void ForceByteBoundary()
        {
            if (_remaining > 0) Write(0, _remaining);
        }

        // the buffer that the bits are written into
        private List<byte> _targetBuffer = null;

        // number of free bits remaining in the last byte added to the target buffer
        private int _remaining = 0;
    }
}
