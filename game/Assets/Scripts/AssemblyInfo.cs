using System.Runtime.CompilerServices;

// Expose internals (e.g. ResetInstanceIdForTesting) to the test assembly.
[assembly: InternalsVisibleTo("AshenThrone.Tests")]
