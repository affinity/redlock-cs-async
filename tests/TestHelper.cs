using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Redlock.CSharp.Tests
{
    public static class TestHelper
    {
        public static Process StartRedisServer(long port)
        {
            var fileName = GetRedisServerLocation();

            // Launch Server
            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = "--port " + port,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine($"Attempt to launch {fileName} failed.");
                Console.WriteLine("Directory listing:");
                foreach (var file in Directory.GetFiles(Path.GetDirectoryName(fileName)))
                {
                    Console.WriteLine($"\t{Path.GetFileName(file)}");
                }
                throw;
            }

            return process;
        }

        private static string GetRedisServerLocation()
        {
            var appVeyor = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            if (!string.IsNullOrEmpty(appVeyor))
            {
                return Path.Combine(appVeyor, "tests", "bin", "Release", "redis-server.exe");
            }
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(assemblyDir, "redis-server.exe");
        }
    }
}