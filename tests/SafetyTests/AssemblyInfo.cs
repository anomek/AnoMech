using Xunit;

// The zone session keeps its start gate in statics and the harness is a process-wide singleton.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
