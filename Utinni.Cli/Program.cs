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
                // NOTE: ParseArguments<T..> tops out at 16 type args; with 17 verbs we use the
                // Type[] overload (no arity cap). MapResult still dispatches by concrete option type.
                // Both ParseArguments<T..> and MapResult(lambdas) top out at 16 verbs. With 17 verbs
                // we use the Type[] ParseArguments overload + a single object-typed MapResult that
                // dispatches on the concrete parsed option type (see Dispatch).
                return parser.ParseArguments(args,
                        typeof(Commands.ParseTreOptions),
                        typeof(Commands.ListObjectsOptions),
                        typeof(Commands.InspectIffOptions),
                        typeof(Commands.DecodeIffOptions),
                        typeof(Commands.RoundtripIffOptions),
                        typeof(Commands.RoundtripTabOptions),
                        typeof(Commands.RoundtripStfOptions),
                        typeof(Commands.RoundtripOtOptions),
                        typeof(Commands.ValidatePluginOptions),
                        typeof(Commands.SaveOptions),
                        typeof(Commands.RepackTreOptions),
                        typeof(Commands.CompileTemplateOptions),
                        typeof(Commands.BuildTreOptions),
                        typeof(Commands.CompileDefinitionOptions),
                        typeof(Commands.CompileDatatableOptions),
                        typeof(Commands.ExportArmorOptions),
                        typeof(Commands.ExportWeaponOptions),
                        typeof(Commands.ApplySaveTabOptions),
                        typeof(Commands.ApplySaveOtOptions),
                        typeof(Commands.ApplySaveIffOptions),
                        typeof(Commands.ApplySaveStfOptions),
                        typeof(Commands.RoundtripParticleOptions),
                        typeof(Commands.DecodeTrnOptions),
                        typeof(Commands.RoundtripTrnOptions),
                        typeof(Commands.ApplySaveTrnOptions),
                        typeof(Commands.ValidateBundleOptions),
                        typeof(Commands.DecodeEffectOptions),
                        typeof(Commands.RoundtripEffectOptions),
                        typeof(Commands.ApplySaveEffectOptions),
                        typeof(Commands.DecodeWithTemplateOptions),
                        typeof(Commands.RoundtripTemplateOptions),
                        typeof(Commands.ListTemplatesOptions))
                    .MapResult(
                        (object opts) => Dispatch(opts),
                        errs => 1);  // exit 1 on usage error per D-02
            }
        }

        private static int Dispatch(object opts)
        {
            switch (opts)
            {
                case Commands.ParseTreOptions o:          return Commands.ParseTreCommand.Run(o);
                case Commands.ListObjectsOptions o:       return Commands.ListObjectsCommand.Run(o);
                case Commands.InspectIffOptions o:        return Commands.InspectIffCommand.Run(o);
                case Commands.DecodeIffOptions o:         return Commands.DecodeIffCommand.Run(o);
                case Commands.RoundtripIffOptions o:      return Commands.RoundtripIffCommand.Run(o);
                case Commands.RoundtripTabOptions o:      return Commands.RoundtripTabCommand.Run(o);
                case Commands.RoundtripStfOptions o:      return Commands.RoundtripStfCommand.Run(o);
                case Commands.RoundtripOtOptions o:       return Commands.RoundtripOtCommand.Run(o);
                case Commands.ValidatePluginOptions o:    return Commands.ValidatePluginCommand.Run(o);
                case Commands.SaveOptions o:              return Commands.SaveCommand.Run(o);
                case Commands.RepackTreOptions o:         return Commands.RepackTreCommand.Run(o);
                case Commands.CompileTemplateOptions o:   return Commands.CompileTemplateCommand.Run(o);
                case Commands.BuildTreOptions o:          return Commands.BuildTreCommand.Run(o);
                case Commands.CompileDefinitionOptions o: return Commands.CompileDefinitionCommand.Run(o);
                case Commands.CompileDatatableOptions o:  return Commands.CompileDatatableCommand.Run(o);
                case Commands.ExportArmorOptions o:       return Commands.ExportArmorCommand.Run(o);
                case Commands.ExportWeaponOptions o:      return Commands.ExportWeaponCommand.Run(o);
                case Commands.ApplySaveTabOptions o:      return Commands.ApplySaveTabCommand.Run(o);
                case Commands.ApplySaveOtOptions o:       return Commands.ApplySaveOtCommand.Run(o);
                case Commands.ApplySaveIffOptions o:      return Commands.ApplySaveIffCommand.Run(o);
                case Commands.ApplySaveStfOptions o:      return Commands.ApplySaveStfCommand.Run(o);
                case Commands.RoundtripParticleOptions o: return Commands.RoundtripParticleCommand.Run(o);
                case Commands.DecodeTrnOptions o:          return Commands.DecodeTrnCommand.Run(o);
                case Commands.RoundtripTrnOptions o:       return Commands.RoundtripTrnCommand.Run(o);
                case Commands.ApplySaveTrnOptions o:       return Commands.ApplySaveTrnCommand.Run(o);
                case Commands.ValidateBundleOptions o:     return Commands.ValidateBundleCommand.Run(o);
                case Commands.DecodeEffectOptions o:       return Commands.DecodeEffectCommand.Run(o);
                case Commands.RoundtripEffectOptions o:    return Commands.RoundtripEffectCommand.Run(o);
                case Commands.ApplySaveEffectOptions o:    return Commands.ApplySaveEffectCommand.Run(o);
                case Commands.DecodeWithTemplateOptions o: return Commands.DecodeWithTemplateCommand.Run(o);
                case Commands.RoundtripTemplateOptions o:  return Commands.RoundtripTemplateCommand.Run(o);
                case Commands.ListTemplatesOptions o:      return Commands.ListTemplatesCommand.Run(o);
                default:                                  return 1;
            }
        }
    }
}
