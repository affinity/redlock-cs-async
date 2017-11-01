using System;
using System.Collections.Generic;
using NUnit.Framework;
using StackExchange.Redis;
using System.Diagnostics;

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
        public void TestWhenLockedAnotherLockRequestIsRejected()
        {
            var redlock = new Redlock(Connect());

            var locked = redlock.Lock(ResourceName, new TimeSpan(0, 0, 10), out var lockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            locked = redlock.Lock(ResourceName, new TimeSpan(0, 0, 10), out _);
            Assert.IsFalse(locked, "lock taken, it shouldn't be possible");
            redlock.Unlock(lockObject);
        }

        [Test]
        public void TestThatSequenceLockedUnlockedAndLockedAgainIsSuccessfull()
        {
            var redlock = new Redlock(Connect());

            var locked = redlock.Lock(ResourceName, new TimeSpan(0, 0, 10), out var lockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            redlock.Unlock(lockObject);
            locked = redlock.Lock(ResourceName, new TimeSpan(0, 0, 10), out var newLockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            redlock.Unlock(newLockObject);
        }

        private static IDatabaseAsync Connect() => ConnectionMultiplexer.Connect("127.0.0.1:6379").GetDatabase();

    }
}
