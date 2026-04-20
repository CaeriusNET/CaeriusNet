// netstandard2.0 polyfill so that 'init' setters and record types compile in this project.
// Roslyn analyzers/source generators must target netstandard2.0; this internal type is recognised
// by the C# compiler when present.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;