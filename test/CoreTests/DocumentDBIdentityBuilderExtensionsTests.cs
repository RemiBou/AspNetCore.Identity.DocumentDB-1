﻿using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace CoreTests
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.DocumentDB;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class DocumentDBIdentityBuilderExtensionsTests
    {
        static readonly string databaseId = "AspDotNetCore.Identity.DocumentDB.Test";
        static readonly string collectionId = "Users.Collection.Test";
        static readonly string key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

        DocumentClient client = new DocumentClient(new Uri("https://localhost:8081"), key);

        [Fact]
        [Trait("Category", "BreaksUnix")]
        public void AddDocumentDBStores_WithDefaultTypesViaOptions_ResolvesStoresAndManagers()
        {
            var services = new ServiceCollection();
            services.AddIdentityWithDocumentDBStores(options =>
            {
                options.EnableDocumentDbEmulatorSupport();
                options.DatabaseId = databaseId;
                options.CollectionId = collectionId;
            });
            // note: UserManager and RoleManager use logging
            services.AddLogging();

            var provider = services.BuildServiceProvider();
            var resolvedUserStore = provider.GetService<IUserStore<IdentityUser>>();
            Assert.NotNull(resolvedUserStore); // "User store did not resolve"

            var resolvedRoleStore = provider.GetService<IRoleStore<IdentityRole>>();
            Assert.NotNull(resolvedRoleStore); // "Role store did not resolve"

            var resolvedUserManager = provider.GetService<UserManager<IdentityUser>>();
            Assert.NotNull(resolvedUserManager); // "User manager did not resolve"

            var resolvedRoleManager = provider.GetService<RoleManager<IdentityRole>>();
            Assert.NotNull(resolvedRoleManager); // "Role manager did not resolve"
        }

        [Fact]
        [Trait("Category", "BreaksUnix")]
        public void AddDocumentDBStores_WithIdentityOptionsSetsThoseOptions()
        {
            var services = new ServiceCollection();
            services.AddIdentityWithDocumentDBStores(options =>
            {
                options.EnableDocumentDbEmulatorSupport();
                options.DatabaseId = databaseId;
                options.CollectionId = collectionId;
            }, identOptions =>
            {
                identOptions.ClaimsIdentity.UserNameClaimType = ClaimTypes.WindowsUserClaim;
            });
            // note: UserManager and RoleManager use logging
            services.AddLogging();
            services.AddOptions();

            var provider = services.BuildServiceProvider();
            var resolvedIdentOptions = provider.GetService<IOptions<IdentityOptions>>();

            Assert.NotNull(resolvedIdentOptions); // "Identity options did not resolve"
            Assert.Equal(ClaimTypes.WindowsUserClaim, resolvedIdentOptions.Value.ClaimsIdentity.UserNameClaimType); // "Identity options are not set"
        }

        protected class CustomUser : IdentityUser
        {
        }

        protected class CustomRole : IdentityRole
        {
        }

        [Fact]
        [Trait("Category", "BreaksUnix")]
        public void AddDocumentDBStores_WithCustomTypesViaOptions_ThisShouldLookReasonableForUsers()
        {
            // this test is just to make sure I consider the interface for using custom types
            // so that it's not a horrible experience even though it should be rarely used
            var services = new ServiceCollection();

            services.AddIdentityWithDocumentDBStores<CustomUser, CustomRole>(options =>
            {
                options.EnableDocumentDbEmulatorSupport();
                options.DatabaseId = databaseId;
                options.CollectionId = collectionId;
            });
            services.AddLogging();

            var provider = services.BuildServiceProvider();
            var resolvedUserStore = provider.GetService<IUserStore<CustomUser>>();
            Assert.NotNull(resolvedUserStore); // "User store did not resolve"

            var resolvedRoleStore = provider.GetService<IRoleStore<CustomRole>>();
            Assert.NotNull(resolvedRoleStore); // "Role store did not resolve"

            var resolvedUserManager = provider.GetService<UserManager<CustomUser>>();
            Assert.NotNull(resolvedUserManager); // "User manager did not resolve"

            var resolvedRoleManager = provider.GetService<RoleManager<CustomRole>>();
            Assert.NotNull(resolvedRoleManager); // "Role manager did not resolve"
        }

        [Fact]
        public void AddDocumentDBStores_ConnectionStringWithoutDatabase_Throws()
        {
            DocumentClient client = null;

            string collectionId = "fake";

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                new ServiceCollection()
                .AddIdentity<IdentityUser, IdentityRole>()
                .RegisterDocumentDBStores<IdentityUser, IdentityRole>(options =>
                {
                    options.EnableDocumentDbEmulatorSupport();
                    options.CollectionId = collectionId;
                });
            });
            Assert.Equal("DatabaseId cannot be null.", ex.Message);
        }

        protected class WrongUser : IdentityUser
        {
        }

        protected class WrongRole : IdentityRole
        {
        }

        [Fact]
        [Trait("Category", "BreaksUnix")]
        public void AddDocumentDBStores_MismatchedTypes_ThrowsWarningToHelpUsers()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                new ServiceCollection()
                    .AddIdentity<IdentityUser, IdentityRole>()
                    .RegisterDocumentDBStores<WrongUser, IdentityRole>(options =>
                    {
                        options.EnableDocumentDbEmulatorSupport();
                        options.DatabaseId = databaseId;
                        options.CollectionId = collectionId;
                    });
            });
            Assert.Equal("User type passed to RegisterDocumentDBStores must match user type passed to AddIdentity. You passed Microsoft.AspNetCore.Identity.DocumentDB.IdentityUser to AddIdentity and CoreTests.DocumentDBIdentityBuilderExtensionsTests+WrongUser to RegisterDocumentDBStores, these do not match.", ex.Message);

            var ex2 = Assert.Throws<ArgumentException>(() =>
            {
                new ServiceCollection()
                    .AddIdentity<IdentityUser, IdentityRole>()
                    .RegisterDocumentDBStores<IdentityUser, WrongRole>(options =>
                    {
                        options.EnableDocumentDbEmulatorSupport();
                        options.DatabaseId = databaseId;
                        options.CollectionId = collectionId;
                    });
            });
            Assert.Equal("Role type passed to RegisterDocumentDBStores must match role type passed to AddIdentity. You passed Microsoft.AspNetCore.Identity.DocumentDB.IdentityRole to AddIdentity and CoreTests.DocumentDBIdentityBuilderExtensionsTests+WrongRole to RegisterDocumentDBStores, these do not match.", ex2.Message);
        }

        [Fact]
        [Trait("Category", "BreaksUnix")]
        public async Task AddDocumentDBStores_NewAndExistingCollections()
        {
            var collection = client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(databaseId)).Where(c => c.Id.Equals(collectionId)).AsEnumerable().FirstOrDefault();

            if (collection != null) { await client.DeleteDocumentCollectionAsync(collection.SelfLink); }

            // when collection does not exist does not throw:
            new ServiceCollection()
                .AddIdentity<IdentityUser, IdentityRole>()
                .RegisterDocumentDBStores<IdentityUser, IdentityRole>(options =>
                {
                    options.EnableDocumentDbEmulatorSupport();
                    options.DatabaseId = databaseId;
                    options.CollectionId = collectionId;
                });

            // when collection exists does not throw:
            new ServiceCollection()
                .AddIdentity<IdentityUser, IdentityRole>()
                .RegisterDocumentDBStores<IdentityUser, IdentityRole>(options =>
                {
                    options.EnableDocumentDbEmulatorSupport();
                    options.DatabaseId = databaseId;
                    options.CollectionId = collectionId;
                });
        }
    }
}