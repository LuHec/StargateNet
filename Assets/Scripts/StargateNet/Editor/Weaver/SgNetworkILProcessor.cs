using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using StargateNet;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace StargateNet
{
    public class SgNetworkILProcessor : ILPostProcessor
    {
        private const string StargateNetAsmdefName = "Unity.StargateNet";

        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            // 筛选出引用了或者本身就是SgNetwork dll的程序集
            bool relevant = compiledAssembly.Name == StargateNetAsmdefName || compiledAssembly.References.Any(
                filePath =>
                    Path.GetFileNameWithoutExtension(filePath) == StargateNetAsmdefName);
            relevant &= compiledAssembly.Name != "Assembly-CSharp-Editor";
            return relevant;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!this.WillProcess(compiledAssembly)) return new ILPostProcessResult(null!);
            var loader = new AssemblyResolver();

            var folders = new HashSet<string>();
            foreach (var reference in compiledAssembly.References)
                folders.Add(Path.Combine(Environment.CurrentDirectory, Path.GetDirectoryName(reference)));

            var folderList = folders.OrderBy(x => x);
            foreach (var folder in folderList) loader.AddSearchDirectory(folder);

            var readerParameters = new ReaderParameters
            {
                InMemory = true,
                AssemblyResolver = loader,
                ReadSymbols = true,
                ReadingMode = ReadingMode.Deferred
            };

            // 读入符号表
            readerParameters.SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData);
            // 读入目标程序集定义
            var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(compiledAssembly.InMemoryAssembly.PeData),
                readerParameters);
            // 排除没有引用StargateNet的程序集
            if (assembly.MainModule.AssemblyReferences.All(refName => refName.Name != StargateNetAsmdefName) &&
                compiledAssembly.Name != StargateNetAsmdefName)
                return new ILPostProcessResult(null!);
            // 载入sgnet程序集
            AssemblyDefinition refAssembly = assembly;
            if (compiledAssembly.Name != StargateNetAsmdefName)
            {
                refAssembly = loader.Resolve(assembly.MainModule.AssemblyReferences.First(refName => refName.Name == StargateNetAsmdefName));
            }

            // 处理程序集，注入代码
            List<DiagnosticMessage> diagnostics = new();
            // 处理SyncVar标记
            diagnostics.AddRange(new NetworkedAttributeProcessor().ProcessAssembly(assembly, refAssembly));
            // 获取SyncVar大小
            diagnostics.AddRange(new NetworkBehaviorProcessor().ProcessAssembly(assembly));
            // 重新写回
            byte[] peData;
            byte[] pdbData;
            {
                var peStream = new MemoryStream();
                var pdbStream = new MemoryStream();
                var writeParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                    WriteSymbols = true,
                    SymbolStream = pdbStream
                };

                assembly.Write(peStream, writeParameters);
                peStream.Flush();
                pdbStream.Flush();

                peData = peStream.ToArray();
                pdbData = pdbStream.ToArray();
            }

            return new ILPostProcessResult(new InMemoryAssembly(peData, pdbData), diagnostics);
        }
    }
}