// Integration tests share global static state (ActivitySource, file system cache, GPU memory).
// Disable xUnit parallel execution for this assembly so test classes do not interfere with
// each other — e.g. ActivityCapture in GenerationLifecycleIntegrationTests must not pick up
// activities emitted by ModelLifecycleTests running concurrently on the same ActivitySource.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
