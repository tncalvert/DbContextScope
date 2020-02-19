using System;
using System.Linq;
using EntityFrameworkCore.DbContextScope.Test.BusinessLogicServices;
using EntityFrameworkCore.DbContextScope.Test.CommandModel;
using EntityFrameworkCore.DbContextScope.Test.DatabaseContext;
using EntityFrameworkCore.DbContextScope.Test.DomainModel;
using EntityFrameworkCore.DbContextScope.Test.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.DbContextScope.Test
{
    public static class UserSpecExtensions
    {
        public static void Equal(this UserCreationSpec spec, User user)
        {
            Assert.NotNull(spec);
            Assert.NotNull(user);
            Assert.Equal(spec.Id, user.Id);
            Assert.Equal(spec.Email, user.Email);
            Assert.Equal(spec.Name, user.Name);
        }
    }

    public class DbContextScopeTest
    {
        private readonly ITestOutputHelper _Output;

        public DbContextScopeTest(ITestOutputHelper output)
        {
            _Output = output;
        }

        private class DbContextFactory : IDbContextFactory
        {
            public TDbContext CreateDbContext<TDbContext>() where TDbContext : DbContext
            {
                if (typeof(TDbContext) == typeof(UserManagementDbContext))
                {
                    var config = new DbContextOptionsBuilder<UserManagementDbContext>()
                        .UseInMemoryDatabase("1337")
                        .ConfigureWarnings(warnings => { warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning); });
                    return new UserManagementDbContext(config.Options) as TDbContext;
                }

                throw new NotImplementedException(typeof(TDbContext).Name);
            }
        }

        [Fact]
        public void FullTest()
        {
            //-- Poor-man DI - build our dependencies by hand for this demo
            var dbContextScopeFactory = new DbContextScopeFactory(new DbContextFactory());
            var ambientDbContextLocator = new AmbientDbContextLocator();
            var userRepository = new UserRepository(ambientDbContextLocator);

            var userCreationService = new UserCreationService(dbContextScopeFactory, userRepository);
            var userQueryService = new UserQueryService(dbContextScopeFactory, userRepository);
            var userEmailService = new UserEmailService(dbContextScopeFactory);
            var userCreditScoreService = new UserCreditScoreService(dbContextScopeFactory);

            _Output.WriteLine(
                "This demo uses an EF Core In Memory database. It does not create any external databases.");
            _Output.WriteLine("");

            //-- Demo of typical usage for read and writes
            _Output.WriteLine("Creating a user called Mary...");
            var marysSpec = new UserCreationSpec("Mary", "mary@example.com");
            userCreationService.CreateUser(marysSpec);
            _Output.WriteLine("Done.\n");

            _Output.WriteLine("Trying to retrieve our newly created user from the data store...");
            var mary = userQueryService.GetUser(marysSpec.Id);
            _Output.WriteLine("OK. Persisted user: {0}", mary);
            marysSpec.Equal(mary);

            //-- Demo of nested DbContextScopes
            _Output.WriteLine("Creating 2 new users called John and Jeanne in an atomic transaction...");
            var johnSpec = new UserCreationSpec("John", "john@example.com");
            var jeanneSpec = new UserCreationSpec("Jeanne", "jeanne@example.com");
            userCreationService.CreateListOfUsers(johnSpec, jeanneSpec);
            _Output.WriteLine("Done.\n");

            _Output.WriteLine("Trying to retrieve our newly created users from the data store...");
            var createdUsers = userQueryService.GetUsers(johnSpec.Id, jeanneSpec.Id).ToList();
            _Output.WriteLine("OK. Found {0} persisted users.", createdUsers.Count);

            Assert.Equal(2, createdUsers.Count);
            johnSpec.Equal(createdUsers[0]);
            jeanneSpec.Equal(createdUsers[1]);

            //-- Demo of nested DbContextScopes in the face of an exception. 
            // If any of the provided users failed to get persisted, none should get persisted. 
            _Output.WriteLine(
                "Creating 2 new users called Julie and Marc in an atomic transaction. Will make the persistence of the second user fail intentionally in order to test the atomicity of the transaction...");
            var julieSpec = new UserCreationSpec("Julie", "julie@example.com");
            var marcSpec = new UserCreationSpec("Marc", "marc@example.com");

            Assert.ThrowsAny<Exception>(() =>
            {
                userCreationService.CreateListOfUsersWithIntentionalFailure(julieSpec, marcSpec);
            });

            _Output.WriteLine("Trying to retrieve our newly created users from the data store...");
            var maybeCreatedUsers = userQueryService.GetUsers(julieSpec.Id, marcSpec.Id).ToList();
            _Output.WriteLine(
                "Found {0} persisted users. If this number is 0, we're all good. If this number is not 0, we have a big problem.",
                maybeCreatedUsers.Count);
            Assert.Equal(0, maybeCreatedUsers.Count);

            //-- Demo of DbContextScope within an async flow
            _Output.WriteLine("Trying to retrieve two users John and Jeanne sequentially in an asynchronous manner...");
            // We're going to block on the async task here as we don't have a choice. No risk of deadlocking in any case as console apps
            // don't have a synchronization context.
            var usersFoundAsync = userQueryService.GetTwoUsersAsync(johnSpec.Id, jeanneSpec.Id).Result;
            _Output.WriteLine("OK. Found {0} persisted users.", usersFoundAsync.Count);
            Assert.Equal(2, usersFoundAsync.Count);
            johnSpec.Equal(usersFoundAsync[0]);
            jeanneSpec.Equal(usersFoundAsync[1]);

            //-- Demo of explicit database transaction. 
            _Output.WriteLine("Trying to retrieve user John within a READ UNCOMMITTED database transaction...");
            // You'll want to use SQL Profiler or Entity Framework Profiler to verify that the correct transaction isolation
            // level is being used.
            var userMaybeUncommitted = userQueryService.GetUserUncommitted(johnSpec.Id);
            _Output.WriteLine("OK. User found: {0}", userMaybeUncommitted);
            johnSpec.Equal(userMaybeUncommitted);

            //-- Demo of disabling the DbContextScope nesting behaviour in order to force the persistence of changes made to entities
            // This is a pretty advanced feature that you can safely ignore until you actually need it.
            _Output.WriteLine("Will simulate sending a Welcome email to John...");

            using (var parentScope = dbContextScopeFactory.Create())
            {
                var parentDbContext = parentScope.DbContexts.Get<UserManagementDbContext>();

                // Load John in the parent DbContext
                var john = parentDbContext.Users.Find(johnSpec.Id);
                _Output.WriteLine("Before calling SendWelcomeEmail(), john.WelcomeEmailSent = " +
                                  john.WelcomeEmailSent);

                // Now call our SendWelcomeEmail() business logic service method, which will
                // update John in a non-nested child context
                userEmailService.SendWelcomeEmail(johnSpec.Id);

                // Verify that we can see the modifications made to John by the SendWelcomeEmail() method
                _Output.WriteLine("After calling SendWelcomeEmail(), john.WelcomeEmailSent = " +
                                  john.WelcomeEmailSent);

                // Note that even though we're not calling SaveChanges() in the parent scope here, the changes
                // made to John by SendWelcomeEmail() will remain persisted in the database as SendWelcomeEmail()
                // forced the creation of a new DbContextScope.
            }

            //-- Demonstration of DbContextScope and parallel programming
            _Output.WriteLine(
                "Calculating and storing the credit score of all users in the database in parallel...");
            userCreditScoreService.UpdateCreditScoreForAllUsers();
            _Output.WriteLine("Done.");
        }
    }
}