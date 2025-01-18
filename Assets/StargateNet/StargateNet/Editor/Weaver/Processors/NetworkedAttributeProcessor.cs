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
        private Dictionary<string, TypeDefinition> _definitions = new();
        private Dictionary<string, CodeGenCallbackData> _propertyToCallbackData;
        private TypeReference _networkBehaviorType;
        private TypeReference _callbackDataType;

        public List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly, AssemblyDefinition refAssembly, Dictionary<string, CodeGenCallbackData> propertyToCallbackData)
        {
            this._propertyToCallbackData = propertyToCallbackData;
            // 先获取Module引用
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    _definitions.TryAdd(type.FullName, type);
                }
            }

            foreach (var module in refAssembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    _definitions.TryAdd(type.FullName, type);
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
                        _networkBehaviorType = type.Module.ImportReference(typeof(NetworkBehavior));
                        _callbackDataType = type.Module.ImportReference(typeof(PropCallbackData));
                        diagnostics.AddRange(ProcessType(type, itfDef));
                    }
                }
            }

            return diagnostics;
        }

        private InterfaceImplementation GetImplementINetworkEntityScript(TypeDefinition type)
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

        private IEnumerable<DiagnosticMessage> ProcessType(TypeDefinition type,
            InterfaceImplementation targetInterfaceImp)
        {
            var diagnostics = new List<DiagnosticMessage>();

            foreach (var field in type.Fields)
            {
                if (field.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                {
                    // 必须是包含的类型，且必须是Property，因为要修改getter和setter
                    if (!StargateNetProcessorUtil.NetworkedableTypes.Contains(field.FieldType.FullName))
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
                if (methodDef.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
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

            // 查找所有的基类，然后计算出总偏移量
            // 查找基类中的 StateBlockSize 属性
            if (type.BaseType != null)
            {
                var queue = new Queue<TypeDefinition>();
                queue.Enqueue(type.BaseType.Resolve());
                while (queue.Count > 0)
                {
                    var currentType = queue.Dequeue();
                    foreach (var prop in currentType.Properties)
                    {
                        if (prop.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                        {
                            lastFieldSize += StargateNetProcessorUtil.CalculateFieldSize(prop.PropertyType);
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

            if (stateBlockProp == null || entityProp == null) return diagnostics;

            foreach (var property in type.Properties)
            {
                if (property.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                {
                    if (!StargateNetProcessorUtil.NetworkedableTypes.Contains(property.PropertyType.FullName))
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
                    setIL.Emit(OpCodes.Ldarga_S, (byte)1); // 参数value的地址
                    setIL.Emit(OpCodes.Conv_U); // 取地址(转成native的int*)
                    setIL.Emit(OpCodes.Ldarg_0); // 加载this
                    setIL.Emit(OpCodes.Callvirt,
                        module.ImportReference(stateBlockProp.GetMethod)); // 加载参数3StateBlock地址
                    setIL.Emit(OpCodes.Ldc_I8, lastFieldSize); // 加载偏移量
                    setIL.Emit(OpCodes.Add); // 加上偏移量
                    setIL.Emit(OpCodes.Ldc_I4, StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType) / 4); // 加载参数4字节数量
                    setIL.Emit(OpCodes.Call, module.ImportReference(_definitions[typeof(StargateNet.Entity).FullName].Methods.First(me => me.Name == nameof(Entity.DirtifyData))));
                    setIL.Emit(OpCodes.Ret); // 返回  

                    lastFieldSize += StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType);
                }
            }

            return diagnostics;
        }

        private void AddPropertyInstructions(PropertyDefinition property, PropertyDefinition stateBlockProp, Int64 lastFieldSize)
        {
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
            setIL.Emit(OpCodes.Ldarga_S, (byte)1); // 参数value的地址
            setIL.Emit(OpCodes.Conv_U); // 取地址(转成native的int*)
            setIL.Emit(OpCodes.Ldarg_0); // 加载this
            setIL.Emit(OpCodes.Callvirt, module.ImportReference(stateBlockProp.GetMethod)); // 加载参数3StateBlock地址
            setIL.Emit(OpCodes.Ldc_I8, lastFieldSize); // 加载偏移量
            setIL.Emit(OpCodes.Add); // 加上偏移量
            setIL.Emit(OpCodes.Ldc_I4,
                (int)(StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType) / 4)); // 加载参数4字节数量
            setIL.Emit(OpCodes.Call,
                module.ImportReference(_definitions[typeof(StargateNet.Entity).FullName].Methods
                    .First(me => me.Name == nameof(Entity.DirtifyData))));
            setIL.Emit(OpCodes.Ret); // 返回  
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
            TypeDefinition sgNetworkUtilDef = _definitions[typeof(StargateNetUtil).FullName];
            switch (propertyDef.PropertyType.FullName)
            {
                case "UnityEngine.Vector4":
                    typeHandler =
                        sgNetworkUtilDef.Methods.First(method => method.Name == nameof(StargateNetUtil.GetVector4));
                    break;
                case "UnityEngine.Vector3":
                    typeHandler =
                        sgNetworkUtilDef.Methods.First(method => method.Name == nameof(StargateNetUtil.GetVector3));
                    break;
                case "UnityEngine.Vector2":
                    typeHandler =
                        sgNetworkUtilDef.Methods.First(method => method.Name == nameof(StargateNetUtil.GetVector2));
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
                    moduleDefinition.ImportReference(_definitions[typeof(NetworkBool).FullName]));
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

        private List<DiagnosticMessage> ProcessMethod(MethodDefinition methodDefinition, Dictionary<string, MethodDefinition> networkCallbacks)
        {
            List<DiagnosticMessage> diagnostics = new List<DiagnosticMessage>();
            // 处理有NetworkCallBackAttribute标记的
            if (methodDefinition.CustomAttributes.All(attr => attr.AttributeType.Name != nameof(NetworkCallBackAttribute)))
                return diagnostics;

            CustomAttribute customAttribute = methodDefinition.CustomAttributes.First(attr => attr.AttributeType.Name == nameof(NetworkCallBackAttribute));
            TypeDefinition typeDefinition = methodDefinition.DeclaringType;
            TypeReference typeReference = typeDefinition.Module.ImportReference(typeDefinition);
            string propName = (string)customAttribute.ConstructorArguments[0].Value;
            bool invokeDurResim = (bool)customAttribute.ConstructorArguments[1].Value;
            MethodReference methodReference = methodDefinition.Module.ImportReference(methodDefinition);
            networkCallbacks.TryAdd(methodDefinition.FullName, CreateCallBackMethod(typeDefinition, typeReference, propName, methodReference));
            typeDefinition.Methods.Add(methodDefinition);
            diagnostics.Add(new DiagnosticMessage()
            {
                DiagnosticType = DiagnosticType.Warning,
                MessageData =
                    $"{methodDefinition.DeclaringType.FullName}.{methodDefinition.Name}",
            });
            return diagnostics;
        }

        /// <summary>
        /// 为属性回调创建Wrapper
        /// </summary>
        /// <param name="typeDefinition">函数所在的类，应当为NetworkBehavior的子类或自身</param>
        /// <param name="behaviourType">NetworkBehaivro</param>
        /// <param name="proName"></param>
        /// <param name="callbackMethod"></param>
        /// <returns></returns>
        private MethodDefinition CreateCallBackMethod(TypeDefinition typeDefinition, TypeReference behaviourType,
            string proName, MethodReference callbackMethod)
        {
            MethodDefinition methodDefinition = new MethodDefinition(proName + "__handler",
                Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig, typeDefinition.Module.TypeSystem.Void);
            methodDefinition.Parameters.Add(new ParameterDefinition("beh", Mono.Cecil.ParameterAttributes.None, _networkBehaviorType));
            methodDefinition.Parameters.Add(new ParameterDefinition("callbk", Mono.Cecil.ParameterAttributes.None, _callbackDataType));
            var ilProcessor = methodDefinition.Body.GetILProcessor();
            var instructions = methodDefinition.Body.Instructions;
            instructions.Add(ilProcessor.Create(OpCodes.Ldarg_0));
            instructions.Add(ilProcessor.Create(OpCodes.Castclass, behaviourType));
            instructions.Add(ilProcessor.Create(OpCodes.Ldarg_1));
            instructions.Add(ilProcessor.Create(OpCodes.Callvirt, callbackMethod));
            instructions.Add(ilProcessor.Create(OpCodes.Ret));
            typeDefinition.Methods.Add(methodDefinition);
            return methodDefinition;
        }
    }
}