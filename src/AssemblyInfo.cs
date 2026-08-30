using System.Reflection;
using System.Runtime.InteropServices;

// Identity of the built executable.
//
// This is not decoration. A Windows binary with no company, no product name and a
// version of 0.0.0.0 is, to a heuristic scanner, indistinguishable from something
// compiled five minutes ago with nothing to say for itself — and this one also
// installs services and loads a packet driver, which is the rest of that profile.
// EasyDPI was reported blocked as "a virus or potentially unwanted software" on a
// machine where none of the shipped binaries match any signature, which is what a
// reputation verdict looks like from the outside. Filling these in does not make the
// file trusted, but it removes one of the reasons to distrust it, and it costs
// nothing. Signing the binary is the actual answer; this is what can be done without
// a certificate.

[assembly: AssemblyTitle("EasyDPI")]
[assembly: AssemblyDescription("Measures a connection and configures DPI bypass and encrypted DNS to match it.")]
[assembly: AssemblyCompany("ozkanbatmaz")]
[assembly: AssemblyProduct("EasyDPI")]
[assembly: AssemblyCopyright("MIT licensed. https://github.com/ozkanbatmaz/EasyDPI")]

// The single source of the version number: AppInfo reads it back from here, so the
// number shown in a diagnostic report and the number in the file's properties cannot
// drift apart.
[assembly: AssemblyVersion("1.3.2.0")]
[assembly: AssemblyFileVersion("1.3.2.0")]

[assembly: ComVisible(false)]
