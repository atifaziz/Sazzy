namespace Sazzy.Sample
{
    using System;
    using System.IO;

    static class Program
    {
        static void Main(string[] args)
        {
            using (var input = File.OpenRead(args[0]))
            using (var message = new HttpMessage(input))
            {
                Console.Error.WriteLine(message.StartLine);

                foreach (var (name, value) in message.Headers)
                    Console.Error.WriteLine(name + ": " + value);

                using (var output = Console.OpenStandardOutput())
                    message.ContentStream.CopyTo(output);
            }
        }
    }
}
