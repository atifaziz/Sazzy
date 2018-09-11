namespace Sazzy
{
    using System;
    using System.IO;
    using System.Text;
    public static class StreamExtension
    {
        public static string ReadLine(this Stream stream)
        {
            if (!stream.CanRead || stream.Position == stream.Length)
                return null;

            StringBuilder result = new StringBuilder();
            int b;
            char character;
            while ((b = stream.ReadByte()) > 0 && ((character = (char) b) != '\n')) //TODO what if \n\r\n
            {
                if (character != '\r' && character != '\n')
                    result.Append(character);
            }
            return result.ToString();
        }
        public static string ReadToEnd(this Stream stream, int contentLength)
        {
            if (contentLength < 0)
                throw new ArgumentOutOfRangeException();
            if (!stream.CanRead || stream.Position == stream.Length)
                return null;

            byte[] buffer = new byte[contentLength];
            StringBuilder result = new StringBuilder();

            if (stream.Read(buffer, 0, contentLength) < 0)
                throw new Exception("");
            result.Append(Encoding.ASCII.GetString(buffer, 0, contentLength));
            return result.ToString();
        }
    }
}
