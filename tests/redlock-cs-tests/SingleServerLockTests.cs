using System;
using System.Collections.Generic;
using NUnit.Framework;
using StackExchange.Redis;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Redlock.CSharp.Tests
{
    [TestFixture]
    public class SingleServerLockTests
    {
        private const string ResourceName = "MyResourceName";
        private readonly List<Process> _redisProcessList = new List<Process>();

        [SetUp]
        public void Setup()
        {
            _redisProcessList.Add(TestHelper.StartRedisServer(6379));
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var process in _redisProcessList)
            {
                if (!process.HasExited) process.Kill();
            }

            _redisProcessList.Clear();
        }

        [Test]
        public async Task TestWhenLockedAnotherLockRequestIsRejected()
        {
            var redlock = new Redlock(Connect());

            var (success, @lock) = await redlock.LockAsync(ResourceName, new TimeSpan(0, 0, 10));
            Assert.IsTrue(success, "Unable to get lock");
            var locked2 = await redlock.LockAsync(ResourceName, new TimeSpan(0, 0, 10));
            Assert.IsFalse(locked2.success, "lock taken, it shouldn't be possible");
            await redlock.UnlockAsync(@lock);
        }

        [Test]
        public async Task TestThatSequenceLockedUnlockedAndLockedAgainIsSuccessful()
        {
            var redlock = new Redlock(Connect());

            var (success, @lock) = await redlock.LockAsync(ResourceName, new TimeSpan(0, 0, 10));
            Assert.IsTrue(success, "Unable to get lock");
            await redlock.UnlockAsync(@lock);
            var (success2, lock2) = await redlock.LockAsync(ResourceName, new TimeSpan(0, 0, 10));
            Assert.IsTrue(success2, "Unable to get lock");
            await redlock.UnlockAsync(lock2);
        }

        private static IDatabaseAsync Connect() => ConnectionMultiplexer.Connect("127.0.0.1:6379").GetDatabase();

    }
}
