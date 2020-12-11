using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Compilation;

namespace Unity.Entities.Tests {
    public static class TestWorldSetup
    {
        public static readonly string[] EntitiesPackage = { "com.unity.entities" };
        public static IEnumerable<Type> FilterSystemsToPackages(IEnumerable<Type> systems, IEnumerable<string> packageNames)
        {
            const string packagePrefix = "Packages";
            foreach (var s in systems)
            {
                var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(s.Assembly.GetName().Name);
                if (path == null)
                    continue;
                if (!path.StartsWith(packagePrefix))
                    continue;
                var packagePath = path.Substring(packagePrefix.Length + 1);
                if (packageNames.Any(packagePath.StartsWith))
                    yield return s;
            }
        }

        public static IEnumerable<Type> GetDefaultInitSystemsFromEntitiesPackage(WorldSystemFilterFlags flags) => FilterSystemsToPackages(
            DefaultWorldInitialization.GetAllSystems(flags), EntitiesPackage
        );

        public static World CreateEntityWorld(string name, bool isEditor)
        {
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default, true); ;
            var world = new World(name, isEditor ? WorldFlags.Editor : WorldFlags.Game);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, FilterSystemsToPackages(systems, EntitiesPackage));
            return world;
        }
    }
}
