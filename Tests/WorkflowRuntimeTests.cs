using System;
using System.IO;
using NUnit.Framework;

namespace NINA.Plugin.DynamicCooling.Tests {
    [TestFixture]
    public class WorkflowRuntimeTests {
        [Test]
        public void CiActionsDeclareNode24RuntimeMajors() {
            var root = FindRepositoryRoot();
            var workflow = File.ReadAllText(
                Path.Combine(root, ".github", "workflows", "build-and-test.yml")
            );

            Assert.Multiple(() => {
                Assert.That(Count(workflow, "actions/checkout@v7"), Is.EqualTo(1));
                Assert.That(Count(workflow, "actions/setup-dotnet@v6"), Is.EqualTo(1));
                Assert.That(Count(workflow, "actions/upload-artifact@v7"), Is.EqualTo(1));
                Assert.That(workflow, Does.Not.Contain("actions/checkout@v4"));
                Assert.That(workflow, Does.Not.Contain("actions/setup-dotnet@v4"));
                Assert.That(workflow, Does.Not.Contain("actions/upload-artifact@v4"));
            });
        }

        private static string FindRepositoryRoot() {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null) {
                if (Directory.Exists(Path.Combine(directory.FullName, ".github"))) {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate repository root.");
        }

        private static int Count(string value, string needle) {
            return (value.Length - value.Replace(needle, string.Empty).Length) / needle.Length;
        }
    }
}
