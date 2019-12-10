#region Copyright 2018 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Sazzy.Sample
{
    using System;
    using System.IO;

    static class Program
    {
        static void Wain(string[] args)
        {
            var arg = args.Length > 0
                    ? args[0]
                    : throw new Exception("Missing file specification.");

            using var input = File.OpenRead(arg);
            using var message = new HttpMessage(input);

            Console.Error.WriteLine(message.StartLine);

            foreach (var (name, value) in message.Headers)
                Console.Error.WriteLine(name + ": " + value);

            using var output = Console.OpenStandardOutput();
            message.ContentStream.CopyTo(output);
        }

        static int Main(string[] args)
        {
            try
            {
                Wain(args);
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 0xbad;
            }
        }
    }
}
