#region LICENSE
/*
 *   Copyright 2014 Angelo Simone Scotto <scotto.a@gmail.com>
 * 
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 * 
 *       http://www.apache.org/licenses/LICENSE-2.0
 * 
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 * 
 * */
#endregion

using System.Threading.Tasks;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Redlock.CSharp
{
    public class Redlock
    {

        /// <summary>
        /// String containing the Lua unlock script.
        /// </summary>
        private const string UnlockScript = @"
            if redis.call(""get"",KEYS[1]) == ARGV[1] then
                return redis.call(""del"",KEYS[1])
            else
                return 0
            end";

        private const int DefaultRetryCount = 3;
        private const double ClockDriveFactor = 0.01;
        private readonly TimeSpan _defaultRetryDelay = TimeSpan.FromMilliseconds(200);
        private readonly IList<IDatabaseAsync> _databases;
        private int Quorum => _databases.Count / 2 + 1;

        [Obsolete("Use constructors (IDatabaseAsync[]) or (IEnumerable<IDatabaseAsync>)")]
        public Redlock(params IConnectionMultiplexer[] connections)
            : this(connections.Select(_ => _.GetDatabase()))
        {
        }

        public Redlock(params IDatabaseAsync[] databases)
            : this((IEnumerable<IDatabaseAsync>)databases)
        {
        }

        public Redlock(IEnumerable<IDatabaseAsync> databases)
        {
            _databases = databases.ToList().AsReadOnly();
        }

        public async Task<(bool success, Lock @lock)> LockAsync(RedisKey resource, TimeSpan ttl)
        {
            var val = CreateUniqueLockId();
            Lock lockObject = null;
            var successful = await Retry(DefaultRetryCount, _defaultRetryDelay, async () =>
            {
                try
                {
                    var n = 0;
                    var startTime = DateTime.Now;

                    // Use keys
                    await ForEachRedisRegistered(async database =>
                    {
                        if (await LockInstance(database, resource, val, ttl))
                        {
                            n += 1;
                        }
                    });

                    /*
                     * Add 2 milliseconds to the drift to account for Redis expires
                     * precision, which is 1 millisecond, plus 1 millisecond min drift 
                     * for small TTLs.        
                     */
                    var drift = Convert.ToInt32(ttl.TotalMilliseconds * ClockDriveFactor + 2);
                    var validityTime = ttl - (DateTime.Now - startTime) - new TimeSpan(0, 0, 0, 0, drift);

                    if (n >= Quorum && validityTime.TotalMilliseconds > 0)
                    {
                        lockObject = new Lock(resource, val, validityTime);
                        return true;
                    }
                    await ForEachRedisRegistered(async database => await UnlockInstance(database, resource, val));
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            });

            if (!successful)
            {
                await ForEachRedisRegistered(async database => await UnlockInstance(database, resource, val));
            }

            return (successful, lockObject);
        }

        public Task UnlockAsync(Lock lockObject)
        {
            return ForEachRedisRegistered(database => UnlockInstance(database, lockObject.Resource, lockObject.Value));
        }

        /// <summary>
        /// Override in order to intercept all requests to Redis.  Useful for tracking and auditing purposes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asyncAction"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        protected virtual Task<T> ExecuteAsync<T>(Func<Task<T>> asyncAction, string name)
        {
            return asyncAction();
        }

        private async Task<bool> LockInstance(IDatabaseAsync database, string resource, byte[] val, TimeSpan ttl)
        {
            try
            {
                return await ExecuteAsync(() => database.StringSetAsync(resource, val, ttl, When.NotExists), "Lock");
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task UnlockInstance(IDatabaseAsync database, string resource, byte[] val)
        {
            RedisKey[] key = { resource };
            RedisValue[] values = { val };
            await ExecuteAsync(() => database.ScriptEvaluateAsync(
                UnlockScript,
                key,
                values
            ), "Unlock");
        }

        private async Task ForEachRedisRegistered(Func<IDatabaseAsync, Task> action)
        {
            foreach (var database in _databases)
            {
                await action(database);
            }
        }

        private static async Task<bool> Retry(int retryCount, TimeSpan retryDelay, Func<Task<bool>> action)
        {
            var maxRetryDelay = (int)retryDelay.TotalMilliseconds;
            var rnd = new Random();
            var currentRetry = 0;

            while (currentRetry++ < retryCount)
            {
                if (await action()) return true;
                await Task.Delay(rnd.Next(maxRetryDelay));
            }
            return false;
        }

        private static byte[] CreateUniqueLockId()
        {
            return Guid.NewGuid().ToByteArray();
        }

    }
}
