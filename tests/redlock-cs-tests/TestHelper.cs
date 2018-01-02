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
            var toolsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".nuget\packages\redis-32\2.6.12.1\tools");
            var fileName = Path.Combine(toolsPath, "redis-server.exe");

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

        private static string GetRepoRoot()
        {
            var appVeyor = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            if (!string.IsNullOrEmpty(appVeyor))
            {
                return appVeyor;
            }
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(assemblyDir, @"..\..\..\..\");
        }

        
    }
}