using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace StargateNet.Editor.Weaver.Processors
{
    public static class NetworkedAttributeProcessor
    {
        // 所有大于4字节的类型才能传输
        private static HashSet<string> networkedableTypes = new()
        {
            typeof(StargateNet.NetworkBool).FullName,
            typeof(System.Int32).FullName,
            typeof(System.UInt32).FullName,
            typeof(System.Int64).FullName,
            typeof(System.UInt64).FullName,
            typeof(System.Single).FullName,
            typeof(System.Double).FullName,
            typeof(UnityEngine.Vector2).FullName,
            typeof(UnityEngine.Vector3).FullName,
            typeof(UnityEngine.Vector4).FullName,
            // typeof(System.String).FullName,
        };

        private static TypeDefinition _sgNetworkUtilDef = null;
        private static TypeDefinition _networkBoolDef = null;

        public static List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly)
        {
            bool b1 = false, b2 = false;
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    if (type.FullName == typeof(SgNetworkUtil).FullName)
                    {
                        _sgNetworkUtilDef = type;
                        b1 = true;
                    }

                    if (type.FullName == typeof(NetworkBool).FullName)
                    {
                        _networkBoolDef = type;
                        b2 = true;
                    }
                    
                    if(b1 && b2) break;
                }
            }

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

            foreach (var methodDef in type.Methods)
            {
                if (methodDef.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(NetworkedAttribute)))
                {
                    var message = new DiagnosticMessage
                    {
                        DiagnosticType = DiagnosticType.Error,
                        MessageData =
                            $"Method '{methodDef.Name}' in type '{type.FullName}' is a method, method can not be networked.",
                    };
                    diagnostics.Add(message);
                }
            }

            // 找到变量实际的内存State
            var targetProperty = targetInterfaceImp.InterfaceType.Resolve().Properties
                .FirstOrDefault(p => p.Name == "State");
            Int64 lastFieldSize = 0;
            if (targetProperty != null)
            {
                foreach (var property in type.Properties)
                {
                    if (property.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(NetworkedAttribute)))
                    {
                        if (!networkedableTypes.Contains(property.PropertyType.FullName))
                        {
                            var message = new DiagnosticMessage
                            {
                                DiagnosticType = DiagnosticType.Error,
                                MessageData =
                                    $"Property '{property.Name}' in type '{type.FullName}' is of type '{property.PropertyType.FullName}' which can not be networked.",
                            };
                            diagnostics.Add(message);
                            continue;
                        }

                        var module = property.DeclaringType.Module; // 在getter方法前插入将属性值设置为默认值的代码
                        var getIL = property.GetMethod.Body.GetILProcessor(); // 先获取所有的指令，然后清除原先的指令，插入新指令
                        getIL.Body.Instructions.Clear(); // 清除原先的指令
                        getIL.Emit(OpCodes.Ldarg_0); // 加载this
                        getIL.Emit(OpCodes.Callvirt, module.ImportReference(targetProperty.GetMethod)); // 加载this.State值
                        getIL.Emit(OpCodes.Ldc_I8, lastFieldSize); // 加载偏移量
                        getIL.Emit(OpCodes.Add); // 加上偏移量
                        if (IfUnityType(property))
                        {
                            ProcessIfUnityType(property, getIL); // 如果是Unity类型，通过自定义反序列化的方式取出值
                            getIL.Emit(OpCodes.Ret); // 返回   
                        }
                        else
                        {
                            // 数据只有4字节的和8字节的(long)
                            getIL.Emit(OpCodes.Conv_I);
                            ProcessIfPrimitiveType(property, getIL);
                            getIL.Emit(OpCodes.Ret); // 返回  
                        }

                        lastFieldSize += CalculateFieldSize(property.PropertyType);
                    }
                }
            }

            return diagnostics;
        }

        private static bool IfUnityType(PropertyDefinition propertyDef)
        {
            switch (propertyDef.PropertyType.FullName)
            {
                case "UnityEngine.Vector4":
                    return true;
                case "UnityEngine.Vector3":
                    return true;
                case "UnityEngine.Vector2":
                    return true;
            }

            return false;
        }

        private static void ProcessIfUnityType(PropertyDefinition propertyDef, ILProcessor ilProcessor)
        {
            MethodDefinition typeHandler = null;
            switch (propertyDef.PropertyType.FullName)
            {
                case "UnityEngine.Vector4":
                    typeHandler =
                        _sgNetworkUtilDef.Methods.First(method => method.Name == nameof(SgNetworkUtil.GetVector4));
                    break;
                case "UnityEngine.Vector3":
                    typeHandler =
                        _sgNetworkUtilDef.Methods.First(method => method.Name == nameof(SgNetworkUtil.GetVector3));
                    break;
                case "UnityEngine.Vector2":
                    typeHandler =
                        _sgNetworkUtilDef.Methods.First(method => method.Name == nameof(SgNetworkUtil.GetVector2));
                    break;
            }

            if (typeHandler != null)
            {
                ilProcessor.Emit(OpCodes.Call, typeHandler.Resolve());
            }
        }

        private static void ProcessIfPrimitiveType(PropertyDefinition propertyDefinition, ILProcessor ilProcessor)
        {
            string fullName = propertyDefinition.PropertyType.FullName;
            if (fullName == typeof(int).FullName || fullName == typeof(uint).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldind_I4);
            }

            if (fullName == typeof(NetworkBool).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldobj, _networkBoolDef);
            }
            
            if (fullName == typeof(long).FullName || fullName == typeof(ulong).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldind_I8);
            }

            if (fullName == typeof(float).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldind_R4);
            }

            if (fullName == typeof(double).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldind_R8);
            }
        }

        private static long CalculateFieldSize(TypeReference typeReference)
        {
            switch (typeReference.MetadataType)
            {
                case MetadataType.Int32:
                    return sizeof(int);
                case MetadataType.UInt32:
                    return sizeof(uint);
                case MetadataType.Int64:
                    return sizeof(long);
                case MetadataType.UInt64:
                    return sizeof(ulong);
                case MetadataType.Single:
                    return sizeof(float);
                case MetadataType.Double:
                    return sizeof(double);
            }

            switch (typeReference.FullName)
            {
                case "UnityEngine.Vector4":
                    return 4 * sizeof(float);
                case "UnityEngine.Vector3":
                    return 3 * sizeof(float);
                case "UnityEngine.Vector2":
                    return 2 * sizeof(float);
                case "StargateNet.NetworkBool":
                    return sizeof(int);

                default:
                    throw new Exception($"Unsported Type:{typeReference.FullName}");
            }
        }
    }
}