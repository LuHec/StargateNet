// using Mono.Cecil;
// using Mono.Cecil.Cil;
// using System;
// using System.Collections.Generic;
// using Unity.CompilationPipeline.Common.Diagnostics;
//
// namespace StargateNet
// {
// public class RPCProcessor
// {
//     private Dictionary<string, TypeDefinition> _definitions = new();
//     private MethodReference _sendRpcMethodRef;
//     private TypeReference _networkBehaviorRef;
//
//     public List<DiagnosticMessage> ProcessAssembly(AssemblyDefinition assembly, AssemblyDefinition refAssembly)
//     {
//         var diagnostics = new List<DiagnosticMessage>();
//         
//         // 获取 SendRPC 方法引用
//         _sendRpcMethodRef = assembly.MainModule.ImportReference(
//             typeof(StargateEngine).GetMethod("SendRPC"));
//             
//         // 获取 NetworkBehavior 类型引用
//         _networkBehaviorRef = assembly.MainModule.ImportReference(
//             typeof(NetworkBehavior));
//
//         foreach (var module in assembly.Modules)
//         {
//             foreach (var type in module.Types)
//             {
//                 // 检查是否继承自 NetworkBehavior
//                 if (IsNetworkBehavior(type))
//                 {
//                     foreach (var method in type.Methods)
//                     {
//                         if (HasRPCAttribute(method))
//                         {
//                             ProcessRPCMethod(method, diagnostics);
//                         }
//                     }
//                 }
//             }
//         }
//
//         return diagnostics;
//     }
//
//     private bool IsNetworkBehavior(TypeDefinition type)
//     {
//         if (type.BaseType == null)
//             return false;
//
//         if (type.BaseType.FullName == _networkBehaviorRef.FullName)
//             return true;
//
//         // 递归检查基类
//         var baseType = type.BaseType.Resolve();
//         return baseType != null && IsNetworkBehavior(baseType);
//     }
//
//     private bool HasRPCAttribute(MethodDefinition method)
//     {
//         if (!method.HasCustomAttributes)
//             return false;
//
//         foreach (var attribute in method.CustomAttributes)
//         {
//             if (attribute.AttributeType.FullName == typeof(NetworkRPCAttribute).FullName)
//             {
//                 // 检查方法是否为公共方法
//                 if (!method.IsPublic)
//                 {
//                     throw new Exception($"RPC方法 {method.FullName} 必须是公共方法");
//                 }
//                 
//                 // 检查返回类型是否为void
//                 if (method.ReturnType.FullName != typeof(void).FullName)
//                 {
//                     throw new Exception($"RPC方法 {method.FullName} 必须为void返回类型");
//                 }
//
//                 // 检查参数类型
//                 foreach (var parameter in method.Parameters)
//                 {
//                     if (!IsUnmanagedType(parameter.ParameterType))
//                     {
//                         throw new Exception($"RPC方法 {method.FullName} 的参数 {parameter.Name} 必须是非托管类型");
//                     }
//                 }
//
//                 return true;
//             }
//         }
//
//         return false;
//     }
//
//     private bool IsUnmanagedType(TypeReference type)
//     {
//         // 基本类型检查
//         switch (type.FullName)
//         {
//             case "System.Boolean":
//             case "System.Byte":
//             case "System.SByte":
//             case "System.Int16":
//             case "System.UInt16":
//             case "System.Int32":
//             case "System.UInt32":
//             case "System.Int64":
//             case "System.UInt64":
//             case "System.Single":
//             case "System.Double":
//             case "UnityEngine.Vector2":
//             case "UnityEngine.Vector3":
//             case "UnityEngine.Vector4":
//             case "UnityEngine.Quaternion":
//                 return true;
//         }
//
//         // 枚举类型检查
//         var resolvedType = type.Resolve();
//         if (resolvedType != null && resolvedType.IsEnum)
//         {
//             return true;
//         }
//
//         return false;
//     }
//
//     private void ProcessRPCMethod(MethodDefinition method, List<DiagnosticMessage> diagnostics)
//     {
//         var type = method.DeclaringType;
//         var originalName = method.Name;
//         
//         // 1. 重命名原方法为实现方法
//         method.Name = $"{originalName}_Implementation";
//         
//         // 2. 创建新的方法作为RPC入口
//         var rpcMethod = new MethodDefinition(originalName, 
//             method.Attributes, 
//             method.ReturnType);
//
//         // 3. 复制参数
//         foreach (var param in method.Parameters)
//         {
//             rpcMethod.Parameters.Add(new ParameterDefinition(
//                 param.Name, param.Attributes, param.ParameterType));
//         }
//
//         // 4. 生成RPC调用代码
//         var il = rpcMethod.Body.GetILProcessor();
//         
//         // if (!IsServer) { SendRPC(...); return; }
//         il.Emit(OpCodes.Ldarg_0);
//         il.Emit(OpCodes.Call, type.Module.ImportReference(
//             typeof(NetworkBehavior).GetProperty("IsServer").GetGetMethod()));
//         var afterRpcLabel = il.Create(OpCodes.Nop);
//         il.Emit(OpCodes.Brtrue, afterRpcLabel);
//
//         // 收集RPC信息
//         il.Emit(OpCodes.Ldarg_0); // NetworkId
//         il.Emit(OpCodes.Call, type.Module.ImportReference(
//             typeof(NetworkBehavior).GetProperty("NetworkId").GetGetMethod()));
//         
//         il.Emit(OpCodes.Ldarg_0); // ScriptId
//         il.Emit(OpCodes.Call, type.Module.ImportReference(
//             typeof(NetworkBehavior).GetProperty("ScriptId").GetGetMethod()));
//         
//         // 方法ID (通过静态构造器注册)
//         il.Emit(OpCodes.Ldc_I4, RegisterRPCMethod(method));
//         
//         // 序列化参数
//         SerializeParameters(il, method.Parameters);
//         
//         // 发送RPC
//         il.Emit(OpCodes.Call, _sendRpcMethodRef);
//         il.Emit(OpCodes.Ret);
//         
//         il.Append(afterRpcLabel);
//         
//         // 直接调用实现
//         for (int i = 0; i < method.Parameters.Count + 1; i++)
//         {
//             il.Emit(OpCodes.Ldarg, i);
//         }
//         il.Emit(OpCodes.Call, method);
//         il.Emit(OpCodes.Ret);
//
//         type.Methods.Add(rpcMethod);
//
//         // 5. 创建静态处理方法
//         CreateRPCHandler(method, type);
//     }
//
//     private void CreateRPCHandler(MethodDefinition implementation, TypeDefinition type)
//     {
//         var handler = new MethodDefinition($"{implementation.Name}_Handler",
//             MethodAttributes.Static | MethodAttributes.Private,
//             type.Module.TypeSystem.Void);
//
//         handler.Parameters.Add(new ParameterDefinition("instance", 
//             ParameterAttributes.None, 
//             type.Module.ImportReference(typeof(NetworkBehavior))));
//         
//         handler.Parameters.Add(new ParameterDefinition("parameters",
//             ParameterAttributes.None,
//             type.Module.ImportReference(typeof(byte[]))));
//
//         var il = handler.Body.GetILProcessor();
//         
//         // 类型转换和参数反序列化
//         il.Emit(OpCodes.Ldarg_0);
//         il.Emit(OpCodes.Castclass, type);
//         DeserializeParameters(il, implementation.Parameters);
//         
//         // 调用实现方法
//         il.Emit(OpCodes.Call, implementation);
//         il.Emit(OpCodes.Ret);
//
//         type.Methods.Add(handler);
//     }
// }}