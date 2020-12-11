using Unity.Assertions;

namespace Unity.Entities
{
    public sealed unsafe partial class EntityManager
    {
        // ----------------------------------------------------------------------------------------------------------
        // PUBLIC
        // ----------------------------------------------------------------------------------------------------------

        // @TODO documentation for serialization/deserialization
        /// <summary>
        /// Prepares an empty <see cref="World"/> to load serialized entities.
        /// </summary>
        public void PrepareForDeserialize()
        {
            if (Debug.EntityCount != 0)
            {
                using (var allEntities = GetAllEntities())
                {
                    throw new System.ArgumentException($"PrepareForDeserialize requires the world to be completely empty, but there are {allEntities.Length}.\nFor example: {Debug.GetEntityInfo(allEntities[0])}");
                }
            }
            
            m_ManagedComponentStore.PrepareForDeserialize();
        }
        
        // ----------------------------------------------------------------------------------------------------------
        // INTERNAL
        // ----------------------------------------------------------------------------------------------------------
   
    }
}
