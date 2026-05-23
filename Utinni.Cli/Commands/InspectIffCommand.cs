/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/

using CommandLine;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("inspect-iff", HelpText = "Emit the chunk tree of an IFF file as JSON.")]
    public class InspectIffOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file.")]
        public string Path { get; set; }
    }

    public static class InspectIffCommand
    {
        public static int Run(InspectIffOptions o)
        {
            return JsonOutput.EmitError("inspect-iff", "NotImplemented", "inspect-iff command lands in Plan 04-03.", exitCode: 1);
        }
    }
}
