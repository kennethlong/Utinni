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

namespace Utinni.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Use new Parser(...) rather than Parser.Default so that HelpWriter
            // resolves Console.Error at call time (after InProcessCliRunner.SetError).
            // Parser.Default is a singleton that caches Console.Error at first access,
            // which breaks in-process stdout/stderr capture in tests.
            using (var parser = new Parser(settings =>
            {
                settings.HelpWriter = System.Console.Error;
                settings.CaseSensitive = false;
            }))
            {
                return parser.ParseArguments<
                        Commands.ParseTreOptions,
                        Commands.ListObjectsOptions,
                        Commands.InspectIffOptions,
                        Commands.DecodeIffOptions,
                        Commands.RoundtripIffOptions,
                        Commands.RoundtripTabOptions,
                        Commands.RoundtripStfOptions,
                        Commands.RoundtripOtOptions,
                        Commands.ValidatePluginOptions>(args)
                    .MapResult(
                        (Commands.ParseTreOptions o)       => Commands.ParseTreCommand.Run(o),
                        (Commands.ListObjectsOptions o)    => Commands.ListObjectsCommand.Run(o),
                        (Commands.InspectIffOptions o)     => Commands.InspectIffCommand.Run(o),
                        (Commands.DecodeIffOptions o)      => Commands.DecodeIffCommand.Run(o),
                        (Commands.RoundtripIffOptions o)   => Commands.RoundtripIffCommand.Run(o),
                        (Commands.RoundtripTabOptions o)   => Commands.RoundtripTabCommand.Run(o),
                        (Commands.RoundtripStfOptions o)   => Commands.RoundtripStfCommand.Run(o),
                        (Commands.RoundtripOtOptions o)    => Commands.RoundtripOtCommand.Run(o),
                        (Commands.ValidatePluginOptions o) => Commands.ValidatePluginCommand.Run(o),
                        errs => 1);  // exit 1 on usage error per D-02
            }
        }
    }
}
