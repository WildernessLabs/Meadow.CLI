using System;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Meadow.CLI.Test
{
    [TestFixture]
    public class IdentityManagerTest
    {
        [Test]
        public void CredentialStoreTest()
        {
            string name = $"cli-test-{Guid.NewGuid():N}";
            string username = Guid.NewGuid().ToString("N");
            string password = Guid.NewGuid().ToString("N");

            IdentityManager identityManager = new IdentityManager(NullLogger.Instance);
            var saveResult = identityManager.SaveCredential(name, username, password);

            Assert.IsTrue(saveResult);

            var credentialResult = identityManager.GetCredentials(name);

            Assert.AreEqual(username, credentialResult.username);
            Assert.AreEqual(password, credentialResult.password);

            identityManager.DeleteCredential(name);
        }
    }
}
