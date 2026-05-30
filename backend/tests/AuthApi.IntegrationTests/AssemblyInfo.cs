// Tests share a single PostgreSQL database, so they must not run concurrently.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
