using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StackExchange.Redis;
using System.Diagnostics;

namespace Redlock.CSharp.Tests
{
    [TestFixture]
    public class MultiServerLockTests
    {
        private const string ResourceName = "MyResourceName";
        private static readonly IEnumerable<long> _ports = new long[] {6379, 6380, 6381};
        private readonly List<Process> _redisProcessList = new List<Process>();

        [SetUp]
        public void Setup()
        {
            foreach (var port in _ports)
            {
                _redisProcessList.Add(TestHelper.StartRedisServer(port));
            }
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var process in _redisProcessList.Where(process => !process.HasExited))
            {
                process.Kill();
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

        private static IEnumerable<IDatabaseAsync> Connect()
            => _ports.Select(port => ConnectionMultiplexer.Connect($"127.0.0.1:{port}").GetDatabase());

    }
}
