using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;

namespace StargateNet
{
    public class NetworkCallBackProcessor
    {
        private Dictionary<string, TypeDefinition> _definitions = new();

        /// <summary>
        /// RepPropertyName:CallbackData
        /// </summary>
        private Dictionary<string, CodeGenCallbackData> _propertyToCallbackData = new();

        private TypeReference _networkBehaviorType;
        private TypeReference _callbackDataType;

        public List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly, AssemblyDefinition refAssembly, ref Dictionary<string, CodeGenCallbackData> propertyToCallbackData)
        {
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
                        diagnostics.AddRange(CollectMethod(type));
                    }
                }
            }


            propertyToCallbackData = this._propertyToCallbackData;
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

        private List<DiagnosticMessage> CollectMethod(TypeDefinition typeDefinition)
        {
            List<DiagnosticMessage> diagnostics = new();
            foreach (MethodDefinition method in typeDefinition.Methods)
            {
                foreach (CustomAttribute customAttribute in method.CustomAttributes)
                {
                    if (customAttribute.AttributeType.FullName == typeof(NetworkCallBackAttribute).FullName)
                    {
                        string propName = (string)customAttribute.ConstructorArguments[0].Value;
                        bool invokeDurResim = (bool)customAttribute.ConstructorArguments[1].Value;
                        if (this._propertyToCallbackData.ContainsKey(propName))
                            diagnostics.Add(new DiagnosticMessage()
                            {
                                DiagnosticType = DiagnosticType.Error,
                                MessageData = "You have more than one OnChanged attribute referring to the same property: " + propName + " on " + typeDefinition.Name +
                                              ". This is not allowed, you must only have one event per property."
                            });
                        else
                            this._propertyToCallbackData.Add(propName, new CodeGenCallbackData() { methodName = method.Name, invokeDurResim = invokeDurResim });
                        if (!this.IsValidPropertyChangedCallback(method))
                            diagnostics.Add(new DiagnosticMessage()
                            {
                                DiagnosticType = DiagnosticType.Error,
                                MessageData = method.FullName + ": incorrect OnChanged method definition. An OnChanged method must have one single parameter of OnChangedData type."
                            });
                    }
                }
            }

            return diagnostics;
        }

        private List<DiagnosticMessage> ProcessMethod(MethodDefinition methodDefinition,
            Dictionary<string, MethodDefinition> networkCallbacks)
        {
            List<DiagnosticMessage> diagnostics = new List<DiagnosticMessage>();
            // 处理有NetworkCallBackAttribute标记的
            if (methodDefinition.CustomAttributes.All(attr =>
                    attr.AttributeType.Name != nameof(NetworkCallBackAttribute)))
                return diagnostics;

            CustomAttribute customAttribute =
                methodDefinition.CustomAttributes.First(attr =>
                    attr.AttributeType.Name == nameof(NetworkCallBackAttribute));
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
                Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static |
                Mono.Cecil.MethodAttributes.HideBySig, typeDefinition.Module.TypeSystem.Void);
            methodDefinition.Parameters.Add(new ParameterDefinition("beh", Mono.Cecil.ParameterAttributes.None,
                _networkBehaviorType));
            methodDefinition.Parameters.Add(new ParameterDefinition("callbk", Mono.Cecil.ParameterAttributes.None,
                _callbackDataType));
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

        private bool IsValidPropertyChangedCallback(MethodDefinition methodDefinition)
        {
            return methodDefinition.Parameters.Count == 1 && methodDefinition.Parameters[0].ParameterType.Name == nameof(OnChangedData);
        }
    }
}