using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace StargateNet
{
    public class NetworkedAttributeProcessor
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

        private  Dictionary<string, TypeDefinition> definitions = new();

        public  List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly,AssemblyDefinition refAssembly)
        {
            definitions.Clear();
            // 先获取Module引用
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    definitions.TryAdd(type.FullName, type);
                }
            }
            
            foreach (var module in refAssembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    definitions.TryAdd(type.FullName, type);
                }
            }

            var diagnostics = new List<DiagnosticMessage>();

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    var itfDef = GetImplementINetworkEntityScript(type);
                    if (itfDef != null)
                    {
                        diagnostics.AddRange(ProcessType(type, itfDef));
                    }
                }
            }

            return diagnostics;
        }

        private  InterfaceImplementation GetImplementINetworkEntityScript(TypeDefinition type)
        {
            // 不能多继承来着，这里bfs毫无意义
            var targetInterfaceFullName = typeof(IStargateNetworkScript).FullName;
            var queue = new Queue<TypeDefinition>();
            queue.Enqueue(type);

            while (queue.Count > 0)
            {
                var currentType = queue.Dequeue();
                var interfaceImplementations = currentType.Interfaces;

                foreach (var interfaceImplementation in interfaceImplementations)
                {
                    if (interfaceImplementation.InterfaceType.Resolve().FullName == targetInterfaceFullName)
                    {
                        return interfaceImplementation;
                    }
                }

                var baseType = currentType.BaseType?.Resolve();
                if (baseType != null)
                {
                    queue.Enqueue(baseType);
                }
            }

            return null;
        }

        private  IEnumerable<DiagnosticMessage> ProcessType(TypeDefinition type,
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

            // 找到接口的StateBlock
            var entityProp = targetInterfaceImp.InterfaceType.Resolve().Properties
                .FirstOrDefault(p => p.Name == "Entity");
            // 找到接口的Entity
            var stateBlockProp = targetInterfaceImp.InterfaceType.Resolve().Properties
                .FirstOrDefault(p => p.Name == "StateBlock");
            Int64 lastFieldSize = 0;
            if (stateBlockProp != null && entityProp != null)
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

                        // ------------------ 设置getter
                        var module = property.DeclaringType.Module; // 在getter方法前插入将属性值设置为默认值的代码
                        var getIL = property.GetMethod.Body.GetILProcessor(); // 先获取所有的指令，然后清除原先的指令，插入新指令
                        getIL.Body.Instructions.Clear(); // 清除原先的指令
                        getIL.Emit(OpCodes.Ldarg_0); // 加载this
                        getIL.Emit(OpCodes.Callvirt, module.ImportReference(stateBlockProp.GetMethod)); // 加载this.State值
                        getIL.Emit(OpCodes.Ldc_I8, lastFieldSize); // 加载偏移量
                        getIL.Emit(OpCodes.Add); // 加上偏移量
                        if (IfUnityType(property))
                        {
                            ProcessIfUnityType(property, getIL, module); // 如果是Unity类型，通过自定义反序列化的方式取出值
                            getIL.Emit(OpCodes.Ret); // 返回   
                        }
                        else
                        {
                            // 数据只有4字节的和8字节的(long)
                            getIL.Emit(OpCodes.Conv_I);
                            ProcessIfPrimitiveType(property, getIL, module);
                            getIL.Emit(OpCodes.Ret); // 返回  
                        }

                        // ------------------ 设置Setter
                        var setIL = property.SetMethod.Body.GetILProcessor();
                        setIL.Body.Instructions.Clear();
                        setIL.Emit(OpCodes.Ldarg_0); // 加载参数1this
                        setIL.Emit(OpCodes.Ldarga_S, (byte)(property.SetMethod.Parameters.First(arg=>arg.Name == "value").Index)); //加载参数2value地址
                        setIL.Emit(OpCodes.Conv_U); // 取地址
                        setIL.Emit(OpCodes.Ldarg_0); // 加载this
                        setIL.Emit(OpCodes.Callvirt,
                            module.ImportReference(stateBlockProp.GetMethod)); // 加载参数3StateBlock地址
                        setIL.Emit(OpCodes.Ldc_I8, lastFieldSize); // 加载偏移量
                        setIL.Emit(OpCodes.Add); // 加上偏移量
                        setIL.Emit(OpCodes.Ldc_I4, (int)(CalculateFieldSize(property.PropertyType) / 4)); // 加载参数4字节数量
                        setIL.Emit(OpCodes.Call,
                            module.ImportReference(definitions[typeof(StargateNet.Entity).FullName].Methods
                                .First(me => me.Name == nameof(Entity.DirtifyData))));
                        setIL.Emit(OpCodes.Ret); // 返回  

                        lastFieldSize += CalculateFieldSize(property.PropertyType);
                    }
                }
            }

            return diagnostics;
        }

        private bool IfUnityType(PropertyDefinition propertyDef)
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

        private void ProcessIfUnityType(PropertyDefinition propertyDef, ILProcessor ilProcessor,
            ModuleDefinition moduleDefinition)
        {
            MethodDefinition typeHandler = null;
            TypeDefinition sgNetworkUtilDef = definitions[typeof(SgNetworkUtil).FullName];
            switch (propertyDef.PropertyType.FullName)
            {
                case "UnityEngine.Vector4":
                    typeHandler =
                        sgNetworkUtilDef.Methods.First(method => method.Name == nameof(SgNetworkUtil.GetVector4));
                    break;
                case "UnityEngine.Vector3":
                    typeHandler =
                        sgNetworkUtilDef.Methods.First(method => method.Name == nameof(SgNetworkUtil.GetVector3));
                    break;
                case "UnityEngine.Vector2":
                    typeHandler =
                        sgNetworkUtilDef.Methods.First(method => method.Name == nameof(SgNetworkUtil.GetVector2));
                    break;
            }

            if (typeHandler != null)
            {
                ilProcessor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeHandler.Resolve()));
            }
        }

        private void ProcessIfPrimitiveType(PropertyDefinition propertyDefinition, ILProcessor ilProcessor,
            ModuleDefinition moduleDefinition)
        {
            string fullName = propertyDefinition.PropertyType.FullName;
            if (fullName == typeof(int).FullName || fullName == typeof(uint).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldind_I4);
            }

            if (fullName == typeof(NetworkBool).FullName)
            {
                ilProcessor.Emit(OpCodes.Ldobj,
                    moduleDefinition.ImportReference(definitions[typeof(NetworkBool).FullName]));
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

        private long CalculateFieldSize(TypeReference typeReference)
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