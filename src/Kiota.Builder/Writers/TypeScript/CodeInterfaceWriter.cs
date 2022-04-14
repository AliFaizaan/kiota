﻿
using System;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.TypeScript
{
    class CodeInterfaceWriter : BaseElementWriter<InterfaceDeclaration, TypeScriptConventionService>
    {
        private readonly CodeUsingWriter _codeUsingWriter;
        public CodeInterfaceWriter(TypeScriptConventionService conventionService, string clientNamespaceName) : base(conventionService)
        {
            _codeUsingWriter = new(clientNamespaceName);
        }
    

        /// <summary>
        /// Writes export statements for classes and enums belonging to a namespace into a generated index.ts file. 
        /// The classes should be export in the order of inheritance so as to avoid circular dependency issues in javascript.
        /// </summary>
        /// <param name="codeElement">Code element is a code namespace</param>
        /// <param name="writer"></param>
        public override void WriteCodeElement(InterfaceDeclaration codeInterface, LanguageWriter writer)
        {
            if (codeInterface == null) throw new ArgumentNullException(nameof(codeInterface));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            var parentNamespace = codeInterface.GetImmediateParentOfType<CodeNamespace>();
            _codeUsingWriter.WriteCodeElement(codeInterface.Usings, parentNamespace, writer);

            var inheritSymbol = conventions.GetTypeString(codeInterface.inherits, codeInterface);
            var derivation = (inheritSymbol == null ? string.Empty : $" extends {inheritSymbol}");
         //  conventions.WriteShortDescription((codeInterface.Parent as CodeClass).Description, writer);

            writer.WriteLine($"export interface {codeInterface.Name.ToFirstCharacterUpperCase()}{derivation}{{");
            writer.IncreaseIndent();
        }
    }
}
