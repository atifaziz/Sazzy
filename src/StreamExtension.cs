namespace Sazzy
{
    using System;
    using System.IO;
    using System.Text;

    static class StreamExtension
    {
        public static string ReadLine(this Stream stream)
        {
            if (!stream.CanRead || stream.Position == stream.Length)
                return null;

            var result = new StringBuilder();
            int b;
            char character;
            while ((b = stream.ReadByte()) >= 0 && ((character = (char) b) != '\n'))
            {
                if (character != '\r' && character != '\n')
                    result.Append(character);
            }
            return result.ToString();
        }
    }
}
