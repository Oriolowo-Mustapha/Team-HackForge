// Database-backed fixtures share an isolated database and may apply migrations. Serialize the
// fixtures; concurrency behavior remains exercised explicitly with competing tasks in each test.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
