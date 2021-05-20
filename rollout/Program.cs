using Quokka.Rollout;
using System;
using System.IO;
using System.Linq;

namespace rollout
{
    class Program
    {
        static void Main(string[] args)
        {
            var solutions = new[]
            {
                "Quokka.RTL",
            };

            RolloutProcess.Run(new RolloutConfig()
            {
                ProjectPath = Path.Combine(RolloutTools.SolutionLocation(), "Quokka.Extension.Interop", "Quokka.Extension.Interop.csproj"),
                LocalPublishLocation = @"c:\code\LocalNuget",
                Nuget = new NugetPushConfig()
                {
                    APIKeyLocation = @"c:\code\LocalNuget\nuget.key.zip",
                    APIKeyLocationRequiredPassword = true
                },
                ReferenceFolders = solutions.Select(s => Path.Combine(@"c:\code", s)).ToList()
            });
        }
    }
}
