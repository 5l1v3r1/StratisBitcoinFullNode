﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Stratis.SmartContracts.ContractValidation
{
    public class SmartContractDecompiler
    {
        public SmartContractDecompilation GetModuleDefinition(byte[] bytes)
        {
            // TODO: Ensure that AppContext.BaseDirectory is robust here
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(AppContext.BaseDirectory);
            var result = new SmartContractDecompilation();
            result.ModuleDefinition = ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters { AssemblyResolver = resolver });
            result.ContractType = result.ModuleDefinition.Types.FirstOrDefault(x => x.FullName != "<Module>");
            return result;
        }
    }
}
