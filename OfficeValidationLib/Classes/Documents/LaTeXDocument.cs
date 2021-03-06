﻿using OfficeValidationLib.Interfaces;

namespace OfficeValidationLib.Classes.Documents
{
    public class LaTeXDocumentFactory : DocumentFactoryBase
    {
        public override string Name { get; protected set; } = "LaTeX";
        public override string[] SupportingExtension { get; protected set; } = new[]
        {
            ".tex"
        };
        protected override IDocument CreateInternal(string path) =>
            new LaTeXDocument(path, this);
    }

    public class LaTeXDocument : DocumentBase
    {
        public string Document { get; private set; }
        public LaTeXDocument(string path, IDocumentFactory creator) : base(path, creator) { }
        public override void InitializeInternal()
        {
            Document = System.IO.File.ReadAllText(Path);
        }
        public override void DisposeInternal()
        {
            Document = null;
        }
    }
}
