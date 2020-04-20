using System;
using Unity.Entities;

namespace UnityEngine.SimViz.Content.Pipeline
{
    public class ContentPipeline : IDisposable
    {
        private World world;

        public ContentPipeline()
        {
            world = new World("Content pipeline world");
        }

        public World World => world;

        public void RunGenerator<T, TParameter>(TParameter parameters)
            where T : ComponentSystemBase, IGeneratorSystem<TParameter>
            where TParameter : struct
        {
            var system = world.CreateSystem<T>();
            system.Parameters = parameters;
            system.Update();
        }

        public void RunGenerator<T>()
            where T : ComponentSystemBase, IGeneratorSystem
        {
            var system = world.CreateSystem<T>();
            system.Update();
        }

        public void Dispose()
        {
            world?.Dispose();
        }
    }
}
