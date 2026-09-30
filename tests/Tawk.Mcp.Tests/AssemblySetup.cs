// Every test gets a fresh fixture, so fields never carry state from one test to the next.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[assembly: Parallelizable(ParallelScope.Fixtures)]
