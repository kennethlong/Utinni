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
    [Verb("validate-plugin", HelpText = "Reflect on a plugin directory and report compliance. WARNING: loads each .dll under the given directory; only run against trusted plugin directories.")]
    public class ValidatePluginOptions
    {
        [Value(0, MetaName = "dir", Required = true, HelpText = "Plugin directory.")]
        public string Dir { get; set; }
    }

    public static class ValidatePluginCommand
    {
        public static int Run(ValidatePluginOptions o)
        {
            return JsonOutput.EmitError("validate-plugin", "NotImplemented", "validate-plugin command lands in Plan 04-04.", exitCode: 1);
        }
    }
}
