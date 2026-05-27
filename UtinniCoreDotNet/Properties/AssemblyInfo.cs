using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("UtinniCoreDotNet")]
[assembly: AssemblyDescription("UtinniCore .NET")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Philip Klatt")]
[assembly: AssemblyProduct("UtinniCoreDotNet")]
[assembly: AssemblyCopyright("Copyright ©  2020, Philip Klatt")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("39ab8a43-b916-4c6e-87dd-928b438cae68")]

// Phase 2 (02-01 / Task 1): give the test assembly access to `internal` members
// so the C-04 regression test in GroundSceneCallbacksTests can invoke the
// shared `Drain(ConcurrentQueue<Action>)` helper without reflection.
[assembly: InternalsVisibleTo("UtinniCoreDotNet.Tests")]

// Phase 7 (07-01): expose TreFile.PayloadReadCount (internal test seam, NOT public
// shipping surface) to the CLI test assembly so it can prove lazy TOC-only enumeration
// performs zero payload reads.
[assembly: InternalsVisibleTo("Utinni.Cli.Tests")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
