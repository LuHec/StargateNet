using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;

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

        private IEnumerable<DiagnosticMessage> ProcessType(TypeDefinition type, InterfaceImplementation targetInterfaceImp)
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

            var entityProp = targetInterfaceImp.InterfaceType.Resolve().Properties.FirstOrDefault(p => p.Name == "Entity"); // 找到接口的StateBlock
            var stateBlockProp = targetInterfaceImp.InterfaceType.Resolve().Properties.FirstOrDefault(p => p.Name == "StateBlock"); // 找到接口的Entity
            if (stateBlockProp == null || entityProp == null) return diagnostics;
            
            _networkBehaviorType = type.Module.ImportReference(_definitions[typeof(IStargateNetworkScript).FullName]);
            _callbackDataType = type.Module.ImportReference(_definitions[typeof(CallbackData).FullName]);
            var initMethodReference = type.Module.ImportReference(type.Module.ImportReference(typeof(StargateBehavior)).Resolve().Methods.FirstOrDefault(m => m.Name == "InternalInit"));
            var registerMethodReference = type.Module.ImportReference(type.Module.ImportReference(typeof(Entity)).Resolve().Methods.FirstOrDefault(m => m.Name == "InternalRegisterCallback"));
            var statePtr = type.Module.ImportReference(type.Module.ImportReference(typeof(StargateBehavior)).Resolve().Properties.FirstOrDefault(p => p.Name == "StateBlock").GetMethod);
            var callbackCtor = type.Module.ImportReference(type.Module.ImportReference(typeof(CallbackEvent)).Resolve().Methods.First(m => m.Name == ".ctor"));


            // StartInternalInitModify(initMethodReference.Resolve());
            int lastFieldSize = 0;
            if (type.BaseType != null) // 查找所有的基类，然后计算出总偏移量，查找基类中的 StateBlockSize 属性
            {
                var queue = new Queue<TypeDefinition>();
                queue.Enqueue(type.BaseType.Resolve());
                while (queue.Count > 0)
                {
                    var currentType = queue.Dequeue();
                    foreach (var property in currentType.Properties)
                    {
                        if (property.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                        {
                            lastFieldSize += StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType);
                            if (_propertyToCallbackData.TryGetValue(property.Name, out CodeGenCallbackData callbackData)) // 查找Callback
                            {
                                int propertySize = StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType);
                                AddCallbackInstruction(type, statePtr, initMethodReference.Resolve(), callbackCtor, registerMethodReference, callbackData.methodName, callbackData.invokeDurResim,
                                    lastFieldSize, propertySize);
                                var message = new DiagnosticMessage
                                {
                                    DiagnosticType = DiagnosticType.Warning,
                                    MessageData =
                                        $"Type0--{type.Name}:{property.Name}",
                                }; 
                                diagnostics.Add(message);
                            }
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

            foreach (var property in type.Properties)
            {
                if (property.CustomAttributes.Any(attr => attr.AttributeType.Name == nameof(ReplicatedAttribute)))
                {
                    if (!StargateNetProcessorUtil.NetworkedableTypes.Contains(property.PropertyType.FullName))
                    {
                        var message = new DiagnosticMessage
                        {
                            DiagnosticType = DiagnosticType.Error,
                            MessageData = $"Property '{property.Name}' in type '{type.FullName}' is of type '{property.PropertyType.FullName}' which can not be networked.",
                        };
                        diagnostics.Add(message);
                        continue;
                    }

                    // ------------------ 设置getter
                    AddPropertyInstructions(property, stateBlockProp, ref lastFieldSize);
                    // ------------------ 处理回调，插入Init函数
                    if (_propertyToCallbackData.TryGetValue(property.Name, out CodeGenCallbackData callbackData))
                    {
                        int propertySize = StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType);
                        AddCallbackInstruction(type, statePtr, initMethodReference.Resolve(), callbackCtor, registerMethodReference, callbackData.methodName, callbackData.invokeDurResim, lastFieldSize, propertySize);
                        var message = new DiagnosticMessage
                        {
                            DiagnosticType = DiagnosticType.Warning,
                            MessageData =
                                $"Type1--{type.Name}:{property.Name}",
                        };
                        diagnostics.Add(message);
                    }

                    // ------------------ 插入Init函数
                }
            }

            EndInternalInitModify(initMethodReference.Resolve());
            return diagnostics;
        }

        private void AddPropertyInstructions(PropertyDefinition property, PropertyDefinition stateBlockProp, ref int lastFieldSize)
        {
            var module = property.DeclaringType.Module; // 在getter方法前插入将属性值设置为默认值的代码
            var getIL = property.GetMethod.Body.GetILProcessor(); // 先获取所有的指令，然后清除原先的指令，插入新指令
            getIL.Body.Instructions.Clear(); // 清除原先的指令
            getIL.Emit(OpCodes.Ldarg_0); // 加载this
            getIL.Emit(OpCodes.Callvirt, module.ImportReference(stateBlockProp.GetMethod)); // 加载this.State值
            getIL.Emit(OpCodes.Ldc_I4, lastFieldSize); // 加载偏移量
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
            setIL.Emit(OpCodes.Ldc_I4, lastFieldSize); // 加载偏移量
            setIL.Emit(OpCodes.Add); // 加上偏移量
            setIL.Emit(OpCodes.Ldc_I4, StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType) / 4); // 加载参数4:字数量(即几个int大小)
            setIL.Emit(OpCodes.Call, module.ImportReference(_definitions[typeof(Entity).FullName].Methods.First(me => me.Name == nameof(Entity.DirtifyData))));
            setIL.Emit(OpCodes.Ret); // 返回  

            lastFieldSize += StargateNetProcessorUtil.CalculateFieldSize(property.PropertyType);
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


        /// <summary>
        /// 为属性回调创建Wrapper
        /// </summary>
        /// <param name="typeDefinition">函数所在的类，应当为NetworkBehavior的子类或自身</param>
        /// <param name="behaviourType">NetworkBehaivro</param>
        /// <param name="callbackName"></param>
        /// <param name="callbackMethod"></param>
        /// <returns></returns>
        private MethodDefinition CreateCallBackMethod(TypeDefinition typeDefinition, TypeReference behaviourType, string callbackName, MethodReference callbackMethod)
        {
            MethodDefinition methodDefinition = new MethodDefinition(callbackName + "__handler",
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

        private void StartInternalInitModify(MethodDefinition initMethod)
        {
            initMethod.Body.Instructions.Clear();
        }

        /// <summary>
        /// 在NetowrkBehavior.InternalInit函数中插入指令，调用Entity.InternalRegisterCallback，从而为每一个int*分配回调函数
        /// </summary>
        private void AddCallbackInstruction(
            TypeDefinition type,
            MethodReference entityStatePtr,
            MethodDefinition initMethod,
            MethodReference callbackCtor,
            MethodReference internalRegMethod,
            string callbackMethodName,
            bool invokeDurResim,
            int stateOffset,
            int propertySize
        )
        {
            MethodDefinition methodDefinition = type.GetMethods().FirstOrDefault(method => method.Name == callbackMethodName);
            if (callbackMethodName == "" || methodDefinition == null) return;
            MethodDefinition callbackWarpper = CreateCallBackMethod(type, type.Module.ImportReference(type), callbackMethodName, methodDefinition);
            int wordSize = propertySize / sizeof(int);
            ILProcessor ilProcessor = initMethod.Body.GetILProcessor();
            Collection<Instruction> instructions = ilProcessor.Body.Instructions;
            for (int index = 0; index < wordSize; index++)
            {
                instructions.Add(ilProcessor.Create(OpCodes.Ldarg_0)); // 加载StargateScript自身
                instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, invokeDurResim ? 1 : 0));
                instructions.Add(ilProcessor.Create(OpCodes.Ldarg_0));
                instructions.Add(ilProcessor.Create(OpCodes.Callvirt, entityStatePtr)); // 加载Script.state指针，此处为基地址
                instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, stateOffset / 4)); // 通过offset得到属性的内存地址
                instructions.Add(ilProcessor.Create(OpCodes.Add)); // 将地址和偏移量相加：(int*)ptr + (int)offset 
                instructions.Add(ilProcessor.Create(OpCodes.Ldarg_0));
                instructions.Add(ilProcessor.Create(OpCodes.Callvirt, entityStatePtr));
                instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, stateOffset / 4 + index));
                instructions.Add(ilProcessor.Create(OpCodes.Add));
                instructions.Add(ilProcessor.Create(OpCodes.Ldc_I4, propertySize / 4));
                instructions.Add(ilProcessor.Create(OpCodes.Ldftn, callbackWarpper)); // 创建CallbackEvent委托
                instructions.Add(ilProcessor.Create(OpCodes.Newobj, callbackCtor));
                instructions.Add(ilProcessor.Create(OpCodes.Call, internalRegMethod)); // 调用注册函数
            }
        }

        private void EndInternalInitModify(MethodDefinition initMethod)
        {
            ILProcessor ilProcessor = initMethod.Body.GetILProcessor();
            initMethod.Body.Instructions.Add(ilProcessor.Create(OpCodes.Ret));
        }

        private bool IsValidCallbackMethod(MethodDefinition method)
        {
            return method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == typeof(CallbackData).FullName;
        }

        // private bool IsRootBehavior(TypeDefinition type)
        // {
        //     return type. type.FullName == typeof(NetworkBehavior).FullName ;
        // }
    }
}