using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace StargateNet.Editor.Weaver.Processors
{
    public static class NetworkedAttributeProcessor
    {
        private static HashSet<string> networkedableTypes = new()
        {
            typeof(System.Boolean).FullName,
            typeof(System.Byte).FullName,
            typeof(System.SByte).FullName,
            typeof(System.Int16).FullName,
            typeof(System.UInt16).FullName,
            typeof(System.Int32).FullName,
            typeof(System.UInt32).FullName,
            typeof(System.Int64).FullName,
            typeof(System.UInt64).FullName,
            typeof(System.Single).FullName,
            typeof(System.Double).FullName,
            typeof(System.String).FullName,
        };

        public static List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly)
        {
            var diagnostics = new List<DiagnosticMessage>();

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    var itfDef = GetImplementINetworkEntityScript(type);
                    if (itfDef != null) diagnostics.AddRange(ProcessType(type, itfDef));
                }
            }

            return diagnostics;
        }

        private static InterfaceImplementation GetImplementINetworkEntityScript(TypeDefinition type)
        {
            // 获取目标接口的完整名称
            var targetInterfaceFullName = typeof(INetworkEntityScript).FullName;

            // 检查当前类型是否实现了目标接口
            var interfaceImplementations = type.Interfaces;
            foreach (var interfaceImplementation in interfaceImplementations)
            {
                if (interfaceImplementation.InterfaceType.Resolve().FullName == targetInterfaceFullName)
                {
                    return interfaceImplementation;
                }
            }

            // 遍历基类，检查是否实现了目标接口
            var cursor = type.BaseType;
            while (cursor != null)
            {
                var resolvedBaseType = cursor.Resolve();
                interfaceImplementations = resolvedBaseType.Interfaces;
                foreach (var interfaceImplementation in interfaceImplementations)
                {
                    if (interfaceImplementation.InterfaceType.Resolve().FullName == targetInterfaceFullName)
                    {
                        return interfaceImplementation;
                    }
                }

                cursor = resolvedBaseType.BaseType;
            }

            return null;
        }

        private static IEnumerable<DiagnosticMessage> ProcessType(TypeDefinition type,
            InterfaceImplementation targetInterfaceImp)
        {
            var diagnostics = new List<DiagnosticMessage>();

            foreach (var field in type.Fields)
            {
                if (field.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(NetworkedAttribute)))
                {
                    // 必须是包含的类型，且必须是Property，因为要修改getter和setter
                    if (!networkedableTypes.Contains(field.FieldType.FullName))
                    {
                        var message = new DiagnosticMessage
                        {
                            DiagnosticType = DiagnosticType.Error,
                            MessageData =
                                $"Field '{field.Name}' in type '{type.FullName}' is of type '{field.FieldType.FullName}' which can not be networked.",
                        };
                        diagnostics.Add(message);
                    }
                    else
                    {
                        var message = new DiagnosticMessage
                        {
                            DiagnosticType = DiagnosticType.Error,
                            MessageData =
                                $"Field '{field.Name}' in type '{type.FullName}' is of type '{field.FieldType.FullName}' is a field, use property instead(networked must be property).",
                        };
                        diagnostics.Add(message);
                    }
                }
            }

            // 找到变量实际的内存State
            var targetProperty = targetInterfaceImp.InterfaceType.Resolve().Properties
                .FirstOrDefault(p => p.Name == "State");

            if (targetProperty != null)
            {
                foreach (var property in type.Properties)
                {
                    // 在getter方法前插入将属性值设置为默认值的代码
                    var module = property.DeclaringType.Module;

                    // 先获取所有的指令，然后清除原先的指令，插入新指令
                    var getIL = property.GetMethod.Body.GetILProcessor();
                    // 清除原先的指令
                    getIL.Body.Instructions.Clear();
                    getIL.Emit(OpCodes.Ldarg_0); // 加载this
                    getIL.Emit(OpCodes.Callvirt, module.ImportReference(targetProperty.GetMethod)); // 加载this.State值
                    getIL.Emit(OpCodes.Ldc_I4_0); // 加载索引0
                    getIL.Emit(OpCodes.Ldelem_I4); // 获取数组元素
                    getIL.Emit(OpCodes.Ret); // 返回
                }
            }

            return diagnostics;
        }
    }
}