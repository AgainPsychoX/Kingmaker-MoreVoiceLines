using System;
using System.IO;
using System.IO.Pipes;

namespace MoreVoiceLines.IPC
{
    /// <summary>
    /// Helper class to write message to stream in specific format.
    /// </summary>
    public class MessageWriteable : BinaryWriter
    {
        public MessageType Type
        {
            get
            {
                return (MessageType)BitConverter.ToInt32(GetBuffer(), 0);
            }
            set
            {
                Buffer.BlockCopy(BitConverter.GetBytes((int)value), 0, GetBuffer(), 0, 4);
            }
        }

        public int Length
        {
            get
            {
                return (int) GetMemoryStream().Length;
            }
            set
            {
                if (value < sizeof(int) * 2)
                {
                    throw new ArgumentException("Message too short (should be at least 8 for empty message)");
                }
                GetMemoryStream().SetLength(value);
            }
        }

        public MemoryStream GetMemoryStream() {
            return (MemoryStream)BaseStream;
        }

        public byte[] GetBuffer()
        {
            return GetMemoryStream().GetBuffer();
        }

        public MessageWriteable(MessageType messageType = MessageType.None, int initialDataCapacity = 480)
            : base(new MemoryStream(sizeof(int) * 2 + initialDataCapacity))
        {
            // Set length first, as it will clear fresh bytes
            Length = sizeof(int) * 2;
            Type = messageType;

            // Move position after the header to write data
            BaseStream.Position = 8;
        }

        protected new void Dispose()
        {
            GetMemoryStream().Dispose();
            base.Dispose();
        }

        public void Send(Stream output, bool close = true)
        {
            // Update length
            BaseStream.Position = 4;
            Write(Length);

            // Write to output
            try
            {
                output.Write(GetMemoryStream().GetBuffer(), 0, Length);
            }
            finally
            {
                if (close) Close();
            }
        }

        public bool TrySend(Stream output, bool close = true)
        {
            if (output == null || !output.CanWrite)
            {
                if (close) Close();
                return false;
            }
            Send(output, true);
            return true;
        }

        public bool TrySend(PipeStream? output, bool close = true)
        {
            if (output == null || !output.CanWrite || !output.IsConnected)
            {
                if (close) Close();
                return false;
            }
            Send(output, true);
            return true;
        }
    }
}
