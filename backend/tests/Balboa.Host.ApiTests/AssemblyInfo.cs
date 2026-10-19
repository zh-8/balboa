using Balboa.TestKit;
using Xunit;

// One Postgres container for the whole assembly; tests run sequentially and reset data with Respawn.
[assembly: AssemblyFixture(typeof(PostgresFixture))]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
