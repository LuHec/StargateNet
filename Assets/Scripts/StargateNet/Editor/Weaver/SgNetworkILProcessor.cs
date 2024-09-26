using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

// ref:https://blog.sge-coretech.com/entry/2024/08/06/165144#Photon-Fusion-%E3%81%AE-RPC
public class SgNetworkILProcessor : ILPostProcessor
{
    public override ILPostProcessor GetInstance() => this;

    public override bool WillProcess(ICompiledAssembly compiledAssembly) => true;
    

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

        var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(compiledAssembly.InMemoryAssembly.PeData), readerParameters);
        
        ProcessAssembly(assembly);
        
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
        
        return new ILPostProcessResult(new InMemoryAssembly(peData, pdbData));
    }
    
    private void ProcessAssembly(AssemblyDefinition assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var type in module.GetTypes())
            {
                ProcessType(type);
            }
        }
    }

    private void ProcessType(TypeDefinition type)
    {
        throw new NotImplementedException();
    }

    class AssemblyResolver : BaseAssemblyResolver
    {
    }
}