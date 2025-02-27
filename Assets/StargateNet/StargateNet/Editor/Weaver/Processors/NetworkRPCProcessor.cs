using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace StargateNet
{
    public class NetworkRPCProcessor
    {
        private Dictionary<string, TypeDefinition> _definitions = new();
        private TypeReference _networkBehaviorRef;
        private MethodReference _startWriteMethodRef;
        private MethodReference _endWriteMethodRef;
        private Dictionary<string, int> _rpcMethodIds = new();
        private int _currentRpcId = 0;

        public List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly, AssemblyDefinition refAssembly)
        {
            var diagnostics = new List<DiagnosticMessage>();

            // 1. 收集RPC方法
            CollectRPCMethodsFromAssemblies(assembly, refAssembly);


            // 3. 处理所有模块中的RPC方法并注册
            ProcessAndRegisterRPCMethods(assembly, diagnostics);

            return diagnostics;
        }

        private void CollectRPCMethodsFromAssemblies(AssemblyDefinition assembly, AssemblyDefinition refAssembly)
        {
            foreach (var module in assembly.Modules)
            {
                CollectRPCMethods(module);
            }

            if (refAssembly != null)
            {
                foreach (var module in refAssembly.Modules)
                {
                    CollectRPCMethods(module);
                }
            }
        }

        private MethodDefinition PrepareInitRpcMethodsMethod(AssemblyDefinition assembly, List<DiagnosticMessage> diagnostics)
        {
            MethodDefinition initRpcMethodsMethod = null;

            // 遍历所有模块查找NetworkRPCManager
            foreach (var module in assembly.Modules)
            {
                var rpcManagerType = module.ImportReference(typeof(NetworkRPCManager)).Resolve();
                if (rpcManagerType != null)
                {
                    initRpcMethodsMethod = rpcManagerType.Methods.FirstOrDefault(m => m.Name == "InitRpcMethods");
                    break;
                }
            }

            return initRpcMethodsMethod;
        }

        private void ProcessAndRegisterRPCMethods(AssemblyDefinition assembly, List<DiagnosticMessage> diagnostics)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    if (!IsNetworkBehavior(type)) continue;

                    var rpcMethods = ProcessTypeRPCMethods(type, diagnostics);
                    diagnostics.Add(new DiagnosticMessage
                    {
                        DiagnosticType = DiagnosticType.Warning,
                        MessageData = $"Found {rpcMethods.Count} RPC methods in {type.FullName}"

                    });
                    RegisterRPCMethods(module, type, rpcMethods);
                }
            }
        }

        private List<(MethodDefinition, MethodDefinition, int)> ProcessTypeRPCMethods(TypeDefinition type, List<DiagnosticMessage> diagnostics)
        {
            var rpcMethods = new List<(MethodDefinition, MethodDefinition, int)>();
            foreach (var method in type.Methods)
            {
                if (HasRPCAttribute(method))
                {
                    rpcMethods.Add(ProcessRPCMethod(type, method, diagnostics));
                }
            }

            return rpcMethods;
        }

        private void RegisterRPCMethods(ModuleDefinition module, TypeDefinition type, List<(MethodDefinition, MethodDefinition, int)> rpcMethods)
        {
            // 查找或创建InternalRegisterRPC方法
            var registerMethod = FindOrAddOverridableMethod(type, "InternalRegisterRPC");
            var il = registerMethod.Body.GetILProcessor();
            il.Clear();
            foreach (var (rpcMethod, staticHandler, rpcId) in rpcMethods)
            {
                // 添加生成的方法到类型中
                type.Methods.Add(rpcMethod);
                type.Methods.Add(staticHandler);

                // 在InitRpcMethods中注册RPC方法
                EmitRPCRegistration(module, type, il, staticHandler, rpcId);
            }
            il.Emit(OpCodes.Ret);
        }

        private void CollectRPCMethods(ModuleDefinition module)
        {
            foreach (var type in module.Types)
            {
                if (IsNetworkBehavior(type))
                {
                    foreach (var method in type.Methods)
                    {
                        if (HasRPCAttribute(method))
                        {
                            // 生成唯一的方法标识符
                            string methodKey = $"{type.FullName}.{method.Name}";
                            if (!_rpcMethodIds.ContainsKey(methodKey))
                            {
                                _rpcMethodIds.Add(methodKey, _currentRpcId);
                                _currentRpcId++;
                            }
                        }
                    }
                }
            }
        }

        private bool IsNetworkBehavior(TypeDefinition type)
        {
            if (type.BaseType == null)
                return false;

            if (type.BaseType.FullName == typeof(NetworkBehavior).FullName)
                return true;

            // 递归检查基类
            var baseType = type.BaseType.Resolve();
            return baseType != null && IsNetworkBehavior(baseType);
        }

        private bool HasRPCAttribute(MethodDefinition method)
        {
            if (!method.HasCustomAttributes)
                return false;

            foreach (var attribute in method.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == typeof(NetworkRPCAttribute).FullName)
                {
                    // 检查方法是否为公共方法
                    if (!method.IsPublic)
                    {
                        throw new Exception($"RPC方法 {method.FullName} 必须是公共方法");
                    }

                    // 检查返回类型是否为void
                    if (method.ReturnType.FullName != typeof(void).FullName)
                    {
                        throw new Exception($"RPC方法 {method.FullName} 必须为void返回类型");
                    }

                    // 检查参数类型
                    foreach (var parameter in method.Parameters)
                    {
                        if (!IsUnmanagedType(parameter.ParameterType))
                        {
                            throw new Exception($"RPC方法 {method.FullName} 的参数 {parameter.Name} 必须是非托管类型");
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        private bool IsUnmanagedType(TypeReference type)
        {
            // 基本类型检查
            switch (type.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                case "System.Single":
                case "System.Double":
                case "UnityEngine.Vector2":
                case "UnityEngine.Vector3":
                case "UnityEngine.Vector4":
                case "UnityEngine.Quaternion":
                    return true;
            }

            // 枚举类型检查
            var resolvedType = type.Resolve();
            if (resolvedType != null && resolvedType.IsEnum)
            {
                return true;
            }

            return false;
        }

        private (MethodDefinition, MethodDefinition, int) ProcessRPCMethod(TypeDefinition typeDefinition, MethodDefinition methodDefinition, List<DiagnosticMessage> diagnostics)
        {
            var module = typeDefinition.Module;

            // 获取EntityGetter
            var entityProp = module.ImportReference(typeof(StargateBehavior)).Resolve().Properties.FirstOrDefault(p => p.Name == "Entity");

            // 获取engine字段
            var engineField = module.ImportReference(typeof(Entity))
                .Resolve()
                .Fields
                .FirstOrDefault(f => f.Name == "engine");

            // 获取NetworkRPCManager getter
            var rpcManagerProp = module.ImportReference(typeof(StargateEngine))
                .Resolve()
                .Properties
                .FirstOrDefault(p => p.Name == "NetworkRPCManager");

            // 获取networkId字段
            var networkIdField = module.ImportReference(typeof(Entity))
                .Resolve()
                .Fields
                .FirstOrDefault(f => f.Name == "networkId");

            // 获取ScriptIdx getter
            var scriptIdProp = module.ImportReference(typeof(StargateBehavior))
                .Resolve()
                .Properties
                .FirstOrDefault(p => p.Name == "ScriptIdx");

            // 获取StartWrite和EndWrite方法
            _startWriteMethodRef = module.ImportReference(typeof(NetworkRPCManager))
                .Resolve()
                .Methods
                .FirstOrDefault(m => m.Name == "StartWrite");

            _endWriteMethodRef = module.ImportReference(typeof(NetworkRPCManager))
                .Resolve()
                .Methods
                .FirstOrDefault(m => m.Name == "EndWrite");

            var originalName = methodDefinition.Name;
            string methodKey = $"{typeDefinition.FullName}.{originalName}";
            int rpcId = _rpcMethodIds[methodKey];

            // 1. 重命名原方法为实现方法
            methodDefinition.Name = $"{originalName}_Implementation";

            // 2. 创建新的RPC入口方法
            var rpcMethod = new MethodDefinition(originalName, Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig, typeDefinition.Module.TypeSystem.Void);

            // 3. 复制参数
            foreach (var param in methodDefinition.Parameters)
            {
                rpcMethod.Parameters.Add(new ParameterDefinition(
                    param.Name, param.Attributes, param.ParameterType));
            }

            // 4. 生成RPC调用代码
            var newMethodIL = rpcMethod.Body.GetILProcessor();
            var instructions = rpcMethod.Body.Instructions;
            instructions.Clear();

            // 计算参数总大小
            int paramByteSize = CalculateParametersSize(methodDefinition.Parameters);

            // 获取From属性
            var rpcAttribute = methodDefinition.CustomAttributes.First(a =>
                a.AttributeType.FullName == typeof(NetworkRPCAttribute).FullName);
            var from = rpcAttribute.ConstructorArguments[0].Value;

            // 正确生成调用链: this.Entity.engine.NetworkRPCManager.StartWrite()
            newMethodIL.Emit(OpCodes.Ldarg_0); // this
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(entityProp.GetMethod)); // get_Entity()
            newMethodIL.Emit(OpCodes.Ldfld, module.ImportReference(engineField)); // .engine
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(rpcManagerProp.GetMethod)); // .NetworkRPCManager

            // NetworkRPCManager.StartWrite参数
            newMethodIL.Emit(OpCodes.Ldc_I4, (int)from); // NetworkRPCFrom
            newMethodIL.Emit(OpCodes.Ldarg_0);
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(entityProp.GetMethod)); // get_Entity()
            newMethodIL.Emit(OpCodes.Ldfld, module.ImportReference(networkIdField)); // .networkId
            newMethodIL.Emit(OpCodes.Ldarg_0);
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(scriptIdProp.GetMethod)); // .ScriptIdx
            newMethodIL.Emit(OpCodes.Ldc_I4, rpcId);
            newMethodIL.Emit(OpCodes.Ldc_I4, paramByteSize);

            // 调用StartWrite
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(_startWriteMethodRef));

            // 写入参数
            foreach (var param in methodDefinition.Parameters)
            {
                WriteParameter(module, newMethodIL, param);
            }

            // 调用EndWrite
            newMethodIL.Emit(OpCodes.Ldarg_0);
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(entityProp.GetMethod));
            newMethodIL.Emit(OpCodes.Ldfld, module.ImportReference(engineField));
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(rpcManagerProp.GetMethod));
            newMethodIL.Emit(OpCodes.Call, module.ImportReference(_endWriteMethodRef));

            // 添加返回指令
            newMethodIL.Emit(OpCodes.Ret);

            // 5. 创建RPC处理方法
            var staticHandler = CreateStaticRpcHandler(module, methodDefinition, typeDefinition);

            return (rpcMethod, staticHandler, rpcId);
        }

        private int CalculateParametersSize(IList<ParameterDefinition> parameters)
        {
            int size = 0;
            foreach (var param in parameters)
            {
                size += GetTypeSize(param.ParameterType);
            }

            return size;
        }

        private int GetTypeSize(TypeReference type)
        {
            switch (type.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                    return 1;
                case "System.Int16":
                case "System.UInt16":
                    return 2;
                case "System.Int32":
                case "System.UInt32":
                case "System.Single":
                    return 4;
                case "System.Int64":
                case "System.UInt64":
                case "System.Double":
                    return 8;
                case "UnityEngine.Vector2":
                    return 8;
                case "UnityEngine.Vector3":
                    return 12;
                case "UnityEngine.Vector4":
                case "UnityEngine.Quaternion":
                    return 16;
                default:
                    if (type.Resolve()?.IsEnum ?? false)
                        return 4;
                    throw new Exception($"Unsupported type: {type.FullName}");
            }
        }

        private void WriteParameter(ModuleDefinition module, ILProcessor il, ParameterDefinition param)
        {
            // 获取EntityGetter
            var entityGetter = module.ImportReference(typeof(StargateBehavior))
                .Resolve()
                .Properties
                .FirstOrDefault(p => p.Name == "Entity").GetMethod;

            // 获取engine字段
            var engineField = module.ImportReference(typeof(Entity))
                .Resolve()
                .Fields
                .FirstOrDefault(f => f.Name == "engine");

            // 获取NetworkRPCManager getter
            var rpcManagerGetter = module.ImportReference(typeof(StargateEngine))
                .Resolve()
                .Properties
                .FirstOrDefault(p => p.Name == "NetworkRPCManager").GetMethod;

            // 获取WriteRPCPram方法
            var writeRpcParamMethod = module.ImportReference(typeof(NetworkRPCManager))
                .Resolve()
                .Methods
                .FirstOrDefault(m => m.Name == "WriteRPCPram");

            // 获取NetworkRPCManager引用
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, module.ImportReference(entityGetter));
            il.Emit(OpCodes.Ldfld, module.ImportReference(engineField));
            il.Emit(OpCodes.Call, module.ImportReference(rpcManagerGetter));

            // 直接获取参数的地址
            il.Emit(OpCodes.Ldarga, param.Index + 1); // +1 因为0是this

            // 计算并加载参数大小
            il.Emit(OpCodes.Ldc_I4, GetTypeSize(param.ParameterType));

            // 调用 WriteRPCPram
            il.Emit(OpCodes.Call, module.ImportReference(writeRpcParamMethod));
        }

        private MethodDefinition CreateStaticRpcHandler(ModuleDefinition module, MethodDefinition implementation, TypeDefinition type)
        {
            var handler = new MethodDefinition($"{implementation.Name}_Handler",
                MethodAttributes.Static | MethodAttributes.Public,
                type.Module.TypeSystem.Void);

            handler.Parameters.Add(new ParameterDefinition("instance",
                ParameterAttributes.None,
                type.Module.ImportReference(typeof(NetworkBehavior))));

            handler.Parameters.Add(new ParameterDefinition("parameters",
                ParameterAttributes.None,
                type.Module.ImportReference(typeof(NetworkRPCPram))));

            var il = handler.Body.GetILProcessor();

            // 类型转换和参数反序列化
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, type);
            DeserializeParameters(module, handler, il, implementation.Parameters);

            // 调用实现方法
            il.Emit(OpCodes.Call, implementation);
            il.Emit(OpCodes.Ret);

            return handler;
        }

        private void DeserializeParameters(ModuleDefinition module, MethodDefinition handler, ILProcessor il, Collection<ParameterDefinition> parameters)
        {
            // 获取NetworkRPCPram的prams字段
            var pramsField = module.ImportReference(typeof(NetworkRPCPram))
                .Resolve()
                .Fields
                .FirstOrDefault(f => f.Name == "prams");

            // 为每个参数创建本地变量
            foreach (var param in parameters)
            {
                handler.Body.Variables.Add(new VariableDefinition(param.ParameterType));
            }

            // 获取基础指针
            il.Emit(OpCodes.Ldarg_1); // 加载NetworkRPCPram参数
            il.Emit(OpCodes.Ldfld, module.ImportReference(pramsField)); // 获取prams指针

            // 按照偏移量读取每个参数
            int offset = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                var param = parameters[i];

                // 如果有偏移，加载新的基址 (prams + offset)
                if (offset > 0)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldfld, module.ImportReference(pramsField));
                    il.Emit(OpCodes.Ldc_I4, offset);
                    il.Emit(OpCodes.Add);
                }

                // 直接解引用: *(type*)(prams + offset)
                il.Emit(OpCodes.Ldobj, param.ParameterType);

                offset += GetTypeSize(param.ParameterType);
            }
        }

        private MethodDefinition FindOrAddOverridableMethod(TypeDefinition typeDefinition, string methodName)
        {
            // 查找已存在的方法
            var method = typeDefinition.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method == null)
            {
                // 创建新方法
                method = new MethodDefinition(methodName,
                    MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    typeDefinition.Module.TypeSystem.Void);
                typeDefinition.Methods.Add(method);

                // 如果不是基类，添加对基类方法的调用
                if (!IsRootBehavior(typeDefinition))
                {
                    var baseMethod = typeDefinition.BaseType.Resolve().Methods
                        .FirstOrDefault(m => m.Name == methodName);

                    var il = method.Body.GetILProcessor();
                    il.Emit(OpCodes.Ldarg_0); // this
                    il.Emit(OpCodes.Call, method.Module.ImportReference(baseMethod));
                    il.Emit(OpCodes.Ret);
                }
            }

            return method;
        }

        private bool IsRootBehavior(TypeDefinition type)
        {
            return type.BaseType?.FullName == typeof(NetworkBehavior).FullName;
        }

        private void EmitRPCRegistration(ModuleDefinition module, TypeDefinition typeDefinition, ILProcessor il, MethodDefinition staticHandler, int rpcId)
        {
            // 提前获取所有需要的引用
            var entityGetter = module.ImportReference(typeof(StargateBehavior))
                .Resolve()
                .Properties
                .FirstOrDefault(p => p.Name == "Entity")
                .GetMethod;

            var engineField = module.ImportReference(typeof(Entity))
                .Resolve()
                .Fields
                .FirstOrDefault(f => f.Name == "engine");

            var rpcManagerGetter = module.ImportReference(typeof(StargateEngine))
                .Resolve()
                .Properties
                .FirstOrDefault(p => p.Name == "NetworkRPCManager")
                .GetMethod;

            var actionCtor = module.ImportReference(typeof(NetworkStaticRpcEvent))
                .Resolve()
                .Methods
                .FirstOrDefault(m => m.IsConstructor);

            var addStaticRpcMethod = module.ImportReference(typeof(NetworkRPCManager))
                .Resolve()
                .Methods
                .FirstOrDefault(m => m.Name == "AddStaticRPC");

            // 生成RPC注册代码
            il.Emit(OpCodes.Ldarg_0); // this
            il.Emit(OpCodes.Call, module.ImportReference(entityGetter));
            il.Emit(OpCodes.Ldfld, module.ImportReference(engineField));
            il.Emit(OpCodes.Call, module.ImportReference(rpcManagerGetter));

            // 加载参数
            il.Emit(OpCodes.Ldc_I4, rpcId);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldftn, staticHandler);

            // 创建委托并调用
            il.Emit(OpCodes.Newobj, module.ImportReference(actionCtor));
            il.Emit(OpCodes.Call, module.ImportReference(addStaticRpcMethod));
        }
    }
}