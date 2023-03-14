using System;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Meadow.CLI.Test
{
    [TestFixture]
    public class IdentityManagerTest
    {
        IdentityManager _identityManager;

        public IdentityManagerTest(IdentityManager identityManager)
        {
            _identityManager = identityManager;
        }

        [Test]
        public void CredentialStoreTest()
        {
            string name = $"cli-test-{Guid.NewGuid():N}";
            string username = Guid.NewGuid().ToString("N");
            string password = Guid.NewGuid().ToString("N");

            var saveResult = _identityManager.SaveCredential(name, username, password);

            Assert.IsTrue(saveResult);

            var credentialResult = _identityManager.GetCredentials(name);

            Assert.AreEqual(username, credentialResult.username);
            Assert.AreEqual(password, credentialResult.password);

            _identityManager.DeleteCredential(name);
        }
    }
}
