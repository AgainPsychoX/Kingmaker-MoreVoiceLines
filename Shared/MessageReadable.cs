using System;
using System.IO;
using System.Threading.Tasks;

namespace MoreVoiceLines.IPC
{
    /// <summary>
    /// Helper class to read message from stream in specific format.
    /// </summary>
    public class MessageReadable : BinaryReader
    {
        public MessageType Type
        {
            get
            {
                return (MessageType)BitConverter.ToInt32(GetBuffer(), 0);
            }
            private set
            {
                Buffer.BlockCopy(BitConverter.GetBytes((int)value), 0, GetBuffer(), 0, 4);
            }
        }

        public int Length
        {
            get
            {
                return (int)GetMemoryStream().Length;
            }
            private set
            {
                if (value < sizeof(int) * 2)
                {
                    throw new ArgumentException();
                }
                GetMemoryStream().SetLength(value);
            }
        }

        public MemoryStream GetMemoryStream()
        {
            return (MemoryStream)BaseStream;
        }

        public byte[] GetBuffer()
        {
            return GetMemoryStream().GetBuffer();
        }

        public MessageReadable(int initialDataCapacity = 480)
            : base(new MemoryStream(sizeof(int) * 2 + initialDataCapacity))
        {
            // Length equals to capacity initially to avoid clearing fresh bytes
            Length = GetMemoryStream().Capacity;
            Type = MessageType.Unknown;
        }

        protected new void Dispose()
        {
            GetMemoryStream().Dispose();
            base.Dispose();
        }

        public async Task ReceiveAsync(Stream inputStream)
        {
            var inputBuffer = GetBuffer();
            var receivedLength = 0;

            // Read at least message type & length (incl. this header)
            do
            {
                var more = await inputStream.ReadAsync(inputBuffer, receivedLength, inputBuffer.Length - receivedLength);
                if (more == 0) return; // end of stream
                receivedLength += more;
            }
            while (receivedLength < sizeof(int) * 2);
            Type = (MessageType)BitConverter.ToInt32(inputBuffer, 0);
            var messageLength = Length = BitConverter.ToInt32(inputBuffer, 4);

            // Read remaining message length if necessary
            // TODO: handle messages larger than default input buffer length by resizing the buffer
            while (receivedLength < messageLength && receivedLength < inputBuffer.Length)
            {
                var more = await inputStream.ReadAsync(inputBuffer, receivedLength, inputBuffer.Length - receivedLength);
                if (more == 0) return; // end of stream (but shouldn't it throw if mid-message?)
                receivedLength += more;
            }

            // Move position after the header to read data
            BaseStream.Position = 8;
        }
    }
}
