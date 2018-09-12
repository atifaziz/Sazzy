namespace Sazzy.Sample
{
    using System;
    using System.IO;

    static class Program
    {
        static void Main(string[] args)
        {
            using (var input = HttpContentStream.Open(File.OpenRead(args[0])))
            {
                foreach (var (name, value) in input.Headers)
                    Console.Error.WriteLine(name + ": " + value);

                using (var output = Console.OpenStandardOutput())
                    input.CopyTo(output);
            }
        }
    }
}
