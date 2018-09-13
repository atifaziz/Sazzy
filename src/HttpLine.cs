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

namespace Sazzy
{
    using System;
    using System.IO;
    using System.Text;

    static class HttpLine
    {
        public static string Read(Stream stream, StringBuilder lineBuilder)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (lineBuilder == null) throw new ArgumentNullException(nameof(lineBuilder));

            lineBuilder.Length = 0;

            int b; char ch;

            while ((b = stream.ReadByte()) >= 0 && (ch = (char) b) != '\n')
            {
                if (ch != '\r' && ch != '\n')
                    lineBuilder.Append(ch);
            }

            return lineBuilder.ToString();
        }
    }
}
