using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace StargateNet
{
    public class NetworkBehaviorProcessor
    {
        public List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly)
        {
            // 找到所有继承自NetworkBehavior的，统计其Sync var大小
            var diagnostic = new List<DiagnosticMessage>();

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    if (InheritsFromNetworkBehavior(type))
                    {
                        diagnostic.AddRange(ProcessType(type));
                    }
                }
            }

            return diagnostic;
        }

        private bool InheritsFromNetworkBehavior(TypeDefinition typeDefinition)
        {
            var baseType = typeDefinition.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == nameof(NetworkBehavior))
                {
                    return true;
                }

                baseType = baseType.Resolve().BaseType;
            }

            return false;
        }


        private List<DiagnosticMessage> ProcessType(TypeDefinition typeDefinition)
        {
            var diagnostics = new List<DiagnosticMessage>();
            int size = 0;


            // 查找所有的基类，然后计算出总偏移量
            // 查找基类中的 StateBlockSize 属性
            var targetInterfaceFullName = typeof(IStargateNetworkScript).FullName;

            if (typeDefinition.BaseType != null)
            {
                var queue = new Queue<TypeDefinition>();
                queue.Enqueue(typeDefinition.BaseType.Resolve());
                while (queue.Count > 0)
                {
                    var currentType = queue.Dequeue();
                    // diagnostics.Add(new DiagnosticMessage()
                    // {
                    //     DiagnosticType = DiagnosticType.Warning,
                    //     MessageData = $"handling:{typeDefinition} from {currentType.FullName}"
                    // });

                    foreach (var prop in currentType.Properties)
                    {
                        // diagnostics.Add(new DiagnosticMessage()
                        // {
                        //     DiagnosticType = DiagnosticType.Warning,
                        //     MessageData =
                        //         $"handling:{typeDefinition.FullName},baseTypr:{currentType.FullName}, prop:{prop.PropertyType.FullName}"
                        // });
                        if (prop.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                        {
                            size += StargateNetProcessorUtil.CalculateFieldSize(prop.PropertyType);
                        }
                    }

                    var baseType = currentType.BaseType?.Resolve(); 
                    // 在NetworkBehavior处就可以停了，往上不会再有网络变量
                    if (baseType != null && baseType.FullName != typeof(StargateBehavior).FullName)
                    {
                        queue.Enqueue(baseType);
                    }
                }
            }

            // diagnostics.Add(new DiagnosticMessage()
            // {
            //     DiagnosticType = DiagnosticType.Warning,
            //     MessageData = $"handling:{typeDefinition.FullName},offset is:{size}"
            // });


            // 查找或创建派生类中的 StateBlockSize 属性
            var targetProp = typeDefinition.Properties.FirstOrDefault(p => p.Name == "StateBlockSize");
            if (targetProp == null)
            {
                // 创建属性
                targetProp = new PropertyDefinition("StateBlockSize", PropertyAttributes.None,
                    typeDefinition.Module.TypeSystem.Int32)
                {
                    GetMethod = new MethodDefinition("get_StateBlockSize",
                        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName |
                        MethodAttributes.HideBySig,
                        typeDefinition.Module.TypeSystem.Int32)
                };

                typeDefinition.Properties.Add(targetProp);
                typeDefinition.Methods.Add(targetProp.GetMethod);
            }
            else if (targetProp.GetMethod == null)
            {
                // 如果属性存在但没有 getter 方法，创建 getter 方法
                var getMethod = new MethodDefinition("get_StateBlockSize",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    typeDefinition.Module.TypeSystem.Int32);
                targetProp.GetMethod = getMethod;
                typeDefinition.Methods.Add(getMethod);
            }

            // 标记为重写基类的虚属性
            targetProp.GetMethod.Attributes |= MethodAttributes.Virtual;

            // 计算 byteSize
            foreach (var property in typeDefinition.Properties)
            {
                if (property.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                {
                    size += StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType);
                }
            }

            // 生成 IL 代码
            var getIL = targetProp.GetMethod.Body.GetILProcessor();
            getIL.Body.Instructions.Clear();
            getIL.Emit(OpCodes.Ldc_I4, size);
            getIL.Emit(OpCodes.Ret);

            return diagnostics;
        }
    }
}