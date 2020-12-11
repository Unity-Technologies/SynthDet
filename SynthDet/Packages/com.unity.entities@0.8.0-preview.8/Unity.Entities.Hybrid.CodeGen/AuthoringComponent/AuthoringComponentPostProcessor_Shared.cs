using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Unity.Entities.CodeGen;

[assembly: InternalsVisibleTo("Unity.Entities.Hybrid.CodeGen.Tests")]
namespace Unity.Entities.Hybrid.CodeGen
{
    internal partial class AuthoringComponentPostProcessor : EntitiesILPostProcessor
    {
        private bool HasGenerateAuthoringComponentAttribute(TypeDefinition typeDefinition)
        {
            return typeDefinition.HasCustomAttributes 
                   && typeDefinition.CustomAttributes.Any(
                       c => c.AttributeType.Name == nameof(GenerateAuthoringComponentAttribute));
        }

        private bool ShouldGenerateAuthoringComponentForBufferElementData(TypeDefinition typeDefinition)
        {
            return typeDefinition.Interfaces.Any(i => i.InterfaceType.Name == nameof(IBufferElementData)) &&
                   HasGenerateAuthoringComponentAttribute(typeDefinition);
        }

        private bool ShouldGenerateComponentDataAuthoringComponent(TypeDefinition typeDefinition)
        {
            return typeDefinition.Interfaces.Any(i => i.InterfaceType.Name == nameof(IComponentData)) &&
                   HasGenerateAuthoringComponentAttribute(typeDefinition);
        }

        protected override bool PostProcessImpl()
        {
            var mainModule = AssemblyDefinition.MainModule;

            TypeDefinition[] componentDataTypesRequiringAuthoringComponent = 
                mainModule.Types.Where(ShouldGenerateComponentDataAuthoringComponent).ToArray();
            TypeDefinition[] bufferElementTypesRequiringAuthoringComponent =
                mainModule.Types.Where(ShouldGenerateAuthoringComponentForBufferElementData).ToArray();
            
            if (componentDataTypesRequiringAuthoringComponent.Length == 0
                && bufferElementTypesRequiringAuthoringComponent.Length == 0)
            {
                return false;
            }
                
            foreach (var componentDataType in componentDataTypesRequiringAuthoringComponent)
            {
                CreateComponentDataAuthoringType(componentDataType);
            }

            foreach (var bufferElementType in bufferElementTypesRequiringAuthoringComponent)
            {
                CreateBufferElementDataAuthoringType(bufferElementType);
            }
            return true;
        }

        static MethodDefinition CreateEmptyConvertMethod(ModuleDefinition componentDataModule, TypeDefinition authoringType)
        {
            return 
                CecilHelpers.AddMethodImplementingInterfaceMethod(
                    componentDataModule, 
                    authoringType,
                    typeof(IConvertGameObjectToEntity).GetMethod(nameof(IConvertGameObjectToEntity.Convert)));
        }
    }
}